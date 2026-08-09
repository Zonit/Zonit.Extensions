using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using System.Collections.Immutable;
using Zonit.Extensions.Cultures;

namespace Zonit.Extensions.Website.Cultures;

/// <summary>
/// One entry in a language switcher.
/// </summary>
/// <param name="Culture">Canonical lowercase BCP-47 tag.</param>
/// <param name="Hreflang">
/// The tag in the spelling the URLs use — <c>pl</c> for a site serving <c>/pl/</c>, <c>pt-BR</c>
/// for one serving <c>/pt-br/</c>. Put it on the anchor's <c>hreflang</c> attribute.
/// </param>
/// <param name="NativeName">Endonym — <c>"polski (Polska)"</c>. What a speaker of that language expects to see.</param>
/// <param name="EnglishName">English name — <c>"Polish (Poland)"</c>. For a secondary label or a title attribute.</param>
/// <param name="Flag">Inline <c>&lt;svg&gt;</c> flag markup, or <see langword="null"/> when the language has none.</param>
/// <param name="Url">
/// Where the anchor should point: the current page in that language, path-absolute and including
/// the mount and the culture segment. Empty on a Site that does not encode culture in its URLs —
/// there is nowhere to navigate to, the switch happens in the scope.
/// </param>
/// <param name="IsCurrent">Whether this is the language currently being rendered.</param>
public readonly record struct CultureLink(
    string Culture,
    string Hreflang,
    string NativeName,
    string EnglishName,
    MarkupString? Flag,
    string Url,
    bool IsCurrent);

