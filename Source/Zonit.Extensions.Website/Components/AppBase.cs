using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using Zonit.Extensions.Cultures;
using Zonit.Extensions.Tenants;
using Zonit.Extensions.Website.Hydration;

namespace Zonit.Extensions.Website;

/// <summary>
/// Complete, extensible document shell — the root component a Site is mounted with. Emits the
/// whole HTML document and renders the routed page inside it.
/// </summary>
/// <remarks>
/// <para><b>What it gets right so no project has to.</b> The <c>lang</c> attribute follows the
/// active culture. <c>&lt;base href&gt;</c> follows the request's path base, so it is correct
/// under a non-root mount <em>and</em> carries the culture segment — which is what makes every
/// relative link keep the language without a single route template mentioning it. The
/// scoped-CSS bundle is discovered and linked, or skipped when the build produced none. The
/// theme is applied before the first paint. The state bridge is rendered when, and only when,
/// there is an interactive pass for it to bridge to. <c>HeadOutlet</c> is present, so the page's
/// own title, canonical and <c>hreflang</c> land where they belong.</para>
///
/// <para><b>How to extend it.</b> In increasing order of power:</para>
/// <list type="number">
///   <item>Declare contents on <c>SiteOptions.Document</c> — stylesheets, scripts, fonts, meta
///         tags, favicon. Covers most of what a hand-written shell contains.</item>
///   <item>Register components into the head or the end of the body
///         (<c>Document.AddHeadComponent&lt;T&gt;()</c>) for anything that needs Razor or
///         services.</item>
///   <item>Derive and override a virtual here — <see cref="BodyStart"/>,
///         <see cref="BuildRoutes"/>, <see cref="PageRenderMode"/>.</item>
///   <item>Ignore this class entirely and pass your own root component to
///         <c>UseWebsite&lt;TApp&gt;</c>, exactly as before. Nothing here is mandatory.</item>
/// </list>
///
/// <code>
/// public sealed class PublicApp : AppBase { }
///
/// app.UseWebsite&lt;PublicApp&gt;("/", o =>
/// {
///     o.Mode = WebsiteMode.Static;
///     o.Cultures.Strategy = CultureUrlStrategy.Prefix;
///     o.Document
///         .AddPreconnect("https://fonts.gstatic.com", crossOrigin: true)
///         .AddStylesheet("_content/Acme.Web/app.css")
///         .SetFavicon("favicon.svg", "image/svg+xml");
/// });
/// </code>
///
/// <para><b>No framework fingerprint.</b> The emitted document names no library and no product:
/// the structural tags are generic, the theme cookie and its global are configurable with
/// neutral defaults, and every asset URL is one the Site declared. An attacker reading the
/// markup learns what the site chose to say, not which open-source stack to look up advisories
/// against.</para>
/// </remarks>
public abstract class AppBase : ComponentBase
{
    [Inject] private ICurrentSite Site { get; set; } = default!;
    [Inject] private ICultureState Culture { get; set; } = default!;
    [Inject] private IHttpContextAccessor Http { get; set; } = default!;
    [Inject] private IHostEnvironment Environment { get; set; } = default!;
    [Inject] private ITenantProvider Tenant { get; set; } = default!;

    /// <summary>
    /// Optional — a host that wired the Website kernel without logging still renders.
    /// </summary>
    [Inject] private ILogger<AppBase>? Logger { get; set; }

    /// <summary>Document contents declared on the Site. Override to substitute a different set.</summary>
    protected virtual DocumentOptions Document => Site.Document;

    /// <summary>
    /// Value of the <c>lang</c> attribute on <c>&lt;html&gt;</c>. Defaults to the active culture.
    /// </summary>
    protected virtual string Lang => Culture.Current.ValueOrDefault ?? "en";

    /// <summary>
    /// Value of <c>&lt;base href&gt;</c>, always with a trailing slash.
    /// </summary>
    /// <remarks>
    /// <para>Taken from the live <c>Request.PathBase</c>, which by this point includes both the
    /// mount and the culture segment — <c>/admin/</c>, <c>/pl/</c>, <c>/admin/pl/</c>. That is the
    /// whole mechanism behind prefixed URLs: relative hrefs resolve against it, so a link renders
    /// as <c>pricing</c> and lands on <c>/pl/pricing</c> without the route template knowing.</para>
    ///
    /// <para>The trailing slash is not cosmetic. A browser treats a base without one as a file
    /// path, so <c>/admin</c> would resolve <c>pricing</c> to <c>/pricing</c> — the right route
    /// on the wrong mount.</para>
    /// </remarks>
    protected virtual string BaseHref
    {
        get
        {
            var pathBase = Http.HttpContext?.Request.PathBase.Value;
            if (string.IsNullOrEmpty(pathBase))
                return "/";

            return pathBase.EndsWith('/') ? pathBase : pathBase + "/";
        }
    }

