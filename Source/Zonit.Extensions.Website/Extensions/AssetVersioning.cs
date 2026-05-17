using System.Collections.Concurrent;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;

namespace Zonit.Extensions.Website;

/// <summary>
/// Cache-busting helpers for <see cref="ResourceAssetCollection"/> — the type returned by
/// <see cref="ComponentBase.Assets"/> (the <c>@Assets</c> property available inside Razor
/// components).
/// </summary>
/// <remarks>
/// <para><b>Why this exists.</b> In .NET 9/10 the framework auto-fingerprints
/// <c>_framework/*</c> files (Blazor framework JS / Wasm) so the URL produced by
/// <c>@Assets["_framework/blazor.web.js"]</c> includes a content hash and lives in the
/// browser cache forever. The same is <em>not</em> true for assets shipped by Razor Class
/// Libraries (RCL) under <c>_content/{AssemblyName}/...</c>: unless the RCL author
/// explicitly opts in via <c>&lt;StaticWebAssetFingerprintPattern&gt;</c> in their
/// <c>.csproj</c>, the URL returned by <c>@Assets["_content/MudBlazor/MudBlazor.min.css"]</c>
/// is plain — no fingerprint, no cache-bust on package upgrade. Browsers then happily
/// serve the previous version after a MudBlazor (or any other RCL) update.</para>
///
/// <para><b>What this does.</b> <see cref="Versioned"/> takes a relative asset path, looks
/// up the canonical (possibly already-fingerprinted) URL through <see cref="ResourceAssetCollection"/>,
/// and appends <c>?v={assemblyVersion}</c> when the path is in the RCL
/// <c>_content/{AssemblyName}/...</c> shape. The version is read once per assembly from
/// the loaded <see cref="Assembly.GetName"/> and memoised in a static cache — no
/// per-render reflection overhead.</para>
///
/// <para><b>What to call it like.</b></para>
/// <code lang="razor">
/// &lt;link href="@Assets.Versioned(&quot;_content/MudBlazor/MudBlazor.min.css&quot;)" rel="stylesheet" /&gt;
/// &lt;script src="@Assets.Versioned(&quot;_content/MudBlazor/MudBlazor.min.js&quot;)"&gt;&lt;/script&gt;
/// </code>
///
/// <para>Output:</para>
/// <code>
/// &lt;link href="_content/MudBlazor/MudBlazor.min.css?v=9.4.0.0" rel="stylesheet" /&gt;
/// </code>
///
/// <para><b>Future-proofing.</b> If MudBlazor (or any other RCL) ever ships with proper
/// fingerprint patterns in a future release, <see cref="ResourceAssetCollection"/> will
/// return a URL that already contains <c>#[.{fingerprint}]</c> — <see cref="Versioned"/>
/// still appends <c>?v=…</c> on top of that, which is a harmless duplicate of cache-busting
/// signal but does not break the URL. So consumers can adopt <c>@Assets.Versioned(...)</c>
/// today and the call survives both the "plain URL" and "fingerprinted URL" worlds.</para>
/// </remarks>
public static class AssetVersioning
{
    /// <summary>
    /// Cache of "assembly name" → "stringified version" lookups. Populated lazily the
    /// first time a given <c>_content/{AssemblyName}/...</c> path is asked for and
    /// reused forever — the version of a loaded assembly is constant for the process
    /// lifetime, so this is cheap and never invalidates.
    /// </summary>
    private static readonly ConcurrentDictionary<string, string> _versionCache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Matches the <c>_content/{AssemblyName}/...</c> prefix used by Razor Class Library
    /// static assets so we can pull the RCL assembly name out of the path. The pattern
    /// is intentionally anchored at the start of the string because the path passed to
    /// <c>@Assets</c> is always a relative URL rooted at the app's static assets root.
    /// </summary>
    private static readonly Regex _rclPathRegex =
        new(@"^_content/(?<asm>[^/]+)/", RegexOptions.Compiled);

    /// <summary>
    /// Returns the canonical asset URL with an additional <c>?v={assemblyVersion}</c>
    /// query string for cache-busting. Path shape <c>_content/{AssemblyName}/...</c>
    /// triggers the RCL-version lookup; anything else falls through to the unchanged
    /// <c>@Assets</c> result (so framework files keep their fingerprint URL untouched).
    /// </summary>
    /// <param name="assets">
    /// The <see cref="ResourceAssetCollection"/> bound to the active component scope —
    /// typically the <c>@Assets</c> property of <see cref="ComponentBase"/>.
    /// </param>
    /// <param name="path">
    /// Relative asset path. RCL assets use the <c>_content/{AssemblyName}/{file}</c>
    /// convention; plain wwwroot assets use just <c>{file}</c> or a sub-folder path.
    /// </param>
    /// <returns>
    /// The resolved URL with the cache-bust suffix appended when applicable, or the
    /// raw <c>@Assets</c> result when the path is not an RCL <c>_content/...</c> path
    /// or when the matching assembly cannot be located in the loaded
    /// <see cref="AppDomain.CurrentDomain"/>.
    /// </returns>
    public static string Versioned(this ResourceAssetCollection assets, string path)
    {
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentException.ThrowIfNullOrEmpty(path);

        // Resolve the canonical URL first so any framework-fingerprinted RCL assets
        // (uncommon today, expected once RCL authors adopt StaticWebAssetFingerprintPattern)
        // already carry their hash; we then add ?v=... on top as a belt-and-braces signal.
        var url = assets[path];

        var version = ResolveAssemblyVersion(path);
        if (version is null)
            return url;

        // Some asset URLs may already carry query parameters (rare in practice, but the
        // contract of @Assets is "opaque string"). Use & when there's a ? already, ?
        // otherwise — same rule that browsers and HttpUtility follow.
        return url.Contains('?') ? $"{url}&v={version}" : $"{url}?v={version}";
    }

    /// <summary>
    /// Pulls the assembly name out of an <c>_content/{AssemblyName}/...</c> path and
    /// looks up the version of the matching loaded assembly. Caches the result.
    /// Returns <see langword="null"/> when the path is not an RCL path or when no
    /// loaded assembly with that name can be found (e.g. the RCL is ahead-of-time
    /// trimmed away — unusual for a referenced CSS host but possible).
    /// </summary>
    private static string? ResolveAssemblyVersion(string path)
    {
        var match = _rclPathRegex.Match(path);
        if (!match.Success)
            return null;

        var assemblyName = match.Groups["asm"].Value;

        return _versionCache.GetOrAdd(assemblyName, static name =>
        {
            // AppDomain.GetAssemblies() enumerates every loaded assembly; comparing by
            // simple name (no version / culture / pkt) matches how the _content/{name}
            // prefix is generated by the SDK — the AssemblyName.Name is the segment.
            // Returning string.Empty (rather than null) on miss keeps the cache from
            // re-trying every render; callers treat empty version as "skip suffix".
            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => string.Equals(a.GetName().Name, name, StringComparison.OrdinalIgnoreCase));

            return assembly?.GetName().Version?.ToString() ?? string.Empty;
        }) is var v && v.Length > 0 ? v : null;
    }
}
