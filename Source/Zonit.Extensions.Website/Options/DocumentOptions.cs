using Microsoft.AspNetCore.Components;
using System.Diagnostics.CodeAnalysis;

namespace Zonit.Extensions.Website;

/// <summary>Kind of asset reference emitted into the document.</summary>
public enum DocumentAssetKind
{
    /// <summary><c>&lt;link rel="stylesheet"&gt;</c> in the head.</summary>
    Stylesheet = 0,

    /// <summary><c>&lt;script&gt;</c> at the end of the body.</summary>
    Script = 1,
}

/// <param name="Kind">Stylesheet or script.</param>
/// <param name="Url">
/// Path or absolute URL. A relative path resolves against <c>&lt;base href&gt;</c>, which is what
/// keeps assets working under a non-root mount.
/// </param>
/// <param name="Fingerprint">
/// Whether to resolve the URL through the static-asset manifest for cache-busting. Absolute URLs
/// are never fingerprinted — the manifest only knows this application's own files.
/// </param>
/// <param name="Defer">Emits <c>defer</c> on a script.</param>
/// <param name="Async">Emits <c>async</c> on a script.</param>
public readonly record struct DocumentAsset(
    DocumentAssetKind Kind,
    string Url,
    bool Fingerprint = true,
    bool Defer = false,
    bool Async = false);

/// <param name="Name">Value for the <c>name</c> attribute, or the <c>property</c> attribute when <paramref name="IsProperty"/>.</param>
/// <param name="Content">Value for the <c>content</c> attribute.</param>
/// <param name="IsProperty">Emit <c>property=</c> instead of <c>name=</c> — the Open Graph convention.</param>
public readonly record struct DocumentMeta(string Name, string Content, bool IsProperty = false);

/// <param name="Origin">Absolute origin to open an early connection to.</param>
/// <param name="CrossOrigin">Emits <c>crossorigin</c> — required for font origins, harmful elsewhere.</param>
public readonly record struct DocumentPreconnect(string Origin, bool CrossOrigin = false);

/// <summary>
/// Declarative contents of a Site's document shell — the fonts, stylesheets, scripts, meta tags
/// and components that <c>AppBase</c> renders around the routed page.
/// </summary>
/// <remarks>
/// <para><b>Why this exists.</b> A hand-written <c>App.razor</c> is the file every project copies
/// once and then never revisits, and it is where the subtle mistakes accumulate: a
/// <c>&lt;base href&gt;</c> hard-coded to <c>/</c> that breaks the moment the Site is mounted
/// elsewhere, a stylesheet linked without fingerprinting so visitors keep a stale copy, a missing
/// scoped-CSS bundle that silently disables every <c>*.razor.css</c> in the application. None of
/// those announce themselves. Declaring the contents instead of the document lets the framework
/// get the structure right once.</para>
///
/// <para><b>Three layers, in increasing power.</b> Most needs are covered by the declarative
/// methods here. Anything that needs Razor — an analytics snippet, a consent banner, a
/// reconnection indicator — is a component registered through <see cref="AddHeadComponent{T}"/>
/// or <see cref="AddBodyEndComponent{T}"/>. Anything beyond that overrides a virtual on
/// <c>AppBase</c>, or abandons it entirely and passes its own root component to
/// <c>UseWebsite&lt;TApp&gt;</c>, exactly as before.</para>
///
/// <para>Nothing here emits a framework or product name into the markup. What ends up in the
/// document is what the Site declared, plus structural tags that name no library.</para>
/// </remarks>
public sealed class DocumentOptions
{
    private readonly List<RenderFragment> _headComponents = [];
    private readonly List<RenderFragment> _bodyEndComponents = [];

    /// <summary>Favicon URL. <see langword="null"/> emits no icon link.</summary>
    /// <remarks>
    /// Left unset by default rather than pointed at a conventional path: linking a favicon that
    /// does not exist costs every visitor a 404 on every page, and a default cannot know whether
    /// the file is there.
    /// </remarks>
    public string? Favicon { get; set; }

