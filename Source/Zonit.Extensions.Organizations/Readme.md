# Zonit.Extensions.Organizations

Tenant / workspace context for ASP.NET Core and Blazor. Exposes the user's currently selected organization as the [`Organization`](../Zonit.Extensions/Readme.md) value object — keeping authorization in one place (`Zonit.Extensions.Auth`).

[![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Organizations.svg)](https://www.nuget.org/packages/Zonit.Extensions.Organizations/)
[![Downloads](https://img.shields.io/nuget/dt/Zonit.Extensions.Organizations.svg)](https://www.nuget.org/packages/Zonit.Extensions.Organizations/)

```bash
dotnet add package Zonit.Extensions.Organizations
```

## What you get

- **`IWorkspaceProvider.Organization : Organization`** — current tenant as VO. Returns `Organization.Empty` when none is selected.
- **`IWorkspaceManager`** — write surface (`Initialize(...)`, `SwitchOrganizationAsync(id)`, exposes `Workspace`/`Organizations`/`State`).
- **`IUserOrganizationManager`** — consumer-implemented contract for backend hydration.
- A Blazor `<ZonitOrganizationsExtension />` component that bridges prerendered state across render modes.

## Setup

```csharp
builder.Services.AddOrganizationsExtension();
app.UseMiddleware<OrganizationsMiddleware>();
```

```razor
@using Zonit.Extensions
<ZonitOrganizationsExtension />
```

Implement `IUserOrganizationManager` in your app (typically backed by EF Core).

## Reading the workspace

```razor
@inject IWorkspaceProvider Workspace

@if (Workspace.Organization.HasValue)
{
    <p>Tenant: @Workspace.Organization.Name</p>
    <p>Id: @Workspace.Organization.Id</p>
}
```

## Switching organization

```razor
@inject IWorkspaceManager Manager

<button @onclick="@(async () => await Manager.SwitchOrganizationAsync(orgId))">
    Switch
</button>
```

The change raises `OnChange`, observed by `IWorkspaceProvider` and any subscribers (incl. `ExtensionsBase` in the Website package).

## Authorization is elsewhere

Earlier versions exposed `IsPermission(string)` / `IsRole(string)` on this provider with hard-coded `"Developer"` / `"All"` bypass paths. **That is gone**. Authorization lives in [Zonit.Extensions.Auth](../Zonit.Extensions.Auth/Readme.md) (`[RequirePermission]`, `IAuthorizationService`, `<AuthorizeView>`). Pure tenancy lives here.

## License

MIT.
