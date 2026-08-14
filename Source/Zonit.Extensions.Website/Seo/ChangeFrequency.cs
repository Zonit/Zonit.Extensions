namespace Zonit.Extensions.Website.Sitemaps;

// Deliberately still in .Sitemaps, not moved up to .Website where the attribute lives, even
// though that costs consumers a second `using`. This enum's full name is baked into code the
// SOURCE GENERATOR wrote, in the constructor signature of StaticPage — moving it makes every
// assembly compiled against an earlier package throw MissingMethodException from its module
// initializer, before a single line of it runs. A one-line `using` is a papercut; a crash at
// assembly load, for code nobody typed, is not a trade worth making.

/// <summary>
/// How often a page's content is expected to change. A hint, not a contract — crawlers weigh it
/// against what they observe, and a page that claims <see cref="Hourly"/> and never changes is
/// simply believed less next time.
/// </summary>
public enum ChangeFrequency
{
    /// <summary>
    /// Not stated. The default, and the honest answer for most pages — the element is omitted
    /// rather than filled with a guess.
    /// </summary>
    /// <remarks>
    /// Zero on purpose, so <c>[Seo]</c> and <c>SitemapEntry</c> both default to "say nothing"
    /// without either of them special-casing it.
    /// </remarks>
    Unset = 0,

    /// <summary>Changes on every access.</summary>
    Always,
    /// <summary>Hourly.</summary>
    Hourly,
    /// <summary>Daily.</summary>
    Daily,
    /// <summary>Weekly.</summary>
    Weekly,
    /// <summary>Monthly.</summary>
    Monthly,
    /// <summary>Yearly.</summary>
    Yearly,
    /// <summary>Archived — will not change again.</summary>
    Never,
}