    /// <summary>
    /// Path base every framework-emitted asset URL is rooted at — the Site's mount, and
    /// deliberately <em>not</em> the culture segment.
    /// </summary>
    /// <remarks>
    /// <para><b>Why assets do not follow <see cref="BaseHref"/>.</b> A relative
    /// <c>src="_content/acme/app.css"</c> resolves against <c>&lt;base href="/pl/"&gt;</c>, so the
    /// browser fetches <c>/pl/_content/acme/app.css</c> — and every other language fetches its own
    /// spelling of the same bytes. That is right for links, where the prefix is the whole point,
    /// and wrong for files: one stylesheet acquires as many URLs as the Site has languages, each
    /// its own cache entry in every intermediary, and an image indexable at twenty addresses. The
    /// culture belongs to what a page <em>says</em>, not to the files it loads.</para>
    ///
    /// <para><b>Why the mount is kept.</b> <c>MapStaticAssets</c> is registered inside the Site's
    /// branch, not on the parent app, so a Site mounted at <c>/admin</c> genuinely serves its
    /// assets from <c>/admin/_content/…</c>; a bare <c>/_content/…</c> would leave the branch and
    /// 404 wherever no root Site exists. The value comes from
    /// <see cref="Cultures.ICultureUrlFeature.SitePathBase"/>, which records the path base from
    /// before the culture segment was appended — the mount exactly, whatever the language.</para>
    /// </remarks>
    protected virtual string AssetBase
        => AssetBaseResolver.Resolve(Http.HttpContext, navigation: null, Site.UrlPolicy);

    /// <summary>
    /// Roots a framework-emitted asset URL at <see cref="AssetBase"/>, leaving anything that is
    /// already absolute alone.
    /// </summary>
    /// <remarks>
    /// An author who wrote a leading slash asked for a host-absolute URL and gets one — including
    /// the consequence that it bypasses the mount. Off-site URLs, protocol-relative URLs and
    /// inline data are returned untouched.
    /// </remarks>
    private string Root(string? url) => AssetBaseResolver.Root(url, AssetBase);

    /// <summary>
    /// The import map with every entry rooted at <see cref="AssetBase"/>.
    /// </summary>
    /// <remarks>
    /// Blazor builds the map from the resource collection with <c>./</c>-relative specifiers, and
    /// an import map resolves against the document base URL exactly like an <c>src</c> does — so
    /// on a prefixed Site every JS module was imported from <c>/pl/_content/…</c>, a second copy of
    /// a module the browser had already fetched under another language. Rewriting the map here is
    /// the only place to fix it: the specifier a module is imported under is the map's key, so a
    /// consumer cannot correct it at the call site.
    /// </remarks>
    protected virtual ImportMapDefinition RootedImportMap()
    {
        // Memoised per mount, not per request. The resource collection is fixed for the process,
        // so the rewrite has exactly one answer per Site — and the shell renders on every page
        // view, which is the wrong place to rebuild three dictionaries.
        //
        // An empty base is NOT a shortcut: "./_content/x.js" against <base href="/pl/"> is the bug
        // being fixed, and rooting it to "/_content/x.js" is the whole point.
        return RootedImportMaps.GetOrAdd(AssetBase, static (@base, assets) =>
        {
            var map = ImportMapDefinition.FromResourceCollection(assets);

            return new ImportMapDefinition(
                RewriteImports(map.Imports, @base),
                map.Scopes?.ToDictionary(
                    scope => RootSpecifier(scope.Key, @base),
                    scope => (IReadOnlyDictionary<string, string>)Rewrite(scope.Value, @base)!,
                    StringComparer.Ordinal),
                Rewrite(map.Integrity, @base));
        }, Assets);
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ImportMapDefinition> RootedImportMaps =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Rewrites the import specifiers, keeping the original <c>./</c> spelling as a second key
    /// pointing at the same rooted URL.
    /// </summary>
    /// <remarks>
    /// The rooted key is what our own emission uses. The <c>./</c> key is for everyone else:
    /// libraries commonly load their module with
    /// <c>JS.InvokeAsync&lt;IJSObjectReference&gt;("import", "./_content/Lib/lib.js")</c>, and that
    /// specifier resolves against the document base — <c>/pl/_content/Lib/lib.js</c> on a prefixed
    /// Site. Dropping the relative key would leave that import unmatched, so it would lose its
    /// fingerprint and go looking for a URL under the language segment. Both keys map to the same
    /// value, so either spelling fetches the one real file.
    /// </remarks>
    private static Dictionary<string, string> RewriteImports(IReadOnlyDictionary<string, string>? imports, string @base)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (imports is null)
            return result;

        foreach (var (key, value) in imports)
        {
            var rooted = RootSpecifier(value, @base);
            result[RootSpecifier(key, @base)] = rooted;

            if (key.StartsWith("./", StringComparison.Ordinal))
                result[key] = rooted;
        }

        return result;
    }

