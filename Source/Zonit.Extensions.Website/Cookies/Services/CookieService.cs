using System.Globalization;
using Microsoft.JSInterop;
using Zonit.Extensions.Website.Cookies.Models;
using Zonit.Extensions.Website.Cookies.Repositories;

namespace Zonit.Extensions.Website.Cookies.Services;

/// <summary>
/// Provides browser cookie access for Blazor components. Reads come from the
/// per-request <see cref="ICookiesRepository"/> snapshot; writes go to the
/// browser via a tiny self-contained JS helper that is installed on demand.
/// </summary>
/// <remarks>
/// <para><b>No static asset.</b> The helper used to live in
/// <c>wwwroot/cookies.js</c>, which required the consumer to wire
/// <c>UseStaticFiles()</c> / <c>MapStaticAssets()</c> correctly and to ship
/// the file in publish output. With that file gone the service is self-contained
/// and works in any host (server, prerender, single-file publish).</para>
/// <para><b>Security.</b> The previous eval()-based writer interpolated
/// attacker-controllable cookie names/values straight into a JS string — a
/// classic injection sink. Here the bootstrap script is a <em>compile-time
/// constant</em>: nothing user-controlled is ever concatenated into JS source.
/// Each cookie attribute is forwarded as a separate JSInterop argument and the
/// helper itself uses <c>encodeURIComponent</c> + <c>document.cookie</c>, so
/// name/value can never escape the attribute syntax.</para>
/// <para><b>AOT.</b> Only primitive arguments (strings, longs, bools) cross the
/// JSInterop boundary, so no reflection-based JSON is required. Compatible with
/// NativeAOT publish.</para>
/// <para><b>CSP.</b> The one-time bootstrap uses <c>eval</c> on a constant
/// string — hosts that ship a strict CSP without <c>'unsafe-eval'</c> should
/// keep shipping their own JS file and replace this service.</para>
/// </remarks>
public class CookieService : ICookieProvider
{
    // One-shot bootstrap. Defined as a const so it is impossible to splice
    // attacker-controlled data into the JS source: the only inputs to the
    // helpers below are JSInterop arguments, which are passed as native JS
    // values, never as string concatenation.
    private const string BootstrapScript = """
        (function () {
            if (window.__zonitCookies) { return; }
            window.__zonitCookies = {
                set: function (name, value, expires, domain, path, maxAge, secure, sameSite) {
                    if (typeof name !== 'string' || name.length === 0) { return; }
                    var p = [encodeURIComponent(name) + '=' + encodeURIComponent(value == null ? '' : value)];
                    if (expires) { p.push('expires=' + expires); }
                    if (domain)  { p.push('domain='  + domain);  }
                    if (path)    { p.push('path='    + path);    }
                    if (typeof maxAge === 'number' && isFinite(maxAge)) { p.push('Max-Age=' + Math.trunc(maxAge)); }
                    if (secure)   { p.push('Secure'); }
                    if (sameSite) { p.push('SameSite=Strict'); }
                    document.cookie = p.join('; ');
                },
                del: function (name, path, domain) {
                    if (typeof name !== 'string' || name.length === 0) { return; }
                    var p = [
                        encodeURIComponent(name) + '=',
                        'expires=Thu, 01 Jan 1970 00:00:00 GMT',
                        'Max-Age=0'
                    ];
                    if (path)   { p.push('path='   + path);   }
                    if (domain) { p.push('domain=' + domain); }
                    document.cookie = p.join('; ');
                }
            };
        })();
        """;

    private readonly IJSRuntime _runtime;
    private readonly ICookiesRepository _cookieRepository;

    // Per-circuit latch. Once the helper is installed in window.__zonitCookies
    // for this IJSRuntime, we skip the bootstrap on subsequent writes — the JS
    // side guards re-entry anyway, but skipping the round-trip is cheaper.
    private bool _bootstrapped;

    public CookieService(IJSRuntime runtime, ICookiesRepository cookieRepository)
    {
        _runtime = runtime;
        _cookieRepository = cookieRepository;
        Cookies = cookieRepository.GetCookies();
    }

    List<CookieModel> Cookies { get; set; }

    public CookieModel? Get(string key)
        => this.Cookies?
            .SingleOrDefault(x => x.Name.Equals(key, StringComparison.OrdinalIgnoreCase));

    public List<CookieModel> GetCookies() => this.Cookies;

    public CookieModel Set(string key, string value, TimeSpan lifetime)
        => Set(key, value, DateTime.UtcNow + lifetime);

    public Task<CookieModel> SetAsync(string key, string value, TimeSpan lifetime)
        => SetAsync(key, value, DateTime.UtcNow + lifetime);

    public CookieModel Set(string key, string value, DateTime expires)
        => Set(new CookieModel { Name = key, Value = value, Expires = expires });

    public Task<CookieModel> SetAsync(string key, string value, DateTime expires)
        => SetAsync(new CookieModel { Name = key, Value = value, Expires = expires });

    public CookieModel Set(CookieModel model)
        => _cookieRepository.Add(model);

    public async Task<CookieModel> SetAsync(CookieModel model)
    {
        var cookie = this.Set(model);

        if (_runtime is null)
            return cookie;

        try
        {
            if (!_bootstrapped)
            {
                // Idempotent on the JS side too — the bootstrap script no-ops if
                // window.__zonitCookies is already defined.
                await _runtime.InvokeVoidAsync("eval", BootstrapScript).ConfigureAwait(false);
                _bootstrapped = true;
            }

            string? expiresRfc1123 = cookie.Expires?.ToString("R", CultureInfo.InvariantCulture);

            // Max-Age must be an integer per RFC 6265. Compute as long so very long
            // lifetimes don't overflow; pass null for session cookies so the
            // browser falls back to the Expires attribute (or session lifetime).
            //
            // Note: HttpOnly is intentionally NOT forwarded — browsers ignore the
            // attribute when the cookie is set from JS, so claiming to support it
            // would be misleading. Real HttpOnly cookies must be issued by the
            // server via Set-Cookie response headers.
            long? maxAgeSeconds = (!cookie.Session && cookie.Expires.HasValue)
                ? (long)(cookie.Expires.Value - DateTime.UtcNow).TotalSeconds
                : null;

            await _runtime.InvokeVoidAsync(
                "__zonitCookies.set",
                cookie.Name,
                cookie.Value,
                expiresRfc1123,
                string.IsNullOrWhiteSpace(cookie.Domain) ? null : cookie.Domain,
                string.IsNullOrWhiteSpace(cookie.Path) ? null : cookie.Path,
                maxAgeSeconds,
                cookie.Secure,
                cookie.SameSite).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            // JS interop isn't available yet (e.g. server-side prerendering).
            // The cookie is still recorded in the scoped repository — caller can
            // retry the JS write after first interactive render.
        }
        catch (JSDisconnectedException)
        {
            // Circuit was disposed mid-call. Nothing to do.
        }

        return cookie;
    }
}
