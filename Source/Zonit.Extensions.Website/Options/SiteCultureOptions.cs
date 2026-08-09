namespace Zonit.Extensions.Website;

/// <summary>
/// Per-Site culture URL policy, reached through <c>SiteOptions.Cultures</c> and bindable from the
/// <c>Website</c> configuration section.
/// </summary>
/// <remarks>
/// <para>Everything here is a <b>web</b> concern — how a language is spelled in a path and which
/// languages search engines may index. The languages themselves, the translations and the
/// active-culture state stay in <c>Zonit.Extensions.Cultures</c>, which knows nothing about HTTP
/// and is shared with console hosts and workers. This type reads that allow-list; it never
/// redefines it.</para>
///
/// <para>The defaults change nothing: <see cref="Strategy"/> is
/// <see cref="CultureUrlStrategy.None"/>, so mounting a Site keeps the URLs it has today and a
/// panel or single-language site never pays for machinery it does not use.</para>
/// </remarks>
public sealed class SiteCultureOptions
{
    /// <summary>
    /// Whether the culture leads the path. <see cref="CultureUrlStrategy.None"/> by default —
    /// see <see cref="CultureUrlStrategy"/> for why this is a per-mount decision.
    /// </summary>
    public CultureUrlStrategy Strategy { get; set; } = CultureUrlStrategy.None;

    /// <summary>
    /// Canonical spelling of the culture segment. Ignored unless <see cref="Strategy"/> is
    /// <see cref="CultureUrlStrategy.Prefix"/>.
    /// </summary>
    public CultureUrlFormat Format { get; set; } = CultureUrlFormat.Short;
}
