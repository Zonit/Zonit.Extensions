using Example.Auth.Stubs;
using Example.Components;
using Zonit.Extensions;
using Zonit.Extensions.Cultures.Options;
using Zonit.Extensions.Website;

var builder = WebApplication.CreateBuilder(args);

// ─── AddWebsite() pulls every Zonit.Extensions core, the auth scheme, the cascading
// auth state, navigation/breadcrumbs/toast/cookie providers, and the dynamic
// PermissionPolicyProvider in one shot. Each demo area registers itself as an
// IWebsiteArea — its services flow through opts.AddArea(...).ConfigureServices.
builder.Services.AddWebsite(opts =>
{
    opts.Mode = WebsiteMode.Server;
    // Browser-Link / dotnet-watch can't inject scripts into a Brotli-compressed body.
    // Production hosts should keep this on.
    opts.Compression = !builder.Environment.IsDevelopment();

    // Each AddArea uses the SAME pattern a real consumer would use to plug
    // a feature module. Areas come from separate projects (Example.Cultures,
    // Example.Auth, ...) so navigation, services and pages are all encapsulated.
    opts.AddArea<HomeArea>();
    opts.AddArea<Example.Cultures.CulturesArea>();
    opts.AddArea<Example.Auth.AuthArea>();
    opts.AddArea<Example.Organizations.OrganizationsArea>();
    opts.AddArea<Example.Projects.ProjectsArea>();
    opts.AddArea<Example.Tenants.TenantsArea>();
    opts.AddArea<Example.Components.ComponentsArea>();
    opts.AddArea<Example.ValueObjects.ValueObjectsArea>();
});

// ─── Cultures: supported list drives the language switcher and Accept-Language fallback.
builder.Services.Configure<CultureOption>(o =>
{
    o.DefaultCulture = "en-US";
    o.DefaultTimeZone = "Europe/Warsaw";
    o.SupportedCultures = ["en-US", "pl-PL", "de-DE", "fr-FR"];
});

// ─── Consumer-side stubs are now registered by each area's ConfigureServices:
//   AuthArea          → DemoStore (singleton seed) + IAuthSource + IUserDirectory
//   OrganizationsArea → IOrganizationSource
//   ProjectsArea      → IProjectSource
//   TenantsArea       → ITenantSource (optional — drop the area to force solo-mode)
// A real consumer registers their own EF/Dapper/remote-API implementations of those
// contracts BEFORE AddWebsite() (or in their own areas); TryAdd* in the demo means
// our in-memory fallbacks never overwrite a real host's wiring.

var app = builder.Build();

// ─── UseWebsite() wires the full middleware pipeline:
//   ForwardedHeaders → Compression → Authentication → Authorization
//                    → SessionMiddleware → CultureMiddleware
//                    → WorkspaceMiddleware → ProjectMiddleware → TenantMiddleware
app.UseWebsite();

app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode()
   .AddAdditionalAssemblies(
        typeof(Example.Cultures.CulturesArea).Assembly,
        typeof(Example.Auth.AuthArea).Assembly,
        typeof(Example.Organizations.OrganizationsArea).Assembly,
        typeof(Example.Projects.ProjectsArea).Assembly,
        typeof(Example.Tenants.TenantsArea).Assembly,
        typeof(Example.Components.ComponentsArea).Assembly,
        typeof(Example.ValueObjects.ValueObjectsArea).Assembly);

// Login / logout endpoints — the only place we touch HttpContext directly.
app.MapPost("/auth/login", DemoLoginService.LoginAsync);
app.MapPost("/auth/logout", DemoLoginService.LogoutAsync);

app.Run();
