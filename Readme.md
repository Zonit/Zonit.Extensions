## Useful tools for Blazor

### Extensions:
- [Zonit.Extensions.Reflection](https://github.com/Zonit/Zonit.Extensions/tree/master/Source/Zonit.Extensions/Reflection) - A utility class for discovering assemblies and types that implement or inherit from a specified base type.
- [Zonit.Extensions.Xml](https://github.com/Zonit/Zonit.Extensions/tree/master/Source/Zonit.Extensions/Xml) - A utility class for serializing objects to XML and deserializing XML back to objects using .NET's XML serialization.
- [Zonit.Extensions.Website.Components](https://github.com/Zonit/Zonit.Extensions/tree/master/Source/Zonit.Extensions.Website/Components)

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