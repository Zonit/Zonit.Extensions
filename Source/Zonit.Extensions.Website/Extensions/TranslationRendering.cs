using Microsoft.AspNetCore.Components;

namespace Zonit.Extensions.Website;

/// <summary>
/// Rendering helpers that bridge the framework-agnostic <see cref="Translation"/> value
/// object to Razor's <see cref="MarkupString"/>. Kept in <c>Zonit.Extensions.Website</c>
/// so that the core <c>Zonit.Extensions.Cultures</c> package stays UI-free and can be
/// used from console / mobile / WASM-client projects without pulling in
/// <c>Microsoft.AspNetCore.Components</c>.
/// </summary>
/// <remarks>
/// The original design wrapped <see cref="Translation"/> in a second struct of the same
/// name inside the Website namespace just to attach implicit <see cref="MarkupString"/>
/// conversions. That created a real ambiguity at the call site whenever both namespaces
/// were imported. C# does not permit cross-assembly partial structs, so adding extra
/// conversion operators to the original struct was off the table. Extension methods are
/// the cleanest path: they keep the VO single-defined, free of UI references, and let
/// Razor consumers opt in with one tiny <c>using</c>.
/// </remarks>
public static class TranslationRendering
{
    /// <summary>
    /// Returns the translation rendered as <see cref="MarkupString"/>, treating the
    /// stored text as raw HTML. Use this only for content you control — it bypasses
    /// Blazor's automatic encoding.
    /// </summary>
    public static MarkupString ToMarkup(this Translation translation)
        => new(translation.Value);
}
