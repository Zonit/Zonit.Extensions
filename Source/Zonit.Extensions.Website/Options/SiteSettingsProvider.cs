using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Zonit.Extensions.Website;

/// <summary>
/// Resolves a Site's live <see cref="SiteSettings"/> by layering the <c>Website</c> configuration
/// section over the defaults declared in code, and re-resolving them when configuration reloads.
/// </summary>
/// <remarks>
/// <para>One instance per Site, created by <c>UseWebsite</c> and read by everything that needs a
/// setting at request or render time. Readers take <see cref="Current"/> once and work on that
/// immutable snapshot — a reload swaps the reference, so nobody can observe half of one
/// generation and half of the next.</para>
///
/// <para><b>Collections replace, they do not extend.</b> Binding a JSON array onto a list that
/// already holds items appends to it — deliberate <c>ConfigurationBinder</c> behaviour, and the
/// wrong reading here every time: an operator writing <c>"Disallow": [ "/search" ]</c> means
/// "disallow exactly this", not "add this to whatever the build happened to contain". Each
/// bindable collection is therefore emptied before binding, but only when the section actually
/// declares the key, so an absent key still keeps the code defaults.</para>
/// </remarks>
public sealed class SiteSettingsProvider : IDisposable
{
    /// <summary>Configuration section holding one entry per mount path.</summary>
    public const string SectionName = "Website";

    private readonly IConfiguration? _configuration;
    private readonly SiteSettings _defaults;
    private readonly string _key;
    private volatile SiteSettings _current;
    private IDisposable? _reload;

    /// <param name="configuration">
    /// Host configuration, or <see langword="null"/> when the host has none — in which case the
    /// code defaults are the whole answer and nothing ever reloads.
    /// </param>
    /// <param name="defaults">Settings declared in code through <c>UseWebsite</c>.</param>
    /// <param name="mountPath">
    /// Normalised mount path (<c>""</c> for the root). Matched against the section's keys
    /// tolerantly, so <c>"/"</c>, <c>""</c> and <c>"/admin/"</c> all find their Site.
    /// </param>
    public SiteSettingsProvider(IConfiguration? configuration, SiteSettings defaults, string mountPath)
    {
        ArgumentNullException.ThrowIfNull(defaults);

        _configuration = configuration;
        _defaults = defaults;
        _key = Normalize(mountPath);
        _current = Resolve();

        Subscribe();
    }

    /// <summary>Live settings for this Site. Read once per operation, never cached across requests.</summary>
    public SiteSettings Current => _current;

    /// <summary>
    /// Raised after a configuration reload produced a new snapshot. Consumers that derive
    /// something expensive from the settings — the URL policy's frozen lookups, for one — rebuild
    /// here rather than on every read.
    /// </summary>
    public event Action<SiteSettings>? OnChange;

    private void Subscribe()
    {
        var section = _configuration?.GetSection(SectionName);
        if (section is null)
            return;

        _reload = ChangeToken.OnChange(section.GetReloadToken, () =>
        {
            _current = Resolve();
            OnChange?.Invoke(_current);
        });
    }

    private SiteSettings Resolve()
    {
        // Always start from a clone: binding straight onto the code defaults would let the first
        // reload mutate them, so the second reload would merge onto already-overridden values and
        // a removed configuration key would never fall back to what the code said.
        var effective = _defaults.Clone();

        var site = FindSection();
        if (site is null)
            return effective;

        ClearBoundCollections(site, effective);
        site.Bind(effective);

        return effective;
    }

    /// <summary>
    /// Locates this Site's entry, tolerating the spellings a mount path is written in —
    /// <c>"/"</c> and <c>""</c> both name the root, and a trailing slash is insignificant.
    /// </summary>
    private IConfigurationSection? FindSection()
    {
        var root = _configuration?.GetSection(SectionName);
        if (root is null || !root.Exists())
            return null;

        foreach (var child in root.GetChildren())
        {
            if (string.Equals(Normalize(child.Key), _key, StringComparison.OrdinalIgnoreCase))
                return child;
        }

        return null;
    }

    /// <summary>
    /// Empties every collection the section declares, so binding replaces instead of appending.
    /// A collection the section is silent about is left alone and keeps the code default.
    /// </summary>
    private static void ClearBoundCollections(IConfigurationSection site, SiteSettings target)
    {
        if (Declares(site, nameof(SiteSettings.IndexedCultures)))
            target.IndexedCultures = [];
    }

    private static bool Declares(IConfigurationSection site, string path)
        => site.GetSection(path).GetChildren().Any();

    /// <summary>
    /// <c>"/"</c> → <c>""</c>, <c>"/admin/"</c> → <c>"/admin"</c>. Mirrors
    /// <c>SiteOptions.NormalizedPathBase</c> so a configuration key written either way matches.
    /// </summary>
    private static string Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        var trimmed = path.Trim().TrimEnd('/');
        if (trimmed.Length == 0)
            return string.Empty;

        return trimmed.StartsWith('/') ? trimmed : "/" + trimmed;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _reload?.Dispose();
        _reload = null;
    }
}
