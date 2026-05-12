using System.Collections.Frozen;
using Zonit.Extensions.Cultures.Languages;

namespace Zonit.Extensions.Cultures.Services;

/// <summary>
/// Resolves a culture code (BCP 47) to a <see cref="LanguageModel"/>. Lookup is O(1) on the
/// exact code; a secondary O(1) index by the primary subtag (e.g. <c>en</c> → <c>en-us</c>)
/// covers the common fallback case <c>en-gb</c> → <c>en-us</c>.
/// </summary>
/// <remarks>
/// The built-in language registry is a <see cref="FrozenDictionary{TKey, TValue}"/> initialised
/// once per process. Per-instance state is empty (registry is static), so the service can be
/// safely registered as a singleton.
/// </remarks>
public sealed class LanguageService : ILanguageProvider
{
    /// <summary>The default language returned when no other match is found.</summary>
    private const string DefaultCode = "en-us";

    /// <summary>Built-in registry — exact code (lower-cased BCP 47) → model. Frozen, AOT-safe.</summary>
    private static readonly FrozenDictionary<string, LanguageModel> ByCode = new Dictionary<string, LanguageModel>(StringComparer.OrdinalIgnoreCase)
    {
        ["ar-sa"] = new Arabic(),
        ["cs-cz"] = new Czech(),
        ["da-dk"] = new Danish(),
        ["nl-nl"] = new Dutch(),
        ["en-us"] = new English(),
        ["fi-fi"] = new Finnish(),
        ["fr-fr"] = new French(),
        ["de-de"] = new German(),
        ["hu-hu"] = new Hungarian(),
        ["it-it"] = new Italian(),
        ["no-no"] = new Norwegian(),
        ["pl-pl"] = new Polish(),
        ["pt-pt"] = new Portuguese(),
        ["ru-ru"] = new Russian(),
        ["sk-sk"] = new Slovak(),
        ["es-es"] = new Spanish(),
        ["sv-se"] = new Swedish(),
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>Secondary index: primary subtag (<c>en</c>) → first registered model with that subtag.</summary>
    private static readonly FrozenDictionary<string, LanguageModel> ByPrimarySubtag = BuildPrimarySubtagIndex();

    private static FrozenDictionary<string, LanguageModel> BuildPrimarySubtagIndex()
    {
        var seed = new Dictionary<string, LanguageModel>(StringComparer.OrdinalIgnoreCase);
        foreach (var (code, model) in ByCode)
        {
            var dash = code.IndexOf('-');
            var primary = dash >= 0 ? code[..dash] : code;
            seed.TryAdd(primary, model);
        }
        return seed.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public LanguageModel GetByCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return ByCode[DefaultCode];

        // 1. Exact match (e.g. "en-us").
        if (ByCode.TryGetValue(code, out var exact))
            return exact;

        // 2. Primary subtag fallback (e.g. "en-gb" → first "en-*").
        var dash = code.IndexOf('-');
        var primary = dash >= 0 ? code.AsSpan(0, dash) : code.AsSpan();
        // FrozenDictionary lookup over Span requires alternate lookup; allocate only here.
        if (ByPrimarySubtag.TryGetValue(primary.ToString(), out var byPrimary))
            return byPrimary;

        // 3. Default fallback — always present.
        return ByCode[DefaultCode];
    }
}
