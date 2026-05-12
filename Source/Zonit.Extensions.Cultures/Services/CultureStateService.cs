using System.Collections.Immutable;
using Microsoft.Extensions.Options;
using Zonit.Extensions.Cultures.Options;

namespace Zonit.Extensions.Cultures.Services;

/// <summary>
/// Per-scope storage of the currently active <see cref="Culture"/> and time-zone.
/// Implements both <see cref="ICultureState"/> (read) and <see cref="ICultureManager"/> (write).
/// Lifetime: <c>Scoped</c>.
/// </summary>
internal sealed class CultureStateService : ICultureManager
{
    private readonly ILanguageProvider _languages;
    private readonly CultureOption _options;

    private Culture _culture;
    private string _timeZone;
    private readonly ImmutableArray<LanguageModel> _supported;

    public CultureStateService(ILanguageProvider languages, IOptions<CultureOption> options)
    {
        _languages = languages;
        _options = options.Value;

        _culture = Culture.TryCreate(_options.DefaultCulture, out var c) ? c : Culture.Default;
        _timeZone = _options.DefaultTimeZone;
        _supported = BuildSupported(_languages, _options.SupportedCultures);
    }

    public Culture Current => _culture;
    public string TimeZone => _timeZone;
    public ImmutableArray<LanguageModel> Supported => _supported;

    public event Action? OnChange;

    public void SetCulture(Culture culture)
    {
        var resolved = ResolveCulture(culture);
        if (resolved == _culture) return;

        _culture = resolved;
        OnChange?.Invoke();
    }

    public void SetTimeZone(string timeZone)
    {
        if (string.IsNullOrWhiteSpace(timeZone))
            timeZone = _options.DefaultTimeZone;

        if (string.Equals(_timeZone, timeZone, StringComparison.Ordinal))
            return;

        try
        {
            // Validate — throws TimeZoneNotFoundException if unknown.
            TimeZoneInfo.FindSystemTimeZoneById(timeZone);
            _timeZone = timeZone;
            OnChange?.Invoke();
        }
        catch (TimeZoneNotFoundException)
        {
            // Fall back to default; do not raise OnChange on no-op fallback.
        }
    }

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
