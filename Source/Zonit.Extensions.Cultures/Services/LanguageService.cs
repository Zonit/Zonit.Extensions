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

    /// <summary>
    /// Built-in languages, in declaration order. The order is load-bearing: the primary-subtag
    /// index below keeps the FIRST model registered for a subtag, so when two regional variants of
    /// one language are both present, whichever is listed first here is what a bare <c>en</c> or
    /// <c>es</c> resolves to.
    /// </summary>
    /// <remarks>
    /// An array rather than the dictionary's own enumeration. <see cref="FrozenDictionary{TKey,
    /// TValue}"/> guarantees no ordering, so building the subtag index by walking it would decide
    /// "which English is <c>en</c>" by hash layout — stable within a build, silently different
    /// after adding an unrelated entry, and impossible to see in code review.
    /// </remarks>
    private static readonly LanguageModel[] Registry =
    [
        new Arabic(),
        new Bengali(),
        new Bulgarian(),
        new Czech(),
        new Danish(),
        new Dutch(),
        new English(),
        new Estonian(),
        new Finnish(),
        new French(),
        new German(),
        new Greek(),
        new Hungarian(),
        new Italian(),
        new Latvian(),
        new Lithuanian(),
        new Maltese(),
        new Norwegian(),
        new Polish(),
        new Portuguese(),
        new Romanian(),
        new Russian(),
        new Slovak(),
        new Spanish(),
        new Swedish(),
    ];

    /// <summary>Built-in registry — exact code (lower-cased BCP 47) → model. Frozen, AOT-safe.</summary>
    private static readonly FrozenDictionary<string, LanguageModel> ByCode = BuildCodeIndex();

    private static FrozenDictionary<string, LanguageModel> BuildCodeIndex()
    {
        var seed = new Dictionary<string, LanguageModel>(StringComparer.OrdinalIgnoreCase);
        foreach (var model in Registry)
        {
            seed[model.Code] = model;

            // AlternativeCodes was documented as part of resolution and was never read — a model
            // could declare "en-gb" and the lookup would still fall through to the primary subtag.
            // Folded into the exact index so it resolves before the subtag guess, which is the
            // order the contract always claimed. First declaration wins, so an alias can never
            // displace a language that owns the tag outright.
            foreach (var alias in model.AlternativeCodes)
                seed.TryAdd(alias, model);
        }
        return seed.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Secondary index: primary subtag (<c>en</c>) → first registered model with that subtag.</summary>
    private static readonly FrozenDictionary<string, LanguageModel> ByPrimarySubtag = BuildPrimarySubtagIndex();

    private static FrozenDictionary<string, LanguageModel> BuildPrimarySubtagIndex()
    {
        var seed = new Dictionary<string, LanguageModel>(StringComparer.OrdinalIgnoreCase);
        foreach (var model in Registry)
        {
            var code = model.Code;
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
