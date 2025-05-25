using Microsoft.JSInterop;
using Zonit.Extensions.Website.Abstractions.Cookies.Models;
using Zonit.Extensions.Website.Cookies.Repositories;

namespace Zonit.Extensions.Website.Cookies.Services;

public class CookieService(
    IJSRuntime _runtime,
    ICookiesRepository _cookieRepository
    ) : ICookieProvider
{
    List<CookieModel> Cookies { get; set; } = _cookieRepository.GetCookies();

    public CookieModel? Get(string key)
        => this.Cookies?
            .SingleOrDefault(x => x.Name.Equals(key, StringComparison.OrdinalIgnoreCase));

    public List<CookieModel> GetCookies()
        => this.Cookies;

    public async Task<CookieModel> SetAsync(string key, string value, int days = 360)
        => await this.SetAsync(new CookieModel
        {
            Name = key,
            Value = value,
            Expires = DateTime.UtcNow.AddDays(days),
        });

    public CookieModel Set(string key, string value, int days = 360)
        => this.Set(new CookieModel
        {
            Name = key,
            Value = value,
            Expires = DateTime.UtcNow.AddDays(days),
        });

    public CookieModel Set(CookieModel model)
        => _cookieRepository.Add(model);

    public async Task<CookieModel> SetAsync(CookieModel model)
    {
        var cookie = this.Set(model);

        if (_runtime is not null)
        {
            var cookieString = $"{cookie.Name}={cookie.Value}";

            if (cookie.Expires.HasValue)
                cookieString += $"; expires={cookie.Expires.Value:R}";

            if (!string.IsNullOrWhiteSpace(cookie.Domain))
                cookieString += $"; domain={cookie.Domain}";

            if (!string.IsNullOrWhiteSpace(cookie.Path))
                cookieString += $"; path={cookie.Path}";

            if (cookie.Secure)
                cookieString += "; secure";

            if (cookie.HttpOnly)
                cookieString += "; HttpOnly";

            if (cookie.SameSite)
                cookieString += "; SameSite=Strict";

            // Check if it's a session cookie
            if (!cookie.Session && cookie.Expires.HasValue)
            {
                var maxAge = (cookie.Expires.Value - DateTime.UtcNow).TotalSeconds;
                cookieString += $"; Max-Age={maxAge}";
            }

            await _runtime.InvokeVoidAsync("eval", $"document.cookie = '{cookieString}';");
        }

        return cookie;
    }
}