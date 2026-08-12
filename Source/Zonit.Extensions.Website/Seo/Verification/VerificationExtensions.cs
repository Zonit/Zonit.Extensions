using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Zonit.Extensions.Tenants;
using Zonit.Extensions.Tenants.Settings;

namespace Zonit.Extensions.Website.Verification;

/// <summary>
/// Serves the ownership documents that cannot be a meta tag — Apple's, which are files at
/// <c>/.well-known/</c>.
/// </summary>
/// <remarks>
/// Search engines verify a domain with a <c>&lt;meta&gt;</c> tag, and those need no call here: a
/// token in <c>Tenant.Settings.Verification</c> is rendered into every page's head automatically.
/// Apple does not work that way — Universal Links, App Clips and Apple Pay each read a file from a
/// fixed address, so those need a route.
/// </remarks>
internal static class VerificationEndpoints
{
    private const string AppSiteAssociation = "/.well-known/apple-app-site-association";
    private const string MerchantAssociation = "/.well-known/apple-developer-merchantid-domain-association";

    /// <summary>
    /// Maps Apple's association files inside a Site's branch. Both answer <c>404</c> until the
    /// tenant supplies the corresponding value. Called by the kernel from <c>UseWebsite</c>;
    /// switched off through <c>SiteOptions.Verification.Enabled</c>.
    /// </summary>
    /// <remarks>
    /// <para><b>Content type matters here more than usual.</b> The association document must be
    /// served as <c>application/json</c> and Apple's fetcher does not follow redirects — a site
    /// that answers the file from a redirect, or serves it as <c>text/html</c>, fails verification
    /// with no error a browser would ever show you.</para>
    ///
    /// <para>Mapped per Site rather than globally so a mounted panel does not answer for the
    /// domain, and so a multi-site host serves each tenant's own document from that tenant's
    /// hostname — which is the whole point of proving ownership.</para>
    /// </remarks>
    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        Map(endpoints, AppSiteAssociation, "application/json", static v => v.AppleAppSiteAssociation);
        Map(endpoints, MerchantAssociation, "text/plain", static v => v.AppleMerchant);
    }

    private static void Map(
        IEndpointRouteBuilder endpoints,
        string pattern,
        string contentType,
        Func<VerificationModel, string?> select)
    {
        endpoints.MapMethods(pattern, GetAndHead, context =>
        {
            var tenant = context.RequestServices.GetRequiredService<ITenantProvider>();
            var body = select(tenant.Settings.Verification);

            if (string.IsNullOrWhiteSpace(body))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return Task.CompletedTask;
            }

            context.Response.ContentType = contentType;

            // Short, not long. These change when an app ships a new bundle identifier, and a file
            // pinned in a CDN for a day is a day of Universal Links opening the website instead of
            // the app — with nothing in any log to say why.
            context.Response.Headers.CacheControl = "public, max-age=300";
            context.Response.Headers["X-Robots-Tag"] = "noindex";

            return context.Response.WriteAsync(body, context.RequestAborted);
        })
        .AllowAnonymous().ExcludeFromDescription();
    }

    private static readonly string[] GetAndHead = ["GET", "HEAD"];
}
