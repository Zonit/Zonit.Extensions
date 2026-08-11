using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Http;
using System.Text;
using System.Text.Encodings.Web;
using Zonit.Extensions.Cultures;
using Zonit.Extensions.Tenants;
using Zonit.Extensions.Website.Cultures;

namespace Zonit.Extensions.Website;

/// <summary>
/// Renders the routed page's document head — title, description, canonical, <c>hreflang</c>
/// cluster, robots directive and Open Graph — from <see cref="IPageMetaState"/>.
/// </summary>
/// <remarks>
/// <para>Rendered by <c>ZonitRouteView</c>, so it is present for every routed page without a
/// layout or a consumer having to place it. It emits through Blazor's <c>PageTitle</c> and
/// <c>HeadContent</c>, which means the host document needs a <c>&lt;HeadOutlet /&gt;</c> —
/// every Zonit document shell already has one.</para>
///
/// <para>Re-renders only when the composed document actually changes. That matters because
/// <see cref="PageMeta"/> is mutable and <c>PageBase</c> re-notifies after each render to catch
/// a title assigned after an <c>await</c>; without the comparison the pair would spin.</para>
///
/// <para><b>Why one markup fragment rather than element calls.</b> The tag list is genuinely
/// dynamic — one <c>hreflang</c> per indexed language — so a render-tree walk would need
/// computed sequence numbers, which defeats Blazor's diffing and trips <c>ASP0006</c>. The head
/// is emitted whole instead, built once per composition and encoded explicitly at every
/// interpolation. Every value that reaches it is already framework-derived (URLs the policy
/// built, tenant settings) but the encoder is unconditional: a page title is author input, and
/// an <c>og:title</c> that closes its own attribute is a scripting bug.</para>
/// </remarks>
public sealed class PageHead : ComponentBase, IDisposable
{
    [Inject] private IPageMetaState Meta { get; set; } = default!;
    [Inject] private ITenantProvider Tenant { get; set; } = default!;
    [Inject] private ICultureState Culture { get; set; } = default!;
    [Inject] private IHttpContextAccessor Http { get; set; } = default!;
    [Inject] private IBreadcrumbsProvider Breadcrumbs { get; set; } = default!;
    [Inject] private ICultureProvider Translator { get; set; } = default!;

    private SeoDocument? _document;
    private MarkupString _markup;
    private bool _subscribed;

    protected override void OnInitialized()
    {
        Meta.OnChange += HandleChanged;
        _subscribed = true;
        Refresh();
    }

    protected override void OnParametersSet() => Refresh();

    private void HandleChanged()
    {
        var next = Compose();
        if (next.Equals(_document))
            return;

        _document = next;
        _markup = new MarkupString(Render(next));
        _ = InvokeAsync(StateHasChanged);
    }

    private void Refresh()
    {
        var next = Compose();
        if (next.Equals(_document))
            return;

        _document = next;
        _markup = new MarkupString(Render(next));
    }

    /// <summary>
    /// The routed page's type, supplied by <c>ZonitRouteView</c>. Carries the page's own
    /// <c>[Seo]</c> declaration and its authorization attributes.
    /// </summary>
    /// <remarks>
    /// The type rather than the component instance: the declaration is metadata, and reading it
    /// off the type is what lets the same answer be produced while assembling the sitemap, where
    /// there is nothing to render.
    /// </remarks>
    [Parameter] public Type? PageType { get; set; }

    private SeoDocument Compose() => SeoDocumentBuilder.Build(
        Meta.Current,
        Tenant.Settings.Site,
        Http.HttpContext?.Features.Get<ICultureUrlFeature>(),
        Culture.Current.ValueOrDefault ?? string.Empty,
        Tenant.Settings.SocialMedia,
        Breadcrumbs.Get(),
        s => Translator.Translate(s).Value,
        PageType is not null && PageIndexing.RequiresAuthorization(PageType),
        // The error page is a normal page that happens to be rendered for a failed address, and
        // nothing in its own declaration can know that. The pipeline does.
        Http.HttpContext is { } http && Middlewares.CultureMiddleware.IsReExecuting(http));

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var title = _document?.Title ?? string.Empty;
        var markup = _markup;

        builder.OpenComponent<PageTitle>(0);
        builder.AddComponentParameter(1, nameof(PageTitle.ChildContent),
            (RenderFragment)(b => b.AddContent(0, title)));
        builder.CloseComponent();

        builder.OpenComponent<HeadContent>(2);
        builder.AddComponentParameter(3, nameof(HeadContent.ChildContent),
            (RenderFragment)(b => b.AddMarkupContent(0, markup.Value)));
        builder.CloseComponent();
    }

    private static string Render(SeoDocument doc)
    {
        var html = new StringBuilder(256);
        var encoder = HtmlEncoder.Default;

        Named(html, encoder, "description", doc.Description);

        // Absent means "index, follow". Emitting that explicitly would be noise, and noise in
        // the head is how contradictory directives eventually get shipped.
        Named(html, encoder, "robots", doc.Robots);

        if (doc.Canonical is not null)
            html.Append("<link rel=\"canonical\" href=\"").Append(encoder.Encode(doc.Canonical)).Append("\" />");

        foreach (var alternate in doc.Alternates)
            Alternate(html, encoder, alternate.Hreflang, alternate.Url);

        if (doc.XDefault is not null)
            Alternate(html, encoder, "x-default", doc.XDefault);

        Property(html, encoder, "og:type", doc.Type);
        Property(html, encoder, "og:title", doc.Title);
        Property(html, encoder, "og:description", doc.Description);
        Property(html, encoder, "og:url", doc.Canonical);
        Property(html, encoder, "og:image", doc.Image);
        Property(html, encoder, "og:site_name", doc.SiteName);
        Property(html, encoder, "og:locale", doc.Locale);

        // summary_large_image whenever there is an image to show large, the bare card otherwise.
        // Getting this wrong renders a cropped thumbnail, not a broken page.
        Named(html, encoder, "twitter:card", doc.Image is null ? "summary" : "summary_large_image");

        // Appended raw: it is JSON, not HTML, and the writer has already neutralised the only
        // sequence that could close the script element early. Running it through the HTML encoder
        // here would turn every quote in the payload into &quot; and produce a block no crawler
        // can parse.
        if (doc.StructuredData is not null)
        {
            html.Append("<script type=\"application/ld+json\">")
                .Append(doc.StructuredData)
                .Append("</script>");
        }

        return html.ToString();
    }

    private static void Named(StringBuilder html, HtmlEncoder encoder, string name, string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return;

        html.Append("<meta name=\"").Append(name)
            .Append("\" content=\"").Append(encoder.Encode(content)).Append("\" />");
    }

    private static void Property(StringBuilder html, HtmlEncoder encoder, string property, string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return;

        html.Append("<meta property=\"").Append(property)
            .Append("\" content=\"").Append(encoder.Encode(content)).Append("\" />");
    }

    private static void Alternate(StringBuilder html, HtmlEncoder encoder, string hreflang, string href)
        => html.Append("<link rel=\"alternate\" hreflang=\"").Append(encoder.Encode(hreflang))
               .Append("\" href=\"").Append(encoder.Encode(href)).Append("\" />");

    public void Dispose()
    {
        if (!_subscribed)
            return;

        Meta.OnChange -= HandleChanged;
        _subscribed = false;
    }
}
