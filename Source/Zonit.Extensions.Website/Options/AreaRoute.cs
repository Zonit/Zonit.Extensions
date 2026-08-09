namespace Zonit.Extensions.Website;

/// <summary>
/// Declares something about one of an area's routes that the <c>@page</c> template cannot express.
/// </summary>
/// <remarks>
/// <para>Today that is per-culture paths. The type exists as a route <em>descriptor</em> rather
/// than a bare culture map so the next thing a route needs to declare — a redirect from a retired
/// address, a crawl priority, a cache profile — is a property here instead of a second parallel
/// collection on <see cref="IWebsiteArea"/> that every area has to remember to fill.</para>
///
/// <para><b>Declared by the area that owns the route.</b> A route belongs to whoever defines it,
/// and a Site-level table would force every host to restate the translations of every plug-in it
/// mounts. The failure mode of getting that wrong is silent: the page still routes under its
/// canonical path, so nothing errors — the translated URL simply 404s and the <c>hreflang</c>
/// pair never appears.</para>
///
/// <code>
/// public IReadOnlyList&lt;AreaRoute&gt; Routes =>
/// [
///     AreaRoute.Localize("/news/{slug}",
///         ("pl-pl", "/aktualnosci/{slug}"),
///         ("de-de", "/nachrichten/{slug}")),
/// ];
/// </code>
/// </remarks>
/// <param name="Template">
/// The canonical route exactly as written in <c>@page</c> — e.g. <c>"/news/{slug}"</c>. This is
/// the path the Blazor router sees after the middleware rewrite, and the path used for any culture
/// that declares no override.
/// </param>
/// <param name="Localized">
/// Per-culture replacements keyed by BCP-47 tag, matched case-insensitively.
/// <see langword="null"/> or empty means the route reads the same in every language.
/// </param>
public sealed record AreaRoute(
    string Template,
    IReadOnlyDictionary<string, string>? Localized = null)
{
    /// <summary>
    /// Declares a route whose <b>path</b> differs per language while the component keeps a single
    /// <c>@page</c> template.
    /// </summary>
    /// <remarks>
    /// <para>Only the static head of each template is mapped: <c>/aktualnosci/{slug}</c>
    /// contributes the prefix <c>/aktualnosci</c>, and everything after it rides along untouched.
    /// A translated <em>slug</em> is a different problem — it lives in the content store, so the
    /// page supplies it through <c>PageMeta.Alternates</c>.</para>
    ///
    /// <para>Cultures outside the Site's supported set are ignored rather than throwing: the
    /// allow-list is the single gate, and a route table permitted to disagree with it would be a
    /// second source of truth that fails silently.</para>
    /// </remarks>
    /// <param name="template">The canonical route, exactly as written in <c>@page</c>.</param>
    /// <param name="paths">Per-culture replacements. Cultures not listed keep the template.</param>
    public static AreaRoute Localize(
        string template,
        params ReadOnlySpan<(string Culture, string Path)> paths)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template);

        var map = new Dictionary<string, string>(paths.Length, StringComparer.OrdinalIgnoreCase);
        foreach (var (culture, path) in paths)
            map[culture] = path;

        return new AreaRoute(template, map);
    }
}
