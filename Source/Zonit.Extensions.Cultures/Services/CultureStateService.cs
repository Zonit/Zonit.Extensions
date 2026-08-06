using System.Collections.Immutable;
using Microsoft.Extensions.Options;
using Zonit.Extensions.Cultures.Options;

namespace Zonit.Extensions.Cultures.Services;

/// <summary>
/// Per-scope storage of the currently active <see cref="Culture"/> and time-zone.
/// Implements both <see cref="ICultureState"/> (read) and <see cref="ICultureManager"/> (write).
/// Lifetime: <c>Scoped</c>.
/// </summary>
internal sealed class CultureStateService : ICultureManager, IDisposable
{
    private readonly ILanguageProvider _languages;
    private readonly IDisposable? _reload;
    private CultureOption _options;

    private Culture _culture;
    private Zone _timeZone;
    private ImmutableArray<LanguageModel> _supported;

    public CultureStateService(ILanguageProvider languages, IOptionsMonitor<CultureOption> options)
    {
        _languages = languages;
        _options = options.CurrentValue;

        _culture = Culture.TryCreate(_options.DefaultCulture, out var c) ? c : Culture.Default;
        _timeZone = ResolveDefaultTimeZone(_options.DefaultTimeZone);
        _supported = BuildSupported(_languages, _options.SupportedCultures);

        // A scope is a request in ASP.NET but a whole connection in a Blazor circuit, which can
        // outlive several configuration reloads. Subscribing — rather than only reading at
        // construction — is what lets an open tab pick up a new language: the OnChange below is
        // the same signal SetCulture raises, so ICultureProvider re-emits it and ExtensionsBase
        // re-renders through InvokeAsync(StateHasChanged).
        _reload = options.OnChange(HandleOptionsReloaded);
    }

    public Culture Current => _culture;
    public Zone TimeZone => _timeZone;
    public ImmutableArray<LanguageModel> Supported => _supported;

    public event Action? OnChange;

    /// <summary>
    /// Re-reads the configuration after a reload and notifies the scope. Runs on the
    /// configuration provider's thread, so it only swaps whole values — the language list is
    /// rebuilt and assigned, never mutated in place.
    /// </summary>
    private void HandleOptionsReloaded(CultureOption options)
    {
        _options = options;
        _supported = BuildSupported(_languages, options.SupportedCultures);

        // A narrowed allow-list can strand the active culture on a language the configuration
        // no longer permits. Re-resolve so the scope cannot keep rendering in it; ResolveCulture
        // falls back to the (possibly also new) default.
        _culture = ResolveCulture(_culture);

        // Fired unconditionally: even when the active culture survives, Supported changed and
        // the language picker has to redraw.
        OnChange?.Invoke();
    }

    /// <summary>Drops the configuration-reload subscription when the scope ends.</summary>
    public void Dispose() => _reload?.Dispose();

    public void SetCulture(Culture culture)
    {
        var resolved = ResolveCulture(culture);
        if (resolved == _culture) return;

        _culture = resolved;
        OnChange?.Invoke();
    }

    public void SetTimeZone(Zone timeZone)
    {
        // Empty / unparseable input → fall back to configured default. This keeps the
        // contract symmetric with SetCulture (which also falls back rather than throws).
        var next = timeZone.HasValue ? timeZone : ResolveDefaultTimeZone(_options.DefaultTimeZone);
        if (next == _timeZone) return;

        _timeZone = next;
        OnChange?.Invoke();
    }

    /// <summary>
    /// Resolves the configured default into a usable <see cref="Zone"/>. If the
    /// configuration is bogus we collapse to <see cref="Zone.Utc"/> rather than crash
    /// at startup — the caller can change the zone later through <see cref="SetTimeZone"/>.
    /// </summary>
    private static Zone ResolveDefaultTimeZone(string configured)
        => Zone.TryCreate(configured, out var tz) ? tz : Zone.Utc;

    /// <summary>
    /// Picks a supported culture from <paramref name="requested"/> or falls back to
    /// <c>CultureOption.DefaultCulture</c>. Comparison is case-insensitive against the
    /// configured supported list.
    /// </summary>
    private Culture ResolveCulture(Culture requested)
    {
        if (!requested.HasValue)
            return Culture.TryCreate(_options.DefaultCulture, out var def) ? def : Culture.Default;

        foreach (var s in _options.SupportedCultures)
        {
            if (string.Equals(s, requested.Value, StringComparison.OrdinalIgnoreCase))
                return requested;
        }

        return Culture.TryCreate(_options.DefaultCulture, out var fallback) ? fallback : Culture.Default;
    }

    private static ImmutableArray<LanguageModel> BuildSupported(
        ILanguageProvider languages, IEnumerable<string> supportedCultures)
    {
        var builder = ImmutableArray.CreateBuilder<LanguageModel>();
        foreach (var code in supportedCultures)
        {
            var model = languages.GetByCode(code);
            // GetByCode never returns null after the FrozenDictionary refactor.
            builder.Add(model);
        }
        return builder.ToImmutable();
    }
}
