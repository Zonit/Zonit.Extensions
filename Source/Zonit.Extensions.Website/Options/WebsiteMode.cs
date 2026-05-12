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
}