    private static Dictionary<string, string>? Rewrite(IReadOnlyDictionary<string, string>? entries, string @base)
        => entries?.ToDictionary(
            entry => RootSpecifier(entry.Key, @base),
            entry => RootSpecifier(entry.Value, @base),
            StringComparer.Ordinal);

    /// <summary>
    /// Turns a <c>./relative</c> import specifier into one rooted at the mount. Absolute and
    /// off-site specifiers are left alone; so are bare module names, which are not URLs at all.
    /// </summary>
    private static string RootSpecifier(string specifier, string @base)
    {
        if (specifier.StartsWith("./", StringComparison.Ordinal))
            return @base + specifier[1..];

        if (specifier.Length == 0 || specifier[0] == '/' || specifier.Contains("://", StringComparison.Ordinal))
            return specifier;

        return @base + "/" + specifier;
    }

    /// <summary>
    /// Render mode applied to the routed page, the head outlet and the state bridge. Derived from
    /// <c>SiteOptions.Mode</c>; <see langword="null"/> means static server rendering.
    /// </summary>
    protected virtual IComponentRenderMode? PageRenderMode => Site.Mode switch
    {
        WebsiteMode.Static => null,
        WebsiteMode.WebAssembly => RenderMode.InteractiveWebAssembly,
        WebsiteMode.Auto => RenderMode.InteractiveAuto,
        _ => RenderMode.InteractiveServer,
    };

    /// <summary>
    /// Renders the router. Defaults to <see cref="WebsiteRoutes"/>; override to substitute a
    /// hand-written one.
    /// </summary>
    /// <remarks>
    /// <para>A method rather than a <see cref="Type"/> property. Forwarding a type into
    /// <c>OpenComponent(int, Type)</c> makes the component and every one of its parameters
    /// reflection-visible to the trimmer, which then cannot prove any of them are kept; a
    /// generic instantiation roots exactly what is used and nothing more. It also has to be a
    /// method because <paramref name="renderMode"/> must be applied to the router component
    /// itself — that is the boundary the interactive pass forms around, and a fragment handed to
    /// <c>AddContent</c> has nowhere to put it.</para>
    /// </remarks>
    /// <param name="builder">Tree being built; the override owns sequence numbers from 0.</param>
    /// <param name="renderMode">
    /// The Site's render mode, or <see langword="null"/> for static server rendering. Apply it to
    /// the routed component with <c>AddComponentRenderMode</c>.
    /// </param>
    protected virtual void BuildRoutes(RenderTreeBuilder builder, IComponentRenderMode? renderMode)
    {
        builder.OpenComponent<WebsiteRoutes>(0);
        if (renderMode is not null)
            builder.AddComponentRenderMode(renderMode);
        builder.CloseComponent();
    }

    /// <summary>
    /// Whether to render the prerender-to-circuit state bridge.
    /// </summary>
    /// <remarks>
    /// Only meaningful when there is an interactive pass. Rendering it on a statically rendered
    /// Site would open a circuit on every page view for a handover that never happens — the cost
    /// with none of the benefit. On an interactive Site, omitting it is the classic "SSR renders
    /// signed in, then the interactive pass renders anonymous" bug.
    /// </remarks>
    protected virtual bool Hydrate => PageRenderMode is not null;

    /// <summary>Rendered at the very start of the head, before anything the framework emits.</summary>
    protected virtual RenderFragment? HeadStart => null;

