using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;
using Zonit.Extensions.Converters;

namespace Zonit.Extensions;

/// <summary>
/// Represents a culture in language format (e.g., "en-US", "pl-PL").
/// </summary>
/// <remarks>
/// This is a DDD value object designed for:
/// <list type="bullet">
///   <item>Entity Framework Core (value object mapping)</item>
///   <item>Blazor form validation (via TypeConverter)</item>
///   <item>JSON serialization (via JsonConverter)</item>
///   <item>Model binding in ASP.NET Core</item>
/// </list>
/// Note: <c>default(Culture).Value</c> returns empty string (not null).
/// Use <see cref="Default"/> for a valid "en-US" culture.
/// </remarks>
[TypeConverter(typeof(ValueObjectTypeConverter<Culture>))]
[JsonConverter(typeof(CultureJsonConverter))]
public readonly struct Culture : IEquatable<Culture>, IComparable<Culture>, IParsable<Culture>, ISpanParsable<Culture>
{
    /// <summary>
    /// Whether the process runs in globalization-invariant mode — no ICU/NLS data, so
    /// <see cref="CultureInfo"/> knows only the invariant culture. Set by
    /// <c>InvariantGlobalization=true</c> at publish or by
    /// <c>DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1</c> at runtime, which is what an ICU-less
    /// container image (Alpine, <c>runtime-deps</c>) does to an otherwise normal build.
    /// </summary>
    /// <remarks>
    /// <para>Probed once, and deliberately declared <b>before</b> <see cref="Default"/>: static
    /// field initializers run in textual order and the constructor behind <see cref="Default"/>
    /// consults this flag.</para>
    ///
    /// <para>Without it, <c>new Culture("en-US")</c> in that initializer throws under invariant
    /// mode, the failure surfaces as a <see cref="TypeInitializationException"/> that no
    /// <c>catch (CultureNotFoundException)</c> can intercept — so even <see cref="TryCreate"/>
    /// threw — and because a failed type initializer is cached for the life of the process,
    /// every later touch of <see cref="Culture"/> rethrows it from wherever it is observed
    /// rather than from the real cause.</para>
    /// </remarks>
    private static readonly bool GlobalizationInvariant = DetectGlobalizationInvariant();

    /// <summary>
    /// Default culture (en-US). Use this when you need a valid default culture.
    /// </summary>
    public static readonly Culture Default = new("en-US");

    /// <summary>
    /// Empty culture instance. Equivalent to default(Culture).
    /// </summary>
    public static readonly Culture Empty = default;

    private readonly string? _value;

    /// <summary>
    /// The culture value in language format (e.g., "en-US"). Never null - returns empty string for default/Empty.
    /// </summary>
    /// <remarks>
    /// This ensures that <c>default(Culture).Value</c> returns <see cref="string.Empty"/> instead of null,
    /// making it safe to use in EF Core and other scenarios without null checks.
    /// </remarks>
    public string Value => _value ?? string.Empty;

    /// <summary>
    /// Indicates whether the culture has a meaningful value (not empty or whitespace).
    /// </summary>
    public bool HasValue => !string.IsNullOrWhiteSpace(_value);

    /// <summary>
    /// Gets the value or returns "en-US" if empty.
    /// Use this when you need a guaranteed valid culture code.
    /// </summary>
    public string ValueOrDefault => HasValue ? _value! : "en-US";

    /// <summary>
    /// Creates a new culture based on the specified language code.
    /// </summary>
    /// <param name="value">Culture code in language format (e.g., "en-US", "pl-PL").</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    /// <exception cref="CultureNotFoundException">Thrown when <paramref name="value"/> is not a valid culture code.</exception>
    public Culture(string value)
    {
        ArgumentNullException.ThrowIfNull(value, nameof(value));

        // Globalization-invariant mode rejects every non-invariant tag, so CultureInfo cannot be
        // the validator there — it would fail "pl-PL" exactly as it fails garbage. Fall back to
        // validating the tag's shape and canonicalising it by hand: the contract callers rely on
        // (well-formed in, canonically cased out, CultureNotFoundException on nonsense) survives,
        // and only the culture *data* is unavailable — which is what invariant mode means.
        if (GlobalizationInvariant)
        {
            if (!TryCanonicalizeTag(value, out var canonical))
                throw new CultureNotFoundException($"Culture '{value}' is not a valid culture code.");

            _value = canonical;
            return;
        }

        try
        {
            var cultureInfo = new CultureInfo(value);
            _value = cultureInfo.Name;
        }
        catch (CultureNotFoundException ex)
        {
            throw new CultureNotFoundException($"Culture '{value}' is not a valid culture code.", ex);
        }
    }

    /// <summary>
    /// Creates a culture from a CultureInfo object.
    /// </summary>
    /// <param name="cultureInfo">CultureInfo object.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="cultureInfo"/> is null.</exception>
    public Culture(CultureInfo cultureInfo)
    {
        ArgumentNullException.ThrowIfNull(cultureInfo, nameof(cultureInfo));
        _value = cultureInfo.Name;
    }

    /// <summary>
    /// Converts culture to a CultureInfo object. Returns null if empty, or CultureInfo for "en-US" as fallback.
    /// </summary>
    /// <remarks>
    /// Under globalization-invariant mode there is no per-culture data to return, so a non-empty
    /// culture yields <see cref="CultureInfo.InvariantCulture"/> rather than throwing. Formatting
    /// and comparison then follow invariant rules — the honest outcome when the runtime carries
    /// no ICU data; <see cref="Value"/> still round-trips the tag you supplied.
    /// </remarks>
    public CultureInfo? ToCultureInfo()
    {
        if (!HasValue)
            return null;

        return GlobalizationInvariant ? CultureInfo.InvariantCulture : new CultureInfo(Value);
    }

    /// <summary>
    /// Converts culture to a CultureInfo object. Returns "en-US" CultureInfo if empty.
    /// </summary>
    /// <remarks>See <see cref="ToCultureInfo"/> for the globalization-invariant behaviour.</remarks>
    public CultureInfo ToCultureInfoOrDefault()
        => GlobalizationInvariant ? CultureInfo.InvariantCulture : new CultureInfo(ValueOrDefault);


    /// <summary>
    /// Gets the two-letter language code (e.g., "en" for "en-US").
    /// </summary>
    /// <remarks>
    /// Read off the tag itself under globalization-invariant mode, where every
    /// <see cref="CultureInfo"/> would report the invariant culture's "iv" instead.
    /// </remarks>
    public string? LanguageCode
    {
        get
        {
            if (!GlobalizationInvariant)
                return ToCultureInfo()?.TwoLetterISOLanguageName;

            if (!HasValue)
                return null;

            var separator = Value.IndexOf('-');
            return separator < 0 ? Value : Value[..separator];
        }
    }

    /// <summary>
    /// Gets the display name of the culture in its native language.
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> under globalization-invariant mode: the display name lives in the
    /// ICU data the process does not have, and answering "Invariant Language (Invariant Country)"
    /// for every culture would be worse than admitting it is unknown.
    /// </remarks>
    public string? NativeName => GlobalizationInvariant ? null : ToCultureInfo()?.NativeName;

    /// <summary>
    /// Gets the display name of the culture in English.
    /// </summary>
    /// <remarks>See <see cref="NativeName"/> — <see langword="null"/> under globalization-invariant mode.</remarks>
    public string? EnglishName => GlobalizationInvariant ? null : ToCultureInfo()?.EnglishName;

    /// <summary>
    /// Converts Culture to string. Returns empty string for <see cref="Empty"/>.
    /// </summary>
    public static implicit operator string(Culture culture) => culture.Value;

    /// <summary>
    /// Converts string to Culture. Returns Empty for null/whitespace or invalid culture codes.
    /// </summary>
    public static implicit operator Culture(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Empty;

        return TryCreate(value, out var culture) ? culture : Empty;
    }

    /// <summary>
    /// Converts CultureInfo to Culture.
    /// </summary>
    public static implicit operator Culture(CultureInfo cultureInfo) => new(cultureInfo);

    /// <summary>
    /// Converts Culture to CultureInfo.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the culture has no value.</exception>
    public static implicit operator CultureInfo(Culture culture) => 
        culture.ToCultureInfo() ?? throw new InvalidOperationException("Cannot convert empty Culture to CultureInfo.");

    /// <inheritdoc />
    public bool Equals(Culture other)
    {
        return string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Culture other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

    /// <summary>
    /// Compares two cultures for equality.
    /// </summary>
    public static bool operator ==(Culture left, Culture right) =>
        left.Equals(right);

    /// <summary>
    /// Compares two cultures for inequality.
    /// </summary>
    public static bool operator !=(Culture left, Culture right) => !(left == right);

    /// <inheritdoc />
    public int CompareTo(Culture other) => string.Compare(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Compares two cultures for less than.
    /// </summary>
    public static bool operator <(Culture left, Culture right) => left.CompareTo(right) < 0;

    /// <summary>
    /// Compares two cultures for less than or equal.
    /// </summary>
    public static bool operator <=(Culture left, Culture right) => left.CompareTo(right) <= 0;

    /// <summary>
    /// Compares two cultures for greater than.
    /// </summary>
    public static bool operator >(Culture left, Culture right) => left.CompareTo(right) > 0;

    /// <summary>
    /// Compares two cultures for greater than or equal.
    /// </summary>
    public static bool operator >=(Culture left, Culture right) => left.CompareTo(right) >= 0;

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>
    /// Creates a culture from the specified language code.
    /// </summary>
    public static Culture Create(string value) => new(value);

    /// <summary>
    /// Tries to create a culture from the specified language code.
    /// </summary>
    /// <param name="value">Culture code.</param>
    /// <param name="culture">Created culture or default if value is invalid.</param>
    /// <returns>True if culture was created, false otherwise.</returns>
    public static bool TryCreate(string? value, out Culture culture)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            culture = Empty;
            return false;
        }

        try
        {
            culture = new Culture(value);
            return true;
        }
        catch (CultureNotFoundException)
        {
            culture = Empty;
            return false;
        }
    }

    /// <summary>
    /// Parses a string to a Culture.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <param name="provider">Format provider (not used).</param>
    /// <returns>Parsed Culture.</returns>
    /// <exception cref="FormatException">Thrown when parsing fails.</exception>
    public static Culture Parse(string s, IFormatProvider? provider)
    {
        if (TryParse(s, provider, out var result))
            return result;

        throw new FormatException($"Cannot parse '{s}' as Culture.");
    }

    /// <summary>
    /// Tries to parse a string to a Culture.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <param name="provider">Format provider (not used).</param>
    /// <param name="result">Parsed Culture or default if parsing fails.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Culture result)
        => TryCreate(s, out result);

    /// <summary>
    /// Parses a span of characters to a Culture.
    /// </summary>
    /// <param name="s">The span of characters to parse.</param>
    /// <param name="provider">Format provider (not used).</param>
    /// <returns>Parsed Culture.</returns>
    /// <exception cref="FormatException">Thrown when parsing fails.</exception>
    public static Culture Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        if (TryParse(s, provider, out var result))
            return result;

        throw new FormatException("Cannot parse as Culture.");
    }

    /// <summary>
    /// Tries to parse a span of characters to a Culture.
    /// </summary>
    /// <param name="s">The span of characters to parse.</param>
    /// <param name="provider">Format provider (not used).</param>
    /// <param name="result">Parsed Culture or <see cref="Empty"/> if parsing fails.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Culture result)
        => TryCreate(s.ToString(), out result);

    /// <summary>
    /// Probes once whether the runtime carries culture data.
    /// </summary>
    /// <remarks>
    /// Two failure shapes, one meaning: with <c>PredefinedCulturesOnly</c> (the default under
    /// invariant mode) "en-US" throws, and with it turned off the tag is accepted but collapses
    /// to invariant data. Comparing against <see cref="CultureInfo.InvariantCulture"/> catches
    /// the second; the <c>catch</c> catches the first. Reading the AppContext switch directly
    /// would miss the environment-variable form, which is precisely the container case.
    /// </remarks>
    private static bool DetectGlobalizationInvariant()
    {
        try
        {
            return CultureInfo.GetCultureInfo("en-US").EnglishName
                == CultureInfo.InvariantCulture.EnglishName;
        }
        catch (CultureNotFoundException)
        {
            return true;
        }
    }

    /// <summary>
    /// Validates a BCP-47 tag by shape and returns it in canonical casing —
    /// <c>language[-Script][-REGION]</c>, e.g. "pl-pl" → "pl-PL", "zh-hans-cn" → "zh-Hans-CN".
    /// </summary>
    /// <remarks>
    /// Only reached under globalization-invariant mode, where <see cref="CultureInfo"/> cannot
    /// tell a real tag from a typo. Structural validation is strictly weaker than ICU's registry
    /// — "qq-QQ" passes here — but it is the strongest check available without culture data, and
    /// it keeps nonsense like "not a culture" out.
    /// </remarks>
    private static bool TryCanonicalizeTag(string value, [NotNullWhen(true)] out string? canonical)
    {
        canonical = null;

        var parts = value.Split('-');
        if (parts.Length is < 1 or > 3)
            return false;

        // Language: two or three ASCII letters ("en", "pl", "fil").
        if (!IsAsciiLetters(parts[0]) || parts[0].Length is < 2 or > 3)
            return false;

        parts[0] = parts[0].ToLowerInvariant();

        for (var i = 1; i < parts.Length; i++)
        {
            var part = parts[i];

            // Script subtag: exactly four letters, title case ("Hans", "Cyrl").
            if (part.Length == 4 && IsAsciiLetters(part))
            {
                parts[i] = string.Concat(
                    char.ToUpperInvariant(part[0]).ToString(),
                    part[1..].ToLowerInvariant());
                continue;
            }

            // Region subtag: two letters ("PL") or three digits ("419").
            if (part.Length == 2 && IsAsciiLetters(part))
            {
                parts[i] = part.ToUpperInvariant();
                continue;
            }

            if (part.Length == 3 && IsAsciiDigits(part))
                continue;

            return false;
        }

        canonical = string.Join('-', parts);
        return true;
    }

    private static bool IsAsciiLetters(ReadOnlySpan<char> value)
    {
        foreach (var c in value)
            if (!char.IsAsciiLetter(c))
                return false;

        return !value.IsEmpty;
    }

    private static bool IsAsciiDigits(ReadOnlySpan<char> value)
    {
        foreach (var c in value)
            if (!char.IsAsciiDigit(c))
                return false;

        return !value.IsEmpty;
    }
}
