using Microsoft.AspNetCore.Components;

namespace Zonit.Extensions.Website;

/// <summary>
/// Base class for Razor pages in a Zonit-hosted Site. Inherits the full DI surface of
/// <see cref="ExtensionsBase"/> (Culture / Workspace / Catalog / Authenticated / Toast
/// / Cookie / Tenant / Breadcrumbs), adds the dynamic <see cref="LayoutKey"/> hook so a page
/// can switch its rendering layout at runtime, and publishes the page's <see cref="Meta"/>
/// to the document head.
/// </summary>
/// <remarks>
/// <para><b>Prefer static attributes for layout selection.</b> Most pages should
/// declare their layout via class-level attributes — read by <c>ZonitRouteView</c>
/// <em>before</em> the page is instantiated and applied on the very first render:</para>
/// <list type="bullet">
///   <item><c>[LayoutKey("Auth.LoginBox")]</c> — pick from <c>ILayoutRegistry</c> by string.</item>
///   <item><c>[NoLayout]</c> — render raw, no chrome.</item>
///   <item>Standard <c>[Layout(typeof(X))]</c> — Blazor's built-in path.</item>
/// </list>
///
/// <para><b>Use <see cref="LayoutKey"/> only for runtime-dependent decisions</b> —
/// e.g. "use 'Login' layout for unauthenticated users, 'Dashboard' for authenticated"
/// on a single endpoint. Setting <see cref="LayoutKey"/> after the first render causes
/// exactly one re-render with the new layout; the user sees a brief flicker. The static
/// attribute path has no flicker.</para>
/// </remarks>
public abstract class PageBase : ExtensionsBase
{
    private PageMeta? _meta;
    private bool _metaPublished;

    [Inject] private IPageMetaState MetaState { get; set; } = default!;

    /// <summary>
    /// Declares this page's document metadata. Override with an expression body.
    /// </summary>
    /// <remarks>
    /// <para>Read <b>exactly once</b>, by <see cref="Meta"/>, and cached for the page's lifetime.
    /// That is what makes the terse expression body safe: an override returning <c>new()</c>
    /// produces a fresh object per read, and everything after the first read — the head, and
    /// every <c>Meta.Title = …</c> the page performs — works on the one instance the base kept.
    /// Do not read this member directly; read <see cref="Meta"/>.</para>
    ///
    /// <code>
    /// protected override PageMeta Metadata => new() { Description = "Manage your profile." };
    /// </code>
    /// </remarks>
    protected virtual PageMeta Metadata => new();

    /// <summary>
    /// This page's document metadata — title, description, social preview, indexing. Stable for
    /// the lifetime of the page, so it can be refined after data has loaded.
    /// </summary>
    /// <remarks>
    /// <para>Published to the head on initialisation and re-announced after the page's own
    /// lifecycle, so a value assigned past an <c>await</c> lands too:</para>
    ///
    /// <code>
    /// protected override async Task OnInitializedAsync(CancellationToken token)
    /// {
    ///     _user = await _users.GetAsync(Id, token);
    ///     Meta.Title = T("User profile {0}", _user.Name);
    /// }
    /// </code>
    ///
    /// <para>A page that sets nothing still renders a correct document: title, description and
    /// canonical all fall back. Composition with the website title, the <c>hreflang</c> cluster
    /// and the robots directive are never the page's business — they come from the tenant and the
    /// Site.</para>
    /// </remarks>
    protected PageMeta Meta => _meta ??= Metadata;

