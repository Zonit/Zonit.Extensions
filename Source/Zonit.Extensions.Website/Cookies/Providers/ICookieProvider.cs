using Zonit.Extensions.Website.Cookies.Models;

namespace Zonit.Extensions.Website;

public interface ICookieProvider
{
    CookieModel? Get(string key);
    List<CookieModel> GetCookies();

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
