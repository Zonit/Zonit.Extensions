namespace Zonit.Extensions.Website;

/// <summary>
/// How wide a page's content should be allowed to grow. Declared by the page, applied by the
/// layout.
/// </summary>
/// <remarks>
/// <para><b>Named by purpose, not by size.</b> A component library offers a t-shirt scale because
/// it does not know what you are building; a site framework does — the answer is always one of a
/// handful of layouts. <see cref="Reading"/> earns its place precisely because no size name can
/// express it: the constraint is roughly seventy characters per line, a typographic measure, and
/// it stays right when the design system's pixel values change.</para>
///
/// <para>The order is monotonic, so it can be guessed without reading this. There is deliberately
/// no numeric value attached: a layout maps these to whatever its design system uses
/// (<c>max-w-*</c>, a MudBlazor <c>MaxWidth</c>, a CSS variable) in one place, once.</para>
/// </remarks>
public enum PageWidth
{
    /// <summary>Forms, sign-in, settings — a single column of controls.</summary>
    Narrow,

    /// <summary>Prose. Constrained to a comfortable line length rather than a pixel width.</summary>
    Reading,

    /// <summary>The ordinary page. The default for anything that does not say otherwise.</summary>
    Content,

    /// <summary>Tables, dashboards, dense grids — content that earns the extra room.</summary>
    Wide,

    /// <summary>Edge to edge. Hero sections, maps, anything that should touch the viewport.</summary>
    Full,
}

/// <summary>
/// Declares a page's content width statically, so the layout applies it on the first render.
/// </summary>
/// <remarks>
/// <para>Read before the page is instantiated, exactly like <c>[LayoutKey]</c> — which is the
/// point: width is the third member of a pattern already in the framework, not a new idea. Use the
/// runtime <c>PageBase.Width</c> override only when the answer depends on data, and accept the one
/// extra render it costs.</para>
///
/// <code>
/// @page "/terms"
/// @attribute [WebsiteWidth(PageWidth.Reading)]
/// </code>
/// </remarks>
/// <param name="width">Width this page asks its layout for.</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class WebsiteWidthAttribute(PageWidth width) : Attribute
{
    /// <summary>Width this page asks its layout for.</summary>
    public PageWidth Width { get; } = width;
}