    /// <summary>MIME type of <see cref="Favicon"/>, e.g. <c>"image/svg+xml"</c>.</summary>
    public string? FaviconType { get; set; }

    /// <summary>
    /// Emits <c>&lt;link rel="stylesheet"&gt;</c> for the host application's scoped-CSS bundle
    /// (<c>{ApplicationName}.styles.css</c>), when the build produced one.
    /// </summary>
    /// <remarks>
    /// On by default, and worth leaving on. Every <c>*.razor.css</c> in the application and in
    /// every referenced library compiles into that one file; a document that does not link it
    /// silently drops every scoped style in the project. The failure is invisible — components
    /// still render, Blazor still stamps its scope attributes, nothing 404s, nothing logs. It
    /// simply looks like the styling regressed. The link is skipped automatically when no bundle
    /// exists, so a project without scoped CSS pays nothing.
    /// </remarks>
    public bool ScopedStyles { get; set; } = true;

    /// <summary>Emits Blazor's <c>&lt;ImportMap /&gt;</c>. Required for JS module imports.</summary>
    public bool ImportMap { get; set; } = true;

    /// <summary>
    /// Layout key applied to pages that declare none — resolved through <c>ILayoutRegistry</c>,
    /// so it must have been registered with <c>services.AddWebsiteLayout&lt;T&gt;("key")</c>.
    /// <see langword="null"/> leaves pages to Blazor's own <c>[Layout]</c> handling.
    /// </summary>
    public string? DefaultLayoutKey { get; set; }

    /// <summary>Stylesheets and scripts, in registration order.</summary>
    public List<DocumentAsset> Assets { get; set; } = [];

    /// <summary>Additional meta tags.</summary>
    public List<DocumentMeta> Metas { get; set; } = [];

    /// <summary>Origins to preconnect to.</summary>
    public List<DocumentPreconnect> Preconnects { get; set; } = [];

    /// <summary>Components rendered at the end of the head, in registration order.</summary>
    /// <remarks>
    /// Stored as fragments rather than <see cref="Type"/> values. A registered generic
    /// instantiation keeps the component and its parameters rooted for the trimmer, where a
    /// <c>Type</c> forwarded into <c>OpenComponent(int, Type)</c> loses that guarantee and has
    /// to be suppressed to compile clean.
    /// </remarks>
    public IReadOnlyList<RenderFragment> HeadComponents => _headComponents;

    /// <summary>Components rendered at the end of the body, in registration order.</summary>
    public IReadOnlyList<RenderFragment> BodyEndComponents => _bodyEndComponents;

    /// <summary>Adds a stylesheet to the head.</summary>
    /// <param name="url">Relative path (resolved against <c>&lt;base href&gt;</c>) or absolute URL.</param>
    /// <param name="fingerprint">
    /// Resolve through the static-asset manifest for cache-busting. Leave on for your own files;
    /// it is ignored for absolute URLs and is a no-op for paths the manifest does not know.
    /// </param>
    public DocumentOptions AddStylesheet(string url, bool fingerprint = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        Assets.Add(new DocumentAsset(DocumentAssetKind.Stylesheet, url, fingerprint));
        return this;
    }

    /// <summary>Adds a script at the end of the body.</summary>
    /// <param name="url">Relative path (resolved against <c>&lt;base href&gt;</c>) or absolute URL.</param>
    /// <param name="async">Emits <c>async</c>.</param>
    /// <param name="fingerprint">Resolve through the static-asset manifest for cache-busting.</param>
    /// <param name="defer">
    /// Emits <c>defer</c>. Harmless and usually correct, but note that a script at the end of the
    /// body is already past the parser — reach for it when the script is also referenced from
    /// the head, or when ordering against other deferred scripts matters.
    /// </param>
    public DocumentOptions AddScript(string url, bool defer = false, bool async = false, bool fingerprint = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        Assets.Add(new DocumentAsset(DocumentAssetKind.Script, url, fingerprint, defer, async));
        return this;
    }

