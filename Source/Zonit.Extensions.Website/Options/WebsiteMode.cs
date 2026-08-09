namespace Zonit.Extensions.Website;

/// <summary>
/// Blazor hosting mode for a Website host.
/// </summary>
public enum WebsiteMode
{
    /// <summary>Interactive Server only.</summary>
    Server = 0,

    /// <summary>Interactive WebAssembly only (the host project is the WASM bootstrapper).</summary>
    WebAssembly = 1,

    /// <summary>Auto render mode: per-component (Server + WebAssembly).</summary>
    Auto = 2,

    /// <summary>
    /// Static server rendering with enhanced navigation — no circuit, no WebAssembly payload.
    /// </summary>
    /// <remarks>
    /// <para>The right mode for a public, content-driven Site. Every other value opens a
    /// SignalR circuit (or ships a runtime) on every page view, which a marketing or
    /// documentation page has no use for: it renders once, the visitor reads it, and enhanced
    /// navigation already makes the next page a fetch rather than a reload.</para>
    ///
    /// <para>Consequences worth knowing before choosing it: <c>@onclick</c> and the other Blazor
    /// event handlers do nothing, the prerender-to-circuit state bridges have nothing to bridge
    /// to, and interactivity has to come from ordinary JavaScript or from individual components
    /// opting in with their own <c>@rendermode</c>.</para>
    /// </remarks>
    Static = 3,
}