/// <summary>
/// Base class for a language switcher. Supplies the data and the behaviour; the markup is
/// entirely yours.
/// </summary>
/// <remarks>
/// <para>There is no ready-made switcher component in this framework on purpose. A panel wants a
/// dropdown, a marketing site wants a flag row in the footer, a documentation site wants a
/// modal — a component that tried to serve all three would be configured to death and still
/// fought. What is genuinely hard is not the markup: it is knowing the right URL per language on
/// a page whose route may be translated, and knowing whether clicking should navigate or just
/// change the scope. That is what this class answers.</para>
///
/// <code>
/// @inherits CultureSwitcherBase
///
/// &lt;ul class="lang-menu"&gt;
///   @foreach (var link in Links)
///   {
///     &lt;li&gt;
///       &lt;a href="@link.Url" hreflang="@link.Hreflang" lang="@link.Hreflang"
///          class="@(link.IsCurrent ? "is-current" : null)"
///          @onclick="() =&gt; SelectAsync(link)" @onclick:preventDefault="InterceptsClick"&gt;
///         @link.Flag @link.NativeName
///       &lt;/a&gt;
///     &lt;/li&gt;
///   }
/// &lt;/ul&gt;
/// </code>
///
/// <para><b>Always render a real anchor.</b> The <c>href</c> is the only way a crawler discovers
/// the other languages, and it is what makes the switcher work with JavaScript disabled and
/// during static rendering. The <c>@onclick</c> pair above is inert in both of those cases and
/// costs nothing.</para>
///
/// <para><b>When the click is intercepted.</b> Only on an interactive Site that does not encode
/// the culture in its URLs — a panel, where switching language should not throw away the circuit
/// and the URL has no language to update. Everywhere else the anchor navigates, which in a
/// Blazor Web App is already an enhanced navigation rather than a full reload, and leaves the
/// address bar telling the truth about what is on screen.</para>
/// </remarks>
public abstract class CultureSwitcherBase : ComponentBase, IDisposable
{
    [Inject] private ICultureState State { get; set; } = default!;
    [Inject] private ICultureManager Manager { get; set; } = default!;
    [Inject] private ILanguageProvider Languages { get; set; } = default!;
    [Inject] private Microsoft.Extensions.Options.IOptionsMonitor<Zonit.Extensions.Cultures.Options.CultureOption> Options { get; set; } = default!;
    [Inject] private IHttpContextAccessor Http { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private ImmutableArray<CultureLink> _links = [];
    private bool _subscribed;

    /// <summary>
    /// One entry per supported language, in configuration order, each carrying the URL of the
    /// current page in that language.
    /// </summary>
    /// <remarks>
    /// Every <em>supported</em> language, not only the indexed ones: withholding a language from
    /// search is not a reason to hide it from a visitor who speaks it.
    /// </remarks>
    protected IReadOnlyList<CultureLink> Links => _links;

    /// <summary>The language currently being rendered.</summary>
    protected CultureLink? CurrentLink
    {
        get
        {
            foreach (var link in _links)
                if (link.IsCurrent)
                    return link;
            return null;
        }
    }

    /// <summary>
    /// Whether <see cref="SelectAsync"/> changes the scope in place instead of navigating —
    /// bind it to <c>@onclick:preventDefault</c>.
    /// </summary>
    protected bool InterceptsClick
    {
        get
        {
            var feature = Http.HttpContext?.Features.Get<ICultureUrlFeature>();
            var prefixed = feature?.Policy.IsPrefixed ?? false;
            return RendererInfo.IsInteractive && !prefixed;
        }
    }

    /// <summary>
    /// Applies <paramref name="link"/>. Changes the scope's culture when the Site does not encode
    /// it in the URL; otherwise navigates, so that the address bar keeps matching the page.
    /// </summary>
    protected virtual Task SelectAsync(CultureLink link)
    {
        if (InterceptsClick)
        {
            Manager.SetCulture(link.Culture);
            return Task.CompletedTask;
        }

        if (!string.IsNullOrEmpty(link.Url))
            Navigation.NavigateTo(link.Url);

        return Task.CompletedTask;
    }

    protected override void OnInitialized()
    {
        State.OnChange += HandleCultureChanged;
        _subscribed = true;
        _links = BuildLinks();
    }

    protected override void OnParametersSet() => _links = BuildLinks();

    private void HandleCultureChanged()
    {
        _links = BuildLinks();
        _ = InvokeAsync(StateHasChanged);
    }

    private ImmutableArray<CultureLink> BuildLinks()
    {
        var current = State.Current.ValueOrDefault ?? string.Empty;
        var feature = Http.HttpContext?.Features.Get<ICultureUrlFeature>();

        // Without a request there is no page to point at — an interactive circuit switching a
        // panel's language has no URL to build and does not need one.
        var policy = feature?.Policy;
        var prefix = feature is null ? string.Empty : feature.SitePathBase;

        var builder = ImmutableArray.CreateBuilder<CultureLink>();

        // Iterate the CONFIGURED allow-list, not ICultureState.Supported.
        //
        // Supported is an array of LanguageModel resolved through ILanguageProvider, whose
        // registry is a frozen set of seventeen built-ins. A configured tag outside it does not
        // fail — it silently resolves to a neighbouring model, so a site supporting pt-pt AND
        // pt-br produced two identical "português (Portugal)" entries both pointing at /pt-pt/,
        // and Brazilian Portuguese was simply unreachable from the switcher. The tag list is the
        // authority for which languages exist; the model registry is only good for a flag.
        foreach (var tag in Options.CurrentValue.SupportedCultures)
        {
            var code = tag.ToLowerInvariant();
            var culture = (Culture)code;
            var url = string.Empty;

            if (feature is not null && policy is not null && policy.IsPrefixed)
            {
                var path = feature.Routes.ToLocalized(code, feature.RoutePath);
                var built = policy.BuildPath(code, path);
                if (built is not null)
                    url = prefix + built;
            }

            // Flag only. Everything else comes from the Culture VO, which reads ICU: its
            // NativeName is the real endonym, where LanguageModel.NativeName falls back to the
            // English name on every built-in and renders a "Polish" entry in a Polish menu.
            var model = Languages.GetByCode(code);
            var flag = string.IsNullOrWhiteSpace(model?.IconFlag) ? (MarkupString?)null : new MarkupString(model.IconFlag);

            builder.Add(new CultureLink(
                Culture: code,
                Hreflang: ToHreflang(policy, code),
                NativeName: culture.NativeName ?? model?.EnglishName ?? code,
                EnglishName: culture.EnglishName ?? model?.EnglishName ?? code,
                Flag: flag,
                Url: url,
                IsCurrent: string.Equals(code, current, StringComparison.OrdinalIgnoreCase)));
        }

        return builder.ToImmutable();
    }

    private static string ToHreflang(CultureUrlPolicy? policy, string culture)
    {
        var segment = policy?.SegmentFor(culture) ?? culture;
        var dash = segment.IndexOf('-');
        return dash < 0
            ? segment
            : string.Concat(segment.AsSpan(0, dash + 1), segment.AsSpan(dash + 1).ToString().ToUpperInvariant());
    }

    public void Dispose()
    {
        if (!_subscribed)
            return;

        State.OnChange -= HandleCultureChanged;
        _subscribed = false;
        GC.SuppressFinalize(this);
    }
}
