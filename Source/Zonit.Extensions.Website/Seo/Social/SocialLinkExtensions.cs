using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Zonit.Extensions.Tenants;
using Zonit.Extensions.Tenants.Settings;

namespace Zonit.Extensions.Website.Social;

/// <summary>
/// Publishes the tenant's social profiles as short links on the Site's own domain —
/// <c>example.com/instagram</c> rather than the profile URL.
/// </summary>
internal static class SocialLinkEndpoints
{
    /// <summary>
    /// Maps one redirect per named platform inside a Site's branch. Called by the kernel from
    /// <c>UseWebsite</c>; configured through <c>SiteOptions.Social</c>.
    /// </summary>
    /// <remarks>
    /// <para><b>Endpoints, not middleware.</b> Middleware would have to run a comparison on every
    /// request in the application to serve a link clicked a few times a day, would be invisible to
    /// the endpoint table that every routing diagnostic reads, and — the part that matters here —
    /// would run <em>before</em> routing, so <c>/pl/instagram</c> would redirect too. A short link
    /// is not a page: it has one address, and as an endpoint it gets that from the same rule that
    /// gives <c>robots.txt</c> one address.</para>
    ///
    /// <para><b>Per request, not per start-up.</b> Every platform is mapped and each checks the
    /// tenant when asked, because in a multi-site host the set of configured profiles differs per
    /// tenant and the endpoint table is fixed once. A platform this tenant left blank answers
    /// <c>404</c>.</para>
    ///
    /// <para>The twelve named platforms only — see <see cref="SocialLinkOptions"/> for why a
    /// custom entry cannot have one.</para>
    ///
    /// <para>Responses carry <c>X-Robots-Tag: noindex</c>. The redirect target is somebody else's
    /// site, and there is nothing here for a search engine to hold: the profile is already
    /// declared as <c>sameAs</c> in the structured data, which is the statement that belongs in
    /// an index.</para>
    /// </remarks>
    internal static void Map(IEndpointRouteBuilder endpoints, SocialLinkOptions options)
    {
        var prefix = options.NormalizedPrefix;

        // Slugs come from the same list sameAs and llms.txt walk. A hand-written list here
        // would be the third copy, and the second one had already drifted to half the
        // platforms before anyone noticed.
        foreach (var label in SocialMediaModel.Platforms)
            Map(endpoints, prefix + "/" + Slug(label), label, options);
    }

    private static void Map(
        IEndpointRouteBuilder endpoints, string pattern, string label, SocialLinkOptions options)
    {
        endpoints.MapMethods(pattern, GetAndHead, context =>
        {
            var social = context.RequestServices.GetRequiredService<ITenantProvider>().Settings.SocialMedia;
            var target = Resolve(social, label);

            if (string.IsNullOrWhiteSpace(target))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return Task.CompletedTask;
            }

            context.Response.Headers["X-Robots-Tag"] = "noindex";
            context.Response.Headers.CacheControl = $"public, max-age={options.CacheSeconds}";

            // Temporary: the destination is a tenant setting and can be corrected without a
            // deployment, which a permanent redirect would make impossible for anyone who already
            // followed the old one.
            context.Response.Redirect(target, permanent: false);
            return Task.CompletedTask;
        })
        .AllowAnonymous().ExcludeFromDescription().WithOrder(options.Order);
    }

    private static string? Resolve(SocialMediaModel? social, string label)
    {
        if (social is null)
            return null;

        // includeCustom: false — a custom entry has no short link, so matching one here could only
        // ever be a label collision with a named platform, answering the wrong URL.
        foreach (var (candidate, url) in social.All(includeCustom: false))
        {
            if (string.Equals(candidate, label, StringComparison.OrdinalIgnoreCase))
                return url;
        }

        return null;
    }

    /// <summary>
    /// <c>"LinkedIn"</c> → <c>"linkedin"</c>, <c>"Facebook group"</c> → <c>"facebook-group"</c>.
    /// </summary>
    private static string Slug(string label)
    {
        var slug = new System.Text.StringBuilder(label.Length);

        foreach (var character in label)
        {
            if (char.IsLetterOrDigit(character))
                slug.Append(char.ToLowerInvariant(character));
            else if (slug.Length > 0 && slug[^1] != '-')
                slug.Append('-');
        }

        return slug.ToString().TrimEnd('-');
    }

    private static readonly string[] GetAndHead = ["GET", "HEAD"];
}
