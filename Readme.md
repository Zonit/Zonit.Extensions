## Useful tools for Blazor

**Nuget Package Abstraction**
```
Install-Package Zonit.Extensions.Website.Abstractions 
```

**Nuget Package Extensions**
```
Install-Package Zonit.Extensions.Website
```

## Cookie handling with support for Blazor

### Installation:
Add this in ``Routes.razor``
```razor
@using Zonit.Extensions

<ZonitCookiesExtension />
```

Services in ``Program.cs``
```cs
builder.Services.AddCookiesExtension();
```
App in ``Program.cs``
```cs
app.UseCookiesExtension();
```

### Example:

```razor
@page "/"
@rendermode InteractiveServer
@using Zonit.Extensions.Website
@inject ICookieProvider Cookie

@foreach (var cookie in Cookie.GetCookies())
{
    <p>@cookie.Name @cookie.Value</p>
}
```


**API**
```cs
    public CookieModel? Get(string key);
    public CookieModel Set(string key, string value, int days = 12 * 30);
    public CookieModel Set(CookieModel model);
    public Task<CookieModel> SetAsync(string key, string value, int days = 12 * 30);
    public Task<CookieModel> SetAsync(CookieModel model);
    public List<CookieModel> GetCookies();
```

We use SetAsync only in the Blazor circuit. It executes the JS code with the Cookies record.