    /// <summary>Adds a <c>&lt;meta name="…" content="…"&gt;</c> tag.</summary>
    /// <remarks>
    /// For fixed, Site-wide values — <c>theme-color</c>, a verification token. Per-page metadata
    /// (title, description, canonical, Open Graph) belongs to <see cref="PageMeta"/> and is
    /// emitted by the page, not here.
    /// </remarks>
    public DocumentOptions AddMeta(string name, string content, bool isProperty = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Metas.Add(new DocumentMeta(name, content, isProperty));
        return this;
    }

    /// <summary>Adds a <c>&lt;link rel="preconnect"&gt;</c>.</summary>
    /// <param name="origin">Absolute origin to open an early connection to.</param>
    /// <param name="crossOrigin">
    /// Required for font origins, which fetch anonymously — without it the browser opens a second
    /// connection and the preconnect was wasted.
    /// </param>
    public DocumentOptions AddPreconnect(string origin, bool crossOrigin = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(origin);
        Preconnects.Add(new DocumentPreconnect(origin, crossOrigin));
        return this;
    }

    /// <summary>Sets the favicon.</summary>
    public DocumentOptions SetFavicon(string url, string? type = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        Favicon = url;
        FaviconType = type;
        return this;
    }

    /// <summary>Renders <typeparamref name="T"/> at the end of the head.</summary>
    /// <remarks>
    /// The typed escape hatch for anything a link or meta tag cannot express — an analytics
    /// snippet, a structured-data block, a consent script. A component rather than a markup
    /// string, so it can inject services, read the tenant and be tested.
    /// </remarks>
    public DocumentOptions AddHeadComponent<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>() where T : IComponent
    {
        _headComponents.Add(Fragment<T>());
        return this;
    }

    /// <summary>Renders <typeparamref name="T"/> at the end of the body, after the framework scripts.</summary>
    public DocumentOptions AddBodyEndComponent<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>() where T : IComponent
    {
        _bodyEndComponents.Add(Fragment<T>());
        return this;
    }

    /// <summary>Adds a pre-built fragment to the head — for content assembled elsewhere.</summary>
    public DocumentOptions AddHeadContent(RenderFragment fragment)
    {
        ArgumentNullException.ThrowIfNull(fragment);
        _headComponents.Add(fragment);
        return this;
    }

    /// <summary>Adds a pre-built fragment to the end of the body.</summary>
    public DocumentOptions AddBodyEndContent(RenderFragment fragment)
    {
        ArgumentNullException.ThrowIfNull(fragment);
        _bodyEndComponents.Add(fragment);
        return this;
    }

    private static RenderFragment Fragment<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>() where T : IComponent
        => builder =>
        {
            builder.OpenComponent<T>(0);
            builder.CloseComponent();
        };

    /// <summary>
    /// Deep copy of the declarative parts, with the registered components carried across by
    /// reference.
    /// </summary>
    /// <remarks>
    /// Components cannot be named in JSON, so a configuration reload has nothing to say about
    /// them; copying the same fragments forward is what keeps an analytics snippet registered in
    /// code from vanishing the first time an operator edits an unrelated key.
    /// </remarks>
    internal DocumentOptions Clone()
    {
        var clone = new DocumentOptions
        {
            Favicon = Favicon,
            FaviconType = FaviconType,
            ScopedStyles = ScopedStyles,
            ImportMap = ImportMap,
            DefaultLayoutKey = DefaultLayoutKey,
            Assets = [.. Assets],
            Metas = [.. Metas],
            Preconnects = [.. Preconnects],
        };

        clone._headComponents.AddRange(_headComponents);
        clone._bodyEndComponents.AddRange(_bodyEndComponents);
        return clone;
    }
}
