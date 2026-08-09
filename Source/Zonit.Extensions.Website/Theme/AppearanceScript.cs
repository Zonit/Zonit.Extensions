using System.Text.Encodings.Web;
using Zonit.Extensions.Tenants.Settings;

namespace Zonit.Extensions.Website;

/// <summary>
/// Builds the blocking inline script that applies the visitor's colour scheme before the first
/// paint and exposes the switch API the rest of the page calls.
/// </summary>
/// <remarks>
/// <para><b>Why inline and blocking.</b> Anything deferred paints the wrong scheme first. A
/// dark-mode visitor seeing a white flash on every navigation is the single most common failure
/// of theme implementations, and it cannot be fixed with CSS alone because the choice lives in a
/// cookie the stylesheet cannot read. The script is a few hundred bytes and runs before the body
/// exists; that is the whole cost.</para>
///
/// <para><b>What it installs.</b> A global with three methods — <c>get()</c> returns the stored
/// preference (<c>"system"</c> when none), <c>effective()</c> returns what is actually applied,
/// <c>set(mode)</c> and <c>toggle()</c> change it. Writing <c>"system"</c> deletes the cookie
/// rather than storing the word, so a visitor who reverts to following their OS leaves no state
/// behind. While no explicit choice is stored, the script tracks live OS changes, so flipping
/// the system theme repaints the page without a reload.</para>
///
/// <para>Everything configurable is emitted through <see cref="JavaScriptEncoder"/>. The values
/// come from server configuration rather than from a request, but a configuration value that can
/// terminate a script literal is a code-injection primitive regardless of who wrote it.</para>
/// </remarks>
public static class AppearanceScript
{
    /// <summary>Renders the bootstrap script body (no <c>&lt;script&gt;</c> wrapper).</summary>
    public static string Build(AppearanceOptions options, ColorScheme fallbackScheme)
    {
        ArgumentNullException.ThrowIfNull(options);

        var js = JavaScriptEncoder.Default;
        var attribute = js.Encode(options.Attribute);
        var cookie = js.Encode(options.CookieName);
        var global = js.Encode(options.GlobalName);
        // What applies when the visitor has stored no choice. An explicit tenant default is a
        // branding decision and outranks the operating system — a site that has decided it is
        // dark should not render light for a visitor who merely never touched the switch. Only
        // ColorScheme.System defers to prefers-color-scheme, and even then something has to
        // answer on a browser without matchMedia, which is what 'light' is doing there.
        var fallback = fallbackScheme switch
        {
            ColorScheme.Dark => "'dark'",
            ColorScheme.Light => "'light'",
            _ => "s()",
        };
        const string colorScheme = "r.style.colorScheme=v;";

        // 'class' is special-cased because Tailwind's stock dark mode toggles a class rather than
        // an attribute, and setting an attribute literally named "class" would wipe every other
        // class on <html>.
        var applyAttribute = options.Attribute == "class"
            ? "r.classList.toggle('dark',v==='dark');"
            : $"r.setAttribute('{attribute}',v);";

        // Triple-brace interpolation: the script body contains literal "}}" runs (a function
        // closing inside an object literal), which "$$" would read as a placeholder terminator.
        return $$$"""
        (function(){var r=document.documentElement;
        function m(){return window.matchMedia&&window.matchMedia('(prefers-color-scheme: dark)')}
        function s(){var q=m();return q?(q.matches?'dark':'light'):'light'}
        function c(){var x=document.cookie.match(/(?:^|;\s*){{{cookie}}}=([^;]*)/);return x?decodeURIComponent(x[1]):''}
        function e(v){return v==='dark'||v==='light'?v:{{{fallback}}}}
        function a(v){v=e(v);{{{applyAttribute}}}{{{colorScheme}}}}
        a(c());
        window['{{{global}}}']={get:function(){return c()||'system'},effective:function(){return e(c())},
        set:function(v){document.cookie=v==='dark'||v==='light'?'{{{cookie}}}='+v+';path=/;max-age=31536000;SameSite=Lax':'{{{cookie}}}=;path=/;max-age=0;SameSite=Lax';a(v)},
        toggle:function(){this.set(e(c())==='dark'?'light':'dark')}};
        var q=m();if(q&&q.addEventListener)q.addEventListener('change',function(){if(!c())a('')})})();
        """;
    }
}
