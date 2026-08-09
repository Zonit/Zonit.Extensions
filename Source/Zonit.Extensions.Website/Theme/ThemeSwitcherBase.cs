using Microsoft.AspNetCore.Components;
using Zonit.Extensions.Tenants.Settings;
using Microsoft.AspNetCore.Http;

namespace Zonit.Extensions.Website;

/// <summary>
/// Base class for a theme switcher. Supplies the wiring; the markup is entirely yours.
/// </summary>
/// <remarks>
/// <para>Like the language switcher, this ships no markup — a footer toggle, a segmented control
/// in a settings pane and an icon button in a top bar have nothing in common but the behaviour.
/// What it gives you is a set of attributes to splat onto whatever element you draw:</para>
///
/// <code>
/// @inherits ThemeSwitcherBase
///
/// &lt;button type="button" @attributes="ToggleAttributes" aria-label="@T("Switch theme")"&gt;
///   &lt;span class="only-light"&gt;🌙&lt;/span&gt;
///   &lt;span class="only-dark"&gt;☀️&lt;/span&gt;
/// &lt;/button&gt;
/// </code>
///
/// <para><b>Render both states and let CSS choose.</b> The example above draws both icons and
/// hides one with <c>[data-theme="dark"] .only-light { display:none }</c>. That is not a
/// stylistic preference: the alternative is asking the server which icon to draw, which makes
/// the HTML depend on the theme cookie and costs the whole Site its shared-cache hit rate for
/// the sake of one glyph.</para>
///
/// <para><b>Works identically in both render modes.</b> The attributes carry a plain DOM
/// <c>onclick</c>, not a Blazor event handler, so the switcher behaves the same on a statically
/// rendered marketing page and inside an interactive circuit, and needs no JS interop, no
/// round-trip and no reload.</para>
/// </remarks>
public abstract class ThemeSwitcherBase : ComponentBase
{
    [Inject] private ICurrentSite Site { get; set; } = default!;
    [Inject] private IHttpContextAccessor Http { get; set; } = default!;

    /// <summary>Theme configuration of the Site being rendered.</summary>
    protected AppearanceOptions Options => Site.Appearance;

    /// <summary>
    /// Splat onto a button to cycle light ↔ dark:
    /// <c>&lt;button @attributes="ToggleAttributes"&gt;</c>.
    /// </summary>
    protected IReadOnlyDictionary<string, object> ToggleAttributes
        => new Dictionary<string, object> { ["onclick"] = $"{Options.GlobalName}.toggle()" };

    /// <summary>
    /// Splat onto a control that selects one specific mode — the three buttons of a
    /// light / dark / system segmented control. <see cref="ColorScheme.System"/> clears the stored
    /// choice and hands control back to the operating system.
    /// </summary>
    protected IReadOnlyDictionary<string, object> SetAttributes(ColorScheme mode)
        => new Dictionary<string, object>
        {
            ["onclick"] = $"{Options.GlobalName}.set('{Name(mode)}')",
        };

    /// <summary>
    /// The stored preference, read from the cookie. <see cref="ColorScheme.System"/> when the
    /// visitor has expressed none — which includes every first visit and every crawler.
    /// </summary>
    /// <remarks>
    /// <b>Do not branch your markup on this.</b> Doing so makes the response depend on the theme
    /// cookie, which is exactly the coupling the client-side design exists to avoid; render both
    /// states and let CSS pick. It is exposed for the cases that are not markup — an analytics
    /// dimension, a server-generated preview image, a settings form's initial value on a page
    /// that is already uncacheable.
    /// </remarks>
    protected ColorScheme Current
    {
        get
        {
            var raw = Http.HttpContext?.Request.Cookies[Options.CookieName];
            return raw switch
            {
                "dark" => ColorScheme.Dark,
                "light" => ColorScheme.Light,
                _ => ColorScheme.System,
            };
        }
    }

    private static string Name(ColorScheme mode) => mode switch
    {
        ColorScheme.Dark => "dark",
        ColorScheme.Light => "light",
        _ => "system",
    };
}
