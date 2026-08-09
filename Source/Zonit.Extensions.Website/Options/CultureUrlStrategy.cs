namespace Zonit.Extensions.Website;

/// <summary>
/// Whether a Site encodes the active culture in its URLs.
/// </summary>
/// <remarks>
/// <para>This is a per-mount decision, not a per-application one, and that is the whole
/// point: one host routinely runs a public multilingual site at <c>/</c> and a closed,
/// single-audience panel at <c>/admin</c>. Forcing the panel to carry <c>/pl/</c> in every
/// URL buys nothing — it is behind a login, it is never indexed, and the extra segment only
/// makes bookmarks longer and the mount registry harder to reason about.</para>
///
/// <para><see cref="None"/> is the default so that mounting a Site changes nothing about its
/// URLs unless the author asks for it. Everything the culture prefix drags along —
/// canonical tags, the <c>hreflang</c> cluster, redirects for the unprefixed form — is
/// switched on by <see cref="Prefix"/> and by nothing else.</para>
/// </remarks>
public enum CultureUrlStrategy
{
    /// <summary>
    /// No culture in the URL. The active culture comes from the cookie, then
    /// <c>Accept-Language</c>, then the configured default — which is exactly how a panel,
    /// an internal tool or a single-language site should behave.
    /// </summary>
    None = 0,

    /// <summary>
    /// The culture leads the path: <c>/pl/pricing</c>. Enables the full public-web contract —
    /// a canonical spelling per language with a permanent redirect from the other, a
    /// <c>hreflang</c> cluster over the indexed cultures, and an unprefixed form that
    /// detects the visitor's language and redirects rather than rendering a second copy of
    /// the page at a second address.
    /// </summary>
    Prefix = 1,
}
