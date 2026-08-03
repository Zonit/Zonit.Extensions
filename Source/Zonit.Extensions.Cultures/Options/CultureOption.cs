using Zonit.Extensions.Cultures.Repositories;

namespace Zonit.Extensions.Cultures.Options;

public class CultureOption
{
    /// <summary>
    /// Default culture. Used both as the initial culture of every scope and as the fallback
    /// language of the translation lookup: a key with no rendition for the active culture is
    /// retried against this one before the raw content is returned verbatim.
    /// </summary>
    /// <remarks>
    /// Compared case-insensitively everywhere, so "pl-pl" and "pl-PL" are interchangeable —
    /// <see cref="Zonit.Extensions.Culture"/> normalises to the canonical casing ("pl-PL") while this option,
    /// <see cref="SupportedCultures"/>, the URL prefix and the culture cookie are conventionally
    /// lowercase. An unparseable value collapses to "en-US" rather than throwing at startup.
    /// </remarks>
    public string DefaultCulture { get; set; } = "en-US";

    /// <summary>
    /// Default timezone
    /// </summary>
    public string DefaultTimeZone { get; set; } = "Europe/Warsaw";

    /// <summary>
    /// Supported cultures
    /// </summary>
    public string[] SupportedCultures { get; set; } = [
        "en-us",
        "ar-sa",
        "fr-fr",
        "de-de",
        "es-es",
        "it-it",
        "nl-nl",
        "sv-se",
        "da-dk",
        "no-no",
        "fi-fi",
        "ru-ru",
        "pl-pl",
        "cs-cz",
        "hu-hu",
        "sk-sk",
        "pt-pt"
    ];

    /// <summary>
    /// Enables recording of unresolved translation keys into
    /// <c>MissingTranslationRepository</c>. Disabled by default.
    /// </summary>
    /// <remarks>
    /// <para>Off by default because the keys are whatever string reached
    /// <c>ICultureProvider.Translate</c>, which in a real application includes dynamic text —
    /// a user-supplied name, an exception message, a per-row label. Recording those in a
    /// process-wide singleton turns arbitrary input into permanent allocations, and nothing in
    /// the framework reads the result: it is development tooling, so the host opts in.</para>
    ///
    /// <para>Enable it where you actually want the report, typically Development:
    /// <c>services.AddCulturesExtension(o => o.TrackMissingTranslations = env.IsDevelopment());</c>
    /// Even when enabled the store is capped — see <see cref="MaxTrackedMissingTranslations"/>.</para>
    /// </remarks>
    public bool TrackMissingTranslations { get; set; }

    /// <summary>
    /// Hard ceiling on the number of distinct keys <c>MissingTranslationRepository</c> retains
    /// while <see cref="TrackMissingTranslations"/> is enabled. Must be greater than zero.
    /// </summary>
    /// <remarks>
    /// Once reached, new keys are dropped silently (<c>BaseRepository.IsFull</c> reports it) so
    /// that a misconfigured or hostile caller cannot grow the singleton without bound. A
    /// zero or negative value throws <see cref="ArgumentOutOfRangeException"/> when the
    /// repository is first resolved rather than being silently reinterpreted.
    /// </remarks>
    public int MaxTrackedMissingTranslations { get; set; } = MissingTranslationRepository.DefaultCapacity;
}
