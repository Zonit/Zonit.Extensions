using Zonit.Extensions.Website.Cookies.Models;

namespace Zonit.Extensions.Website.Cookies.Repositories;

public interface ICookiesRepository
{
    public void Initialize(List<CookieModel> cookies);
    public CookieModel Add(CookieModel cookie);
    public List<CookieModel> GetCookies();
}