    /// <summary>Rendered at the end of the head, after the declared assets and before <c>HeadOutlet</c>.</summary>
    protected virtual RenderFragment? HeadEnd => null;

    /// <summary>Rendered at the start of the body, before the router.</summary>
    protected virtual RenderFragment? BodyStart => null;

    /// <summary>Rendered at the end of the body, after the framework script and declared scripts.</summary>
    protected virtual RenderFragment? BodyEnd => null;

    /// <summary>
    /// Host application's scoped-CSS bundle, or <see langword="null"/> when the build produced
    /// none.
    /// </summary>
    /// <remarks>
    /// <para>The name is derived from the entry assembly rather than configured, because a setting
    /// nobody knows they need is the same as no setting at all — and the symptom of getting it
    /// wrong (every <c>*.razor.css</c> in the project silently stops applying) points nowhere
    /// near the cause. <c>Assets[]</c> returns the key unchanged for anything absent from the
    /// manifest and a fingerprinted path for anything present, so "unchanged" is a reliable
    /// "this file does not exist" — without it the Site pays a 404 on every page load.</para>
    ///
    /// <para>The derivation holds only while the bundle keeps its default name. The Razor SDK
    /// emits it as <c>$(PackageId).styles.css</c>, so a host that renames <c>PackageId</c> to keep
    /// its assembly name out of a public URL breaks the derivation and hits exactly the silent
    /// failure above. <see cref="DocumentOptions.ScopedCssBundleName"/> states the real name for
    /// that case; because it is explicit, a miss there is always a mistake and is logged.</para>
    /// </remarks>
    protected string? ScopedCssBundle
    {
        get
        {
            // Through the virtual, not Site.Document — a subclass that substitutes the document
            // substitutes this too, and the bundle it names is the one that gets linked.
            var configured = Document.ScopedCssBundleName;
            var name = configured ?? $"{Environment.ApplicationName}.styles.css";
            var resolved = Assets[name];

            if (resolved != name)
                return resolved;

            if (configured is not null && WarnedAssets.TryAdd(configured, 0))
            {
                Logger?.LogWarning(
                    "Document.ScopedCssBundleName is '{Bundle}', which the static-asset manifest " +
                    "does not contain, so no scoped-CSS bundle is linked and every *.razor.css in " +
                    "the application is inert. The bundle is emitted as $(PackageId).styles.css — " +
                    "check that this name matches the host project's PackageId.",
                    configured);
            }

            return null;
        }
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var document = Document;
        var renderMode = PageRenderMode;

        builder.AddMarkupContent(0, "<!DOCTYPE html>\n");

        builder.OpenElement(1, "html");
        builder.AddAttribute(2, "lang", Lang);

        builder.OpenElement(3, "head");
        BuildHead(builder, document, renderMode);
        builder.CloseElement();

        builder.OpenElement(4, "body");
        BuildBody(builder, document, renderMode);
        builder.CloseElement();

        builder.CloseElement();
    }

    private void BuildHead(RenderTreeBuilder builder, DocumentOptions document, IComponentRenderMode? renderMode)
    {
        builder.AddContent(0, HeadStart);

        builder.AddMarkupContent(1, "<meta charset=\"utf-8\" />");
        builder.AddMarkupContent(2, "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />");

        builder.OpenElement(3, "base");
        builder.AddAttribute(4, "href", BaseHref);
        builder.CloseElement();

        // The theme handshake goes as early as possible and blocks: anything after a stylesheet
        // paints the wrong scheme first, and a dark-mode visitor sees a white flash on every
        // navigation. It is a few hundred bytes and runs before the body exists.
        if (Site.Appearance.Enabled)
        {
            builder.OpenElement(5, "script");
            builder.AddMarkupContent(6, AppearanceScript.Build(Site.Appearance, Tenant.Settings.Theme.ColorScheme));
            builder.CloseElement();
        }

        BuildPreconnects(builder, document);
        BuildMetas(builder, document);
        BuildStylesheets(builder, document);
        BuildFavicon(builder, document);

        if (document.ImportMap)
        {
            builder.OpenComponent<ImportMap>(7);
            // "ImportMapDefinition" is the parameter name; the component itself is ImportMap.
            // Getting it wrong is silent — the value lands in AdditionalAttributes and is splatted
            // onto the <script> tag as markup while the default map still renders inside it.
            builder.AddAttribute(11, "ImportMapDefinition", RootedImportMap());
            builder.CloseComponent();
        }

        BuildComponents(builder, 8, document.HeadComponents);

        builder.AddContent(9, HeadEnd);

        // Last, so a consumer's own head content is already in the tree. HeadOutlet is what the
        // page's PageHead writes through — without it the title, canonical and hreflang are
        // computed and then dropped on the floor.
        builder.OpenComponent<HeadOutlet>(10);
        if (renderMode is not null)
            builder.AddComponentRenderMode(renderMode);
        builder.CloseComponent();
    }

