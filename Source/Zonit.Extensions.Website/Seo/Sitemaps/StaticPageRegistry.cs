using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;

namespace Zonit.Extensions.Website.Sitemaps;

/// <summary>
/// One page declared with <c>[Sitemap]</c> and/or <c>[Llms]</c>, as the generator emitted it.
/// </summary>
/// <param name="Path">Site-relative path, no culture segment, no mount base.</param>
/// <param name="InSitemap">Whether <c>[Sitemap]</c> was present.</param>
/// <param name="Change">Change frequency for the sitemap.</param>
/// <param name="Priority">Sitemap priority, or <see langword="null"/>.</param>
/// <param name="LlmsDescription">Description for <c>llms.txt</c>, or <see langword="null"/>.</param>
/// <param name="LlmsTitle">Title for <c>llms.txt</c>. Falls back to <paramref name="Path"/>.</param>
/// <param name="LlmsSection">Section heading for <c>llms.txt</c>. Falls back to <c>Resources</c>.</param>
public readonly record struct StaticPage(
    string Path,
    bool InSitemap,
    ChangeFrequency Change = ChangeFrequency.Unset,
    double? Priority = null,
    string? LlmsDescription = null,
    string? LlmsTitle = null,
    string? LlmsSection = null);

/// <summary>
/// Build-time page declarations, filled by the source generator through a module initializer.
/// </summary>
/// <remarks>
/// <para>Static rather than a DI service because a <c>[ModuleInitializer]</c> runs before any
/// container exists — the same mechanism, and the same reason, as
/// <c>ViewModelMetadataRegistry</c>. Registration happens once per assembly when the assembly is
/// loaded; nothing scans, reflects over types or allocates per request.</para>
///
/// <para>Entries are keyed by the declaring assembly so a Site publishes only the pages of the
/// areas it actually mounts. Without that, a host running a public site and an admin panel from
/// one process would put every assembly's pages into both sitemaps.</para>
/// </remarks>
public static class StaticPageRegistry
{
    private static readonly ConcurrentDictionary<Assembly, StaticPage[]> Pages = new();

    /// <summary>
    /// Registers an assembly's declarations. Called by generated code; last writer wins, so a
    /// hot-reloaded assembly replaces rather than duplicates its entries.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void Register(Assembly assembly, StaticPage[] pages)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(pages);
        Pages[assembly] = pages;
    }

    /// <summary>Declarations contributed by the given assemblies, in registration order.</summary>
    public static IEnumerable<StaticPage> For(IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        foreach (var assembly in assemblies.Distinct())
        {
            if (!Pages.TryGetValue(assembly, out var pages))
                continue;

            foreach (var page in pages)
                yield return page;
        }
    }

    /// <summary>Every registered declaration, whichever assembly it came from.</summary>
    public static IEnumerable<StaticPage> All() => Pages.Values.SelectMany(static p => p);
}
