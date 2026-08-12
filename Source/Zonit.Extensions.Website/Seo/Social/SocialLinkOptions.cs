namespace Zonit.Extensions.Website.Social;

/// <summary>
/// Configuration for the short social links — <c>example.com/instagram</c> instead of the profile
/// URL nobody can read out loud.
/// </summary>
/// <remarks>
/// <para><b>Only the named platforms get a short link.</b> <c>SocialMedia.Custom</c> entries do
/// not, and cannot: a route has to exist before any tenant is resolved, so a slug typed into an
/// admin panel could only be served by a catch-all — and a catch-all is the one thing that must
/// not be added here. It would match every otherwise-unmatched path, which turns
/// <c>/pl/typo</c> from "no endpoint, render the 404 page" into "a non-page endpoint under a
/// language prefix", and that answers a bare empty 404. Trading the styled error page for a short
/// link to a Discord invite is not a good trade.</para>
///
/// <para>Custom entries keep working where they always did — <c>llms.txt</c>, footers, anywhere
/// <c>SocialMedia.All()</c> is enumerated. They are simply not addresses on this domain.</para>
/// </remarks>
public sealed class SocialLinkOptions
{
    /// <summary>
    /// Whether the Site publishes them. On by default — the tenant is already there, and a link
    /// nobody configured is a route that never matches.
    /// </summary>
    /// <remarks>
    /// Turn it off for a Site whose root is not a place to publish anything, or when the routes
    /// are simply unwanted. Leaving it on for a Site whose tenant filled in nothing costs twelve
    /// entries in the endpoint table and no work per request.
    /// </remarks>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Segment the links live under, without slashes. Empty — the default — puts them at the Site
    /// root: <c>/instagram</c>.
    /// </summary>
    /// <remarks>
    /// The root is what makes the link worth having; <c>/go/instagram</c> is barely shorter than
    /// the profile URL. A prefix is not needed to avoid colliding with a page — a page at
    /// <c>/discord</c> wins on its own, see <see cref="Order"/> — it is for the case where a Site
    /// wants the links somewhere else entirely.
    /// </remarks>
    public string Prefix { get; set; } = string.Empty;

    /// <summary>
    /// Routing order of the redirects. Deliberately high, so anything else matching the same path
    /// wins.
    /// </summary>
    /// <remarks>
    /// These routes are on by default and sit at the Site root, where a project may genuinely have
    /// a page called <c>/discord</c>. Two endpoints with the same template and the same order is an
    /// ambiguous match — an exception, thrown at request time rather than at start-up, on a route
    /// the framework added without being asked. Ordering them last turns that into the answer
    /// everyone would have chosen anyway: the application's own page is served, and the convenience
    /// redirect quietly does not exist for that one platform.
    /// </remarks>
    public int Order { get; set; } = 10_000;

    /// <summary>
    /// How long a browser or CDN may reuse the redirect, in seconds. Five minutes by default.
    /// </summary>
    /// <remarks>
    /// The target is a tenant setting, editable without a deployment, so it must not be pinned in
    /// a cache for long — but a shared link gets clicked in bursts, and a few minutes absorbs the
    /// burst while keeping a correction minutes away rather than permanent. This is also why the
    /// redirect is <c>302</c> and not <c>301</c>: a permanent redirect to an address that can
    /// change is a promise the site cannot keep.
    /// </remarks>
    public int CacheSeconds { get; set; } = 300;

    /// <summary>Normalised prefix — empty, or <c>/segment</c> with no trailing slash.</summary>
    internal string NormalizedPrefix
    {
        get
        {
            var value = Prefix.Trim().Trim('/');
            return value.Length == 0 ? string.Empty : "/" + value;
        }
    }
}