    private void BuildBody(RenderTreeBuilder builder, DocumentOptions document, IComponentRenderMode? renderMode)
    {
        builder.AddContent(0, BodyStart);

        if (Hydrate)
        {
            builder.OpenComponent<WebsiteHydrator>(1);
            if (renderMode is not null)
                builder.AddComponentRenderMode(renderMode);
            builder.CloseComponent();
        }

        builder.OpenRegion(2);
        BuildRoutes(builder, renderMode);
        builder.CloseRegion();

        // Blazor's framework script is required even on a statically rendered Site: enhanced
        // navigation and streaming rendering both live in it, and without it every link becomes
        // a full page load.
        builder.OpenElement(3, "script");
        builder.AddAttribute(4, "src", Root(Assets["_framework/blazor.web.js"]));
        builder.CloseElement();

        BuildScripts(builder, document);
        BuildComponents(builder, 5, document.BodyEndComponents);

        builder.AddContent(6, BodyEnd);
    }

    // ── Declared-asset emission ───────────────────────────────────────────────────────────────
    //
    // ASP0006 wants literal sequence numbers, and it is right to for ordinary component markup:
    // computed numbers defeat Blazor's diffing. It does not apply here. These lists are
    // configuration, their length is unknown at compile time, and OpenRegion with a per-item
    // sequence is precisely the mechanism the framework provides for that case — a literal would
    // give every item the same number and collapse the region. It is also moot: the document
    // shell is the root static component of the response, rendered exactly once and never
    // diffed against a previous tree.
#pragma warning disable ASP0006

    private void BuildPreconnects(RenderTreeBuilder builder, DocumentOptions document)
    {
        var index = 0;
        foreach (var preconnect in document.Preconnects)
        {
            builder.OpenRegion(index++);
            builder.OpenElement(0, "link");
            builder.AddAttribute(1, "rel", "preconnect");
            builder.AddAttribute(2, "href", preconnect.Origin);
            if (preconnect.CrossOrigin)
                builder.AddAttribute(3, "crossorigin", string.Empty);
            builder.CloseElement();
            builder.CloseRegion();
        }
    }

    private void BuildMetas(RenderTreeBuilder builder, DocumentOptions document)
    {
        // theme-color from the tenant's brand colour, unless the Site declared one itself.
        //
        // Derived rather than configured because the answer already exists: a tenant that has
        // picked a primary colour has picked the colour a phone should tint its browser chrome
        // with, and asking for it twice is how the two drift apart. An explicit
        // Document.AddMeta("theme-color", …) still wins — a Site that genuinely wants a different
        // chrome colour from its brand colour says so and is believed.
        if (!document.Metas.Any(m => string.Equals(m.Name, "theme-color", StringComparison.OrdinalIgnoreCase)))
        {
            var brand = Tenant.Settings.Theme.PrimaryColor;
            if (!string.IsNullOrWhiteSpace(brand))
            {
                builder.OpenRegion(900);
                builder.OpenElement(0, "meta");
                builder.AddAttribute(1, "name", "theme-color");
                builder.AddAttribute(2, "content", brand);
                builder.CloseElement();
                builder.CloseRegion();
            }
        }

        var index = 1000;
        foreach (var meta in document.Metas)
        {
            builder.OpenRegion(index++);
            builder.OpenElement(0, "meta");
            builder.AddAttribute(1, meta.IsProperty ? "property" : "name", meta.Name);
            builder.AddAttribute(2, "content", meta.Content);
            builder.CloseElement();
            builder.CloseRegion();
        }
    }

