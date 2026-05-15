using System.Globalization;
using Zonit.Extensions.Cultures.Models;
using Zonit.Extensions.Cultures.Repositories;

namespace Zonit.Extensions.Cultures.Services;

/// <summary>
/// Renders translations and time-zone-aware values for the current scope. Reads culture
/// state from <see cref="ICultureState"/> (no write coupling), translations from the
/// process-wide <see cref="TranslationRepository"/>, and reports unresolved keys to
/// <see cref="MissingTranslationRepository"/> for development tooling.
/// </summary>
internal sealed class CultureService : ICultureProvider, IDisposable
{
    private const string NoVariableMessage = "no variable";

    private readonly TranslationRepository _translations;
    private readonly MissingTranslationRepository _missing;
    private readonly ICultureState _state;

    private DateTimeFormatModel _dateTimeFormat = new();

    public CultureService(
        TranslationRepository translations,
        MissingTranslationRepository missing,
        ICultureState state)
    {
        _translations = translations ?? throw new ArgumentNullException(nameof(translations));
        _missing = missing ?? throw new ArgumentNullException(nameof(missing));
        _state = state ?? throw new ArgumentNullException(nameof(state));

        _state.OnChange += HandleStateChanged;
        UpdateDateTimeFormat();
    }

    public Culture Current => _state.Current;
    public DateTimeFormatModel DateTimeFormat => _dateTimeFormat;
    public event Action? OnChange;

    public Translation Translate(string content, params object?[] args)
    {
        if (string.IsNullOrWhiteSpace(content))
            return Translation.Empty;

        var current = _state.Current;
        var currentCode = current.HasValue ? current.Value : Culture.Default.Value;

        // 1. Current culture.
        var hit = FindTranslation(content, currentCode);
        if (hit is not null)
            return Format(hit.Content, args);

        // 2. Default-culture fallback.
        if (!IsDefault(currentCode))
        {
            var defHit = FindTranslation(content, Culture.Default.Value);
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

    private static bool IsDefault(string culture) =>
        string.Equals(culture, Culture.Default.Value, StringComparison.OrdinalIgnoreCase);

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

    public void Dispose() => _state.OnChange -= HandleStateChanged;
}
