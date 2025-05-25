using Zonit.Extensions.Website.Abstractions.Cookies.Models;

namespace Zonit.Extensions.Website;

public interface ICookieProvider
{
    public CookieModel? Get(string key);
    public CookieModel Set(string key, string value, int days = 12 * 30);
    public CookieModel Set(CookieModel model);
    public Task<CookieModel> SetAsync(string key, string value, int days = 12 * 30);
    public Task<CookieModel> SetAsync(CookieModel model);
    public List<CookieModel> GetCookies();
}
