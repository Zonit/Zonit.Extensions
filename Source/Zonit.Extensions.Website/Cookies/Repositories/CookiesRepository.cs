using Zonit.Extensions.Website.Abstractions.Cookies.Models;

namespace Zonit.Extensions.Website.Cookies.Repositories;

internal class CookiesRepository : ICookiesRepository
{
    List<CookieModel> Cookies { get; set; } = [];

    public List<CookieModel> GetCookies()
        => this.Cookies;

    public CookieModel Add(CookieModel cookie)
    {
        var existingCookie = Cookies.SingleOrDefault(x => x.Name == cookie.Name);

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

    public void Inicjalize(List<CookieModel> cookies)
        => Cookies = cookies;
}