    /// <summary>
    /// Dynamic layout key for this page. Mirrors <c>ILayoutContext.Key</c>.
    /// </summary>
    /// <remarks>
    /// <para>Setter semantics:</para>
    /// <list type="bullet">
    ///   <item><c>null</c> — render with <em>no</em> layout (equivalent to a runtime
    ///         <see cref="NoLayoutAttribute"/>).</item>
    ///   <item><c>""</c> (empty) — fall back to the Site / router default layout
    ///         (overrides any static <see cref="LayoutKeyAttribute"/> on the page).</item>
    ///   <item>Any other string — resolved via <c>ILayoutRegistry</c>. Missing keys
    ///         log a warning and fall back to the Site default.</item>
    /// </list>
    /// <para>The override is cleared automatically when the user navigates to a
    /// different page (<c>ZonitRouteView</c> handles the reset); pages do not need to
    /// undo it manually.</para>
    /// </remarks>
    protected string? LayoutKey
    {
        get => LayoutContext.HasOverride ? LayoutContext.Key : null;
        set => LayoutContext.SetKey(value);
    }

    /// <summary>
    /// How wide this page's content should be allowed to grow. The layout reads it and maps it to
    /// its own design system.
    /// </summary>
    /// <remarks>
    /// <para><b>Prefer the attribute.</b> <c>[WebsiteWidth(PageWidth.Reading)]</c> is read before
    /// the page is instantiated and applied on the very first render; assigning here after the
    /// first render costs exactly one re-render with the new width, and the visitor may see the
    /// change. Use it only when the answer genuinely depends on data — a detail page that goes
    /// <see cref="PageWidth.Wide"/> once it knows the record has a table.</para>
    ///
    /// <para>Reset on navigation by <c>ZonitRouteView</c>, like the layout key, so a page never
    /// inherits the previous one's width.</para>
    /// </remarks>
    protected PageWidth Width
    {
        get => LayoutContext.Width;
        set => LayoutContext.SetWidth(value);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();

        // Published before the page's own OnInitializedAsync runs, so the head has a title from
        // the very first render rather than a blank tab that fills in later. Anything the page
        // learns afterwards arrives through the post-render notification below.
        MetaState.Set(Meta);
        _metaPublished = true;
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        Republish();
    }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();
        Republish();
    }

    /// <summary>
    /// Re-announces <see cref="Meta"/> after the page's own lifecycle has had a chance to change it.
    /// </summary>
    /// <remarks>
    /// <para><see cref="PageMeta"/> is a mutable object, so a title assigned inside
    /// <c>OnInitializedAsync</c> or <c>OnParametersSet</c> changes state that nothing observed.
    /// The head component holds the other half of the contract and ignores a notification that
    /// changes nothing, which is what stops this from being a render loop.</para>
    ///
    /// <para>Hooked to the async lifecycle rather than to <c>OnAfterRender</c>, which looks like
    /// the natural place and is the wrong one: <b>it never runs under static server rendering</b>
    /// — precisely the mode a public, indexable Site uses. Both hooks here run in every render
    /// mode, and both sit on the <c>ComponentBase</c> overloads rather than the
    /// cancellation-token ones that pages override, so a page cannot silently opt out of having
    /// its own metadata published by forgetting a <c>base</c> call.</para>
    /// </remarks>
    private void Republish()
    {
        if (_metaPublished)
            MetaState.Touch();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        // Clear on teardown so a page that declares no title cannot inherit the previous page's
        // one during an interactive navigation, where the head component outlives the page.
        //
        // Only if the state still holds OUR metadata. Blazor renders the incoming component tree
        // and disposes the outgoing one afterwards, so on an interactive navigation the new page
        // has already published its title by the time this runs — an unconditional Clear() wipes
        // it. The symptom is precise and easy to misread: the heading is correct on a direct load
        // and blank after every in-app navigation, because only then is there an old page to
        // dispose.
        if (disposing && _metaPublished)
        {
            // Not MetaState?.Current — that reads as "clear when both are null" to the compiler
            // and to the next reader. The state is only interesting when it exists AND still holds
            // ours; _meta is non-null here because _metaPublished says it was published.
            if (MetaState is { } state && ReferenceEquals(state.Current, _meta))
                state.Clear();

            _metaPublished = false;
        }

        base.Dispose(disposing);
    }
}
