using Zonit.Extensions.Website.Abstractions.Cookies.Models;

namespace Zonit.Extensions.Website.Cookies.Repositories;

public interface ICookiesRepository
{
    public void Inicjalize(List<CookieModel> cookies);
    public CookieModel Add(CookieModel cookie);
    public List<CookieModel> GetCookies();
}