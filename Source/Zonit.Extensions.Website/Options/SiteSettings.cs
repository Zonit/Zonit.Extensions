namespace Zonit.Extensions.Website;

/// <summary>
/// The part of a Site's configuration that a <b>deployment</b> decides — bindable from the
/// <c>Website</c> configuration section, keyed by mount path, and re-read when configuration
/// reloads.
/// </summary>
/// <remarks>
/// <para><b>Two keys, and deliberately no more.</b> Both answer the same question — what this
/// environment lets search engines see — and that is the one thing that legitimately differs
/// between a staging box and production while the build is identical. Everything else has a
/// better home:</para>
///
/// <list type="bullet">
///   <item><b>Code</b> (<c>SiteOptions</c>) for the structural: cookie and global names, the root
///         attribute, the culture URL strategy and format, document assets, crawl directives.
///         Markup and CSS are written against these; a deployment that could retune them would
///         be able to break the site without touching it, and half of them are baked into the
///         pipeline at startup anyway.</item>
///   <item><b>Tenant settings</b> for anything a person would recognise: site title, meta
///         description, canonical URL, title composition, logo, favicon, default colour scheme.
///         That tree already has storage, validation and an admin UI — a second place to set the
///         site title would be a second place for it to be wrong.</item>
/// </list>
///
/// <code language="json">
/// {
///   "Website": {
///     "/":      { "Indexable": true, "IndexedCultures": [ "en-us", "pl-pl" ] },
///     "/admin": { "Indexable": false }
///   }
/// }
/// </code>
///
/// <para>The key is the mount path as passed to <c>UseWebsite</c>. <c>"/"</c> and <c>""</c> both
/// name the root mount, and a trailing slash is ignored, so <c>"/admin"</c> and <c>"/admin/"</c>
/// are the same Site.</para>
/// </remarks>
public sealed class SiteSettings
{
    /// <summary>
    /// Whether search engines may index pages under this Site. <see langword="null"/> — the
    /// default — derives the answer from <c>SiteOptions.Permission</c>: a Site that requires a
    /// permission is closed, so it is served <c>noindex, nofollow</c> and a blanket
    /// <c>robots.txt</c> disallow.
    /// </summary>
    /// <remarks>
    /// Derived rather than defaulted to a constant, because both constants are wrong. Hard
    /// <see langword="false"/> would silently de-index every existing public site on upgrade —
    /// the worst class of regression, invisible until traffic disappears. Hard
    /// <see langword="true"/> would publish every panel that forgot to say otherwise. Tying it to
    /// the permission makes the safe answer automatic for exactly the mounts that need it, and
    /// setting it to <see langword="false"/> in a staging <c>appsettings</c> is how a
    /// production-shaped environment stays out of search without a separate build.
    /// </remarks>
    public bool? Indexable { get; set; }

    /// <summary>
    /// Cultures allowed into search indexes. <see langword="null"/> — the default — means every
    /// supported culture, which is the right answer for a site whose translations are complete.
    /// </summary>
    /// <remarks>
    /// <para>A different axis from the culture allow-list, which answers "can this language be
    /// rendered". This answers "may it appear in search results", and the two genuinely differ
    /// while a translation is finished but unreviewed. A supported-but-unindexed culture renders
    /// normally; it is served <c>noindex, follow</c>, left out of the <c>hreflang</c> cluster and
    /// disallowed in <c>robots.txt</c>.</para>
    ///
    /// <para>Configuration rather than code so a language can be staged or pulled without a
    /// deployment. Entries outside the supported allow-list are dropped rather than honoured:
    /// advertising an <c>hreflang</c> for a culture the site cannot render points search engines
    /// at a 404.</para>
    /// </remarks>
    public string[]? IndexedCultures { get; set; }

    /// <summary>
    /// Deep copy — the merge starts from a clone of the code defaults so a reload cannot mutate
    /// them and leave the next reload merging onto already-overridden values.
    /// </summary>
    internal SiteSettings Clone() => new()
    {
        Indexable = Indexable,
        IndexedCultures = IndexedCultures?.ToArray(),
    };
}
