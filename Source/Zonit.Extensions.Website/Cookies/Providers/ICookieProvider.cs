using Zonit.Extensions.Website.Cookies.Models;

namespace Zonit.Extensions.Website;

public interface ICookieProvider
{
    CookieModel? Get(string key);
    List<CookieModel> GetCookies();

    /// <summary>
    /// Re-hydrates the in-memory cookie list from <c>document.cookie</c> via JSInterop.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In Blazor Server the HTTP-request scope (where <c>CookieMiddleware</c> seeds the
    /// repository from <c>HttpContext.Request.Cookies</c>) is <b>not</b> the same DI
    /// scope as the interactive circuit — a fresh circuit starts with an empty repo
    /// and <see cref="Get"/> would always return <see langword="null"/> even though
    /// the browser has the cookies. Call this once on first interactive render
    /// (e.g. inside <c>OnAfterRenderAsync(firstRender: true)</c>) to bridge the
    /// gap. No-op during prerender / when JSInterop is not yet available.
    /// </para>
    /// <para>
    /// HttpOnly cookies are intentionally <em>not</em> visible to JavaScript and will
    /// therefore not appear in the refreshed list — that is a browser security
    /// guarantee, not a framework limitation.
    /// </para>
    /// </remarks>
    Task RefreshAsync();

    // ---- TimeSpan-based lifetime (preferred) ----
    CookieModel Set(string key, string value, TimeSpan lifetime);
    Task<CookieModel> SetAsync(string key, string value, TimeSpan lifetime);

    // ---- Absolute expiry ----
    CookieModel Set(string key, string value, DateTime expires);
    Task<CookieModel> SetAsync(string key, string value, DateTime expires);

    // ---- Full-control overloads ----
    CookieModel Set(CookieModel model);
    Task<CookieModel> SetAsync(CookieModel model);

    // ---- Convenience: 1-year default ----
    CookieModel Set(string key, string value)
        => Set(key, value, TimeSpan.FromDays(365));
    Task<CookieModel> SetAsync(string key, string value)
        => SetAsync(key, value, TimeSpan.FromDays(365));
}
