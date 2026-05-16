using Example;
using Example.Components;
using Zonit.Extensions;
using Zonit.Extensions.Cultures.Options;
using Zonit.Extensions.Website;

var builder = WebApplication.CreateBuilder(args);

// ─── Build-time: register the Website framework + each Area's DI services.
// Areas implementing IWebsiteServices (DemoStore, IAuthSource, IOrganizationSource,
// IProjectSource, ITenantSource registrations) flow through here exactly once,
// regardless of how many Sites later mount them.
builder.Services.AddWebsite(opts =>
{
    // MemoryCache / RazorComponents are services-only flags and default to true.
    // Set opts.Controllers = true if your areas need [ApiController] endpoints.
    opts.AddArea<HomeArea>();
    opts.AddArea<CulturesArea>();
    opts.AddArea<AuthArea>();
    opts.AddArea<OrganizationsArea>();
    opts.AddArea<ProjectsArea>();
    opts.AddArea<TenantsArea>();
    opts.AddArea<ComponentsArea>();
    opts.AddArea<ValueObjectsArea>();
    opts.AddArea<MudBlazorArea>();
});

// ─── Cultures: supported list drives the language switcher and Accept-Language fallback.
builder.Services.Configure<CultureOption>(o =>
{
    o.DefaultCulture = "en-US";
    o.DefaultTimeZone = "Europe/Warsaw";
    o.SupportedCultures = ["en-US", "pl-PL", "de-DE", "fr-FR"];
});

var app = builder.Build();

// ─── Runtime: mount each Site. Single Site at root in this demo.
//
// Multi-Site example:
//
//   app.UseWebsite<App>("/admin", o =>
//   {
//       o.Permission = "admin";          // gate every page under /admin behind the policy
//       o.AddArea<Example.Auth.AuthArea>();      // login also at /admin/login
//       o.AddArea<MyAdminArea>();
//   });
//
// Each UseWebsite<App>(…) call creates an isolated MapWhen branch with its own
// PathBase + MapRazorComponents — declare the catch-all root Site LAST.
app.UseWebsite<App>("/", o =>
{
    o.Mode = WebsiteMode.Server;
    // Browser-Link / dotnet-watch can't inject scripts into a Brotli-compressed body.
    o.Compression = !builder.Environment.IsDevelopment();
    // dev demo runs HTTP-only on a random port — opt out of HTTPS redirect.
    o.HttpsRedirection = !builder.Environment.IsDevelopment();

    o.AddArea<HomeArea>();
    o.AddArea<CulturesArea>();
    o.AddArea<AuthArea>();
    o.AddArea<OrganizationsArea>();
    o.AddArea<ProjectsArea>();
    o.AddArea<TenantsArea>();
    o.AddArea<ComponentsArea>();
    o.AddArea<ValueObjectsArea>();
    o.AddArea<MudBlazorArea>();
});

app.Run();
