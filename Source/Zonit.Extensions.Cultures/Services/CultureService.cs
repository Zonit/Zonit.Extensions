using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.Extensions.Options;
using Zonit.Extensions.Cultures.Models;
using Zonit.Extensions.Cultures.Options;
using Zonit.Extensions.Cultures.Repositories;

namespace Zonit.Extensions.Cultures.Services;

/// <summary>
/// Renders translations and time-zone-aware values for the current scope. Reads culture
/// state from <see cref="ICultureState"/> (no write coupling), translations from the
/// process-wide <see cref="TranslationRepository"/>, and — when
/// <see cref="CultureOption.TrackMissingTranslations"/> is enabled — reports unresolved keys
/// to <see cref="MissingTranslationRepository"/> for development tooling.
/// </summary>
internal sealed class CultureService : ICultureProvider, IDisposable
{
    private const string NoVariableMessage = "no variable";

    private readonly TranslationRepository _translations;
    private readonly MissingTranslationRepository _missing;
    private readonly ICultureState _state;

    /// <summary>
    /// Canonical form of <see cref="CultureOption.DefaultCulture"/>. This is the fallback
    /// language of the lookup and the culture whose misses are NOT worth recording (for it, the
    /// source string is the translation). Hardcoding "en-US" here — as this service used to —
    /// silently disabled the fallback for every app whose default language is not English.
    /// </summary>
    /// <remarks>
    /// Cached rather than read per call because <see cref="Translate"/> is the busiest method in
    /// the stack, and refreshed on configuration reload so a changed default language does not
    /// need a process restart.
    /// </remarks>
    private string _defaultCulture;

    private bool _trackMissing;

    private readonly IDisposable? _reload;

    private DateTimeFormatModel _dateTimeFormat = new();

    public CultureService(
        TranslationRepository translations,
        MissingTranslationRepository missing,
        ICultureState state,
        IOptionsMonitor<CultureOption> options)
    {
        _translations = translations ?? throw new ArgumentNullException(nameof(translations));
        _missing = missing ?? throw new ArgumentNullException(nameof(missing));
        _state = state ?? throw new ArgumentNullException(nameof(state));
        ArgumentNullException.ThrowIfNull(options);

        Apply(options.CurrentValue);
        _reload = options.OnChange(Apply);

        _state.OnChange += HandleStateChanged;
        UpdateDateTimeFormat();
    }

    /// <summary>
    /// Copies the two option values this service caches. Deliberately does NOT raise
    /// <see cref="OnChange"/>: <see cref="CultureStateService"/> subscribes to the same reload
    /// and raises it through <see cref="ICultureState.OnChange"/>, which this service already
    /// re-emits — signalling here too would double every re-render.
    /// </summary>
    [MemberNotNull(nameof(_defaultCulture))]
    private void Apply(CultureOption option)
    {
        // Same fallback ladder CultureStateService uses for the initial culture, so a bogus
        // configured tag degrades to en-US instead of throwing during scope construction.
        _defaultCulture = Culture.TryCreate(option.DefaultCulture, out var configured)
            ? configured.Value
            : Culture.Default.Value;
        _trackMissing = option.TrackMissingTranslations;
    }

    public Culture Current => _state.Current;
    public DateTimeFormatModel DateTimeFormat => _dateTimeFormat;
    public event Action? OnChange;

    public Translation Translate(string content, params object?[] args)
    {
        if (string.IsNullOrWhiteSpace(content))
            return Translation.Empty;

        var current = _state.Current;
        var currentCode = current.HasValue ? current.Value : _defaultCulture;

        // 1. Current culture.
        var hit = FindTranslation(content, currentCode);
        if (hit is not null)
            return Format(hit.Content, args);

        // 2. Configured-default-culture fallback.
        if (!IsDefault(currentCode))
        {
            var defHit = FindTranslation(content, _defaultCulture);
            if (defHit is not null)
                return Format(defHit.Content, args);
        }

        // 3. Surface the missing key for development tooling, then return the input verbatim.
        RecordMissing(content, currentCode);
        return Format(content, args);
    }

    public DateTime ClientTimeZone(DateTime utcDateTime)
    {
        // Delegated to the VO so that fixed-offset zones ("UTC+2", "UTC-5") work alongside
        // named ones — the old implementation only handled named zones via
        // FindSystemTimeZoneById and would silently no-op on a fixed-offset state.
        var tz = _state.TimeZone;
        if (!tz.HasValue)
            return utcDateTime;

        try
        {
            return tz.ConvertFromUtc(utcDateTime);
        }
        catch (TimeZoneNotFoundException)
        {
            return utcDateTime;
        }
    }

    private void HandleStateChanged()
    {
        UpdateDateTimeFormat();
        OnChange?.Invoke();
    }

    private void UpdateDateTimeFormat()
    {
        var info = _state.Current.ToCultureInfo() ?? CultureInfo.InvariantCulture;
        _dateTimeFormat = new DateTimeFormatModel
        {
            ShortDatePattern = info.DateTimeFormat.ShortDatePattern,
            ShortTimePattern = info.DateTimeFormat.ShortTimePattern,
        };
    }

    private Models.Translate? FindTranslation(string content, string culture)
    {
        // Fast path: TryGet is an O(1) ConcurrentDictionary lookup. The hot loop below
        // walks at most a handful of cultures per variable; LINQ would allocate a closure
        // and an enumerator on every Translate() call (this is the busiest method in the
        // entire stack — every UI render hits it).
        if (!_translations.TryGet(content, out var variable))
            return null;

        var translates = variable.Translates;
        for (int i = 0; i < translates.Count; i++)
        {
            var t = translates[i];
            if (string.Equals(t.Culture, culture, StringComparison.OrdinalIgnoreCase))
                return t;
        }
        return null;
    }

    /// <summary>
    /// Case-insensitive by necessity, never <c>==</c>: <see cref="ICultureState.Current"/> is
    /// canonically cased by <see cref="CultureInfo.Name"/> ("pl-PL") while the configured
    /// option, <see cref="CultureOption.SupportedCultures"/>, the URL prefix and the cookie are
    /// all conventionally lowercase ("pl-pl"). An ordinal comparison here would report every
    /// culture as non-default and re-run the fallback pass against itself.
    /// </summary>
    private bool IsDefault(string culture) =>
        string.Equals(culture, _defaultCulture, StringComparison.OrdinalIgnoreCase);

    private static Translation Format(string content, params object?[]? args)
    {
        if (args is null || args.Length == 0)
            return new Translation(content);

        try
        {
            return new Translation(string.Format(CultureInfo.CurrentCulture, content, args));
        }
        catch (FormatException)
        {
            return new Translation(content);
        }
    }

    private void RecordMissing(string content, string culture)
    {
        // Opt-in: `content` is caller-supplied text, so an always-on recorder turns any dynamic
        // string reaching Translate() into a permanent entry in a process-wide singleton.
        if (!_trackMissing) return;

        // Skip the default culture; that means the source string itself is the "translation".
        if (IsDefault(culture)) return;

        if (_missing.TryGet(content, out var existing))
        {
            if (existing.GetTranslate(culture) is null)
                existing.AddTranslate(new Translate { Content = string.Empty, Culture = culture });
            return;
        }

        _missing.Add(new Variable(
            content,
            [new Translate { Content = string.Empty, Culture = NoVariableMessage }]));
    }

    public void Dispose()
    {
        _state.OnChange -= HandleStateChanged;
        _reload?.Dispose();
    }
}