    private void BuildStylesheets(RenderTreeBuilder builder, DocumentOptions document)
    {
        var index = 2000;
        foreach (var asset in document.Assets)
        {
            if (asset.Kind != DocumentAssetKind.Stylesheet)
                continue;

            builder.OpenRegion(index++);
            builder.OpenElement(0, "link");
            builder.AddAttribute(1, "rel", "stylesheet");
            builder.AddAttribute(2, "href", Resolve(asset));
            builder.CloseElement();
            builder.CloseRegion();
        }

        // Emitted last so host rules win over declared ones on equal specificity, which is what
        // a project overriding a library's chrome expects.
        if (document.ScopedStyles && ScopedCssBundle is { } bundle)
        {
            builder.OpenRegion(index);
            builder.OpenElement(0, "link");
            builder.AddAttribute(1, "rel", "stylesheet");
            builder.AddAttribute(2, "href", Root(bundle));
            builder.CloseElement();
            builder.CloseRegion();
        }
    }

    private void BuildFavicon(RenderTreeBuilder builder, DocumentOptions document)
    {
        if (document.Favicon is null)
            return;

        builder.OpenRegion(3000);
        builder.OpenElement(0, "link");
        builder.AddAttribute(1, "rel", "icon");
        if (document.FaviconType is not null)
            builder.AddAttribute(2, "type", document.FaviconType);
        builder.AddAttribute(3, "href", Root(document.Favicon));
        builder.CloseElement();
        builder.CloseRegion();
    }

    private void BuildScripts(RenderTreeBuilder builder, DocumentOptions document)
    {
        var index = 4000;
        foreach (var asset in document.Assets)
        {
            if (asset.Kind != DocumentAssetKind.Script)
                continue;

            builder.OpenRegion(index++);
            builder.OpenElement(0, "script");
            builder.AddAttribute(1, "src", Resolve(asset));
            if (asset.Defer)
                builder.AddAttribute(2, "defer", string.Empty);
            if (asset.Async)
                builder.AddAttribute(3, "async", string.Empty);
            builder.CloseElement();
            builder.CloseRegion();
        }
    }

    private static void BuildComponents(RenderTreeBuilder builder, int region, IReadOnlyList<RenderFragment> fragments)
    {
        if (fragments.Count == 0)
            return;

        builder.OpenRegion(region);
        var index = 0;
        foreach (var fragment in fragments)
        {
            builder.OpenRegion(index++);
            builder.AddContent(0, fragment);
            builder.CloseRegion();
        }
        builder.CloseRegion();
    }

#pragma warning restore ASP0006

    /// <summary>
    /// Runs a declared URL through the static-asset manifest when asked, leaving absolute URLs
    /// alone — the manifest only knows this application's own files, and handing it a CDN URL
    /// would just return it unchanged after a pointless lookup.
    /// </summary>
    /// <remarks>
    /// <para><b>Warns when a fingerprint was asked for and not obtained.</b> A key the manifest
    /// does not recognise comes back <em>verbatim</em> — no exception, no log, nothing in the
    /// build. So <c>AddStylesheet("app.css")</c> instead of
    /// <c>AddStylesheet("_content/Acme.Web/app.css")</c> produces a page that renders perfectly
    /// and has silently lost its cache-busting, which nobody discovers until a deployment ships
    /// a stylesheet every returning visitor keeps the old copy of. The lookup cannot tell that
    /// apart from a file legitimately served outside the manifest, so this is a warning and not
    /// an error — silence it with <c>fingerprint: false</c>, which says the omission is
    /// deliberate.</para>
    /// </remarks>
    private string Resolve(DocumentAsset asset)
    {
        if (!asset.Fingerprint || asset.Url.Contains("://", StringComparison.Ordinal) || asset.Url.StartsWith("//", StringComparison.Ordinal))
            return Root(asset.Url);

        var resolved = Assets[asset.Url];

        // One line per distinct URL per process — the shell renders on every request, and a
        // warning repeated per page view is a warning nobody reads.
        if (ReferenceEquals(resolved, asset.Url) || resolved == asset.Url)
        {
            if (WarnedAssets.TryAdd(asset.Url, 0))
            {
                Logger?.LogWarning(
                    "Static asset '{Url}' is not in the manifest, so it is served without a content " +
                    "fingerprint and browsers will cache a stale copy across deployments. Use the full " +
                    "key (\"_content/<AssemblyName>/{Url}\" for an RCL) or pass fingerprint: false if " +
                    "this file is served outside MapStaticAssets.",
                    asset.Url, asset.Url);
            }
        }

        return Root(resolved);
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> WarnedAssets = new(StringComparer.Ordinal);
}
