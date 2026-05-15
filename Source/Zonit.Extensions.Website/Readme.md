# Zonit.Extensions.Website

Blazor / ASP.NET Core integration layer that wires the Zonit value objects, providers and services into a ready-to-use base for Razor pages and components.

[![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Website.svg)](https://www.nuget.org/packages/Zonit.Extensions.Website/)
[![Downloads](https://img.shields.io/nuget/dt/Zonit.Extensions.Website.svg)](https://www.nuget.org/packages/Zonit.Extensions.Website/)

```bash
dotnet add package Zonit.Extensions.Website
```

## What you get

- **`PageBase` / `PageEditBase<T>` / `PageViewBase<T>`** — Razor base components with cancellation, logging, view-model binding and a source-generator that emits AOT-safe metadata for every `T`.
- **`ExtensionsBase`** — the parent base injecting `Culture`, `Workspace`, `Catalog`, `Authenticated` (Identity VO), `Toast`, `Cookie`, `Breadcrumbs`. Subscribes to `OnChange` events and re-renders.
- **Areas / plugin model** — `IWebsiteArea` interface, `WebsiteOptions.AddArea<T>()`, navigation aggregation through `INavigationProvider`.
- **Navigation** — `NavItem` / `NavGroup` with VO-typed `Title`, `Url`, `Permission` and `Target` enum; tree structure with children + groups; thread-safe runtime additions.
- **Breadcrumbs**, **toasts**, **cookies** — small, focused providers.
- **`AddWebsite()`** extension method that registers compression, forwarded headers, antiforgery, Razor components and all areas in one call.

## Setup

```csharp
builder.Services
    .AddCulturesExtension()
    .AddAuthExtension()
    .AddOrganizationsExtension()
    .AddProjectsExtension()
    .AddWebsite(o =>
    {
        o.AddArea<DashboardArea>();
        o.AddArea<AdminArea>();
    });

var app = builder.Build();
app
   .UseAuthExtension()
   .UseMiddleware<CultureMiddleware>()
   .UseMiddleware<OrganizationsMiddleware>()
   .UseMiddleware<ProjectsMiddleware>();
```

```razor
@* App.razor or Routes.razor *@
@using Zonit.Extensions
<ZonitCulturesExtension />
<ZonitIdentityExtension />
<ZonitOrganizationsExtension />
<ZonitProjectsExtension />
<ZonitCookiesExtension />
```

## A typical page

```razor
@page "/orders"
@inherits PageBase
@attribute [RequirePermission("orders.read")]

<h1>@T("Orders for {0}", Workspace.Organization.Name)</h1>

@if (Authenticated.IsAuthenticated)
{
    <p>Welcome, @Authenticated.Current.Name</p>
}

<MudButton OnClick="@(() => Toast.Show("Saved"))">@T("Save")</MudButton>
```

`T(key, args)` translates via `ICultureProvider`; `Authenticated.Current` is the `Identity` VO; `Workspace.Organization` is the `Organization` VO.

## Cookies

```razor
@inject ICookieProvider Cookie
@foreach (var c in Cookie.GetCookies())
{
    <p>@c.Name = @c.Value</p>
}

@code {
    async Task SetTheme()
        => await Cookie.SetAsync("theme", "dark", TimeSpan.FromDays(365));
}
```

`SetAsync` runs JavaScript in interactive circuits; `Set` is for the prerender pass.

## License

MIT.
