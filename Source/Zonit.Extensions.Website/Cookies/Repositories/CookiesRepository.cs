using Zonit.Extensions.Website.Cookies.Models;

namespace Zonit.Extensions.Website.Cookies.Repositories;

internal class CookiesRepository : ICookiesRepository
{
    List<CookieModel> Cookies { get; set; } = [];

    public List<CookieModel> GetCookies()
        => this.Cookies;

    public CookieModel Add(CookieModel cookie)
    {
        // Cookie names are case-insensitive per RFC 6265 §4.1.2; matching with
        // OrdinalIgnoreCase here keeps `Set("session", …)` after `Set("Session", …)`
        // from creating a duplicate slot in the per-request snapshot.
        var existingCookie = Cookies.SingleOrDefault(x => string.Equals(x.Name, cookie.Name, StringComparison.OrdinalIgnoreCase));

        if (existingCookie is not null)
        {
            existingCookie.Value = cookie.Value;
            existingCookie.Domain = cookie.Domain;
            existingCookie.Path = cookie.Path;
            existingCookie.Expires = cookie.Expires;
            existingCookie.Secure = cookie.Secure;
            existingCookie.HttpOnly = cookie.HttpOnly;
            existingCookie.SameSite = cookie.SameSite;
            existingCookie.Session = cookie.Session;

            return existingCookie;
        }

        this.Cookies.Add(cookie);

        return cookie;
    }

    public void Initialize(List<CookieModel> cookies)
        => Cookies = cookies;
}