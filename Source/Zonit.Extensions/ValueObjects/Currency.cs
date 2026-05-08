using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Zonit.Extensions.Converters;

namespace Zonit.Extensions;

/// <summary>
/// Represents a currency identified by its ISO 4217 alphabetic code (for fiat) or
/// a commonly accepted ticker (for cryptocurrencies). Examples: <c>USD</c>, <c>EUR</c>,
/// <c>PLN</c>, <c>GBP</c>, <c>JPY</c>, <c>BTC</c>, <c>ETH</c>.
/// </summary>
/// <remarks>
/// This is a DDD value object designed for:
/// <list type="bullet">
///   <item>Entity Framework Core (stored as the alphabetic code, e.g. "USD")</item>
///   <item>Blazor form validation (via <see cref="TypeConverter"/>)</item>
///   <item>JSON serialization (via <see cref="JsonConverter"/>)</item>
///   <item>Model binding in ASP.NET Core</item>
/// </list>
/// <para>
/// Known currencies expose <see cref="Symbol"/>, <see cref="Name"/>, <see cref="DecimalDigits"/>
/// and <see cref="IsCrypto"/>. Unknown but syntactically valid codes (3–10 letters/digits) are
/// accepted and stored verbatim, but their <see cref="Symbol"/>/<see cref="Name"/> may be empty.
/// </para>
/// <para>
/// <c>default(Currency).Code</c> returns an empty string. Use <see cref="Empty"/> explicitly,
/// or one of the predefined constants (<see cref="USD"/>, <see cref="EUR"/>, <see cref="PLN"/>, ...).
/// </para>
/// </remarks>
[TypeConverter(typeof(ValueObjectTypeConverter<Currency>))]
[JsonConverter(typeof(CurrencyJsonConverter))]
public readonly struct Currency : IEquatable<Currency>, IComparable<Currency>, IParsable<Currency>, ISpanParsable<Currency>
{
    /// <summary>
    /// Maximum length of a currency code (e.g., crypto tickers like "USDT", "USDC", "DOGE").
    /// </summary>
    public const int MaxLength = 10;

    /// <summary>
    /// Minimum length of a currency code (ISO 4217 is 3, but 2 is allowed for some legacy codes).
    /// </summary>
    public const int MinLength = 2;

    /// <summary>
    /// Empty currency instance. Equivalent to <c>default(Currency)</c>.
    /// </summary>
    public static readonly Currency Empty = default;

    private readonly string? _code;

    /// <summary>
    /// The currency code, always uppercase (e.g., "USD", "PLN", "BTC").
    /// Returns empty string for <see cref="Empty"/>.
    /// </summary>
    public string Code => _code ?? string.Empty;

    /// <summary>
    /// Indicates whether the currency has a meaningful value.
    /// </summary>
    public bool HasValue => !string.IsNullOrEmpty(_code);

    /// <summary>
    /// Currency symbol (e.g., "$", "€", "zł"). Returns empty string when no symbol is defined
    /// for this currency or for unknown codes; consumers should fall back to <see cref="Code"/>.
    /// </summary>
    public string Symbol => GetMetadata().Symbol;

    /// <summary>
    /// Human-readable English name (e.g., "United States Dollar"). Empty for unknown codes.
    /// </summary>
    public string Name => GetMetadata().Name;

    /// <summary>
    /// Number of fractional digits typically used when displaying amounts in this currency.
    /// 2 by default. JPY = 0, BTC = 8, ETH = 18.
    /// </summary>
    public int DecimalDigits => GetMetadata().DecimalDigits;

    /// <summary>
    /// True if this is a known cryptocurrency (BTC, ETH, ...). False for fiat or unknown codes.
    /// </summary>
    public bool IsCrypto => GetMetadata().IsCrypto;

    /// <summary>
    /// True if the code matches a predefined fiat or crypto currency (see <see cref="GetKnownCurrencies"/>).
    /// </summary>
    public bool IsKnown => HasValue && KnownCurrencies.ContainsKey(_code!);

    /// <summary>
    /// Creates a new currency from the specified code.
    /// </summary>
    /// <param name="code">Currency code (case-insensitive). Will be normalized to upper-case.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="code"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="code"/> is empty, too short, too long, or contains invalid characters.</exception>
    public Currency(string code)
    {
        ArgumentNullException.ThrowIfNull(code);

        var normalized = Normalize(code);
        if (!IsValidCode(normalized, out var error))
            throw new ArgumentException(error, nameof(code));

        _code = normalized;
    }

    /// <summary>
    /// Returns formatted amount using this currency's symbol and decimal digits.
    /// Falls back to <see cref="Code"/> when no symbol is defined.
    /// </summary>
    /// <param name="amount">The amount to format.</param>
    /// <param name="formatProvider">Optional culture for number formatting. Defaults to <see cref="System.Globalization.CultureInfo.CurrentCulture"/>.</param>
    /// <returns>Formatted string, e.g. "$19.99", "19.99 zł", "0.00012345 BTC".</returns>
    public string Format(decimal amount, IFormatProvider? formatProvider = null)
    {
        var meta = GetMetadata();
        var digits = meta.DecimalDigits;
        var formatted = amount.ToString("N" + digits.ToString(System.Globalization.CultureInfo.InvariantCulture), formatProvider ?? System.Globalization.CultureInfo.CurrentCulture);

        if (!HasValue)
            return formatted;

        if (string.IsNullOrEmpty(meta.Symbol))
            return $"{formatted} {Code}";

        return meta.SymbolPosition switch
        {
            SymbolPosition.Before => $"{meta.Symbol}{formatted}",
            SymbolPosition.After => $"{formatted} {meta.Symbol}",
            _ => $"{meta.Symbol}{formatted}"
        };
    }

    /// <inheritdoc />
    public bool Equals(Currency other) => string.Equals(Code, other.Code, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Currency other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Code.GetHashCode(StringComparison.Ordinal);

    /// <summary>Compares two currencies for equality.</summary>
    public static bool operator ==(Currency left, Currency right) => left.Equals(right);

    /// <summary>Compares two currencies for inequality.</summary>
    public static bool operator !=(Currency left, Currency right) => !left.Equals(right);

    /// <inheritdoc />
    public int CompareTo(Currency other) => string.Compare(Code, other.Code, StringComparison.Ordinal);

    /// <summary>Compares two currencies for less than.</summary>
    public static bool operator <(Currency left, Currency right) => left.CompareTo(right) < 0;

    /// <summary>Compares two currencies for less than or equal.</summary>
    public static bool operator <=(Currency left, Currency right) => left.CompareTo(right) <= 0;

    /// <summary>Compares two currencies for greater than.</summary>
    public static bool operator >(Currency left, Currency right) => left.CompareTo(right) > 0;

    /// <summary>Compares two currencies for greater than or equal.</summary>
    public static bool operator >=(Currency left, Currency right) => left.CompareTo(right) >= 0;

    /// <summary>
    /// Converts <see cref="Currency"/> to its <see cref="Code"/> string.
    /// </summary>
    public static implicit operator string(Currency currency) => currency.Code;

    /// <summary>
    /// Converts a string to a <see cref="Currency"/>.
    /// Returns <see cref="Empty"/> for null/whitespace or invalid codes (no exception).
    /// </summary>
    public static implicit operator Currency(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return Empty;

        return TryCreate(code, out var currency) ? currency : Empty;
    }

    /// <inheritdoc />
    public override string ToString() => Code;

    /// <summary>
    /// Returns "USD ($)" or "BTC" when no symbol is defined.
    /// </summary>
    public string ToDisplayString()
    {
        if (!HasValue) return string.Empty;
        var sym = Symbol;
        return string.IsNullOrEmpty(sym) ? Code : $"{Code} ({sym})";
    }

    /// <summary>
    /// Creates a currency from the specified code. Throws on invalid input.
    /// </summary>
    public static Currency Create(string code) => new(code);

    /// <summary>
    /// Tries to create a currency from the specified code without throwing.
    /// </summary>
    /// <param name="code">Currency code (case-insensitive).</param>
    /// <param name="currency">Created currency or <see cref="Empty"/> if invalid.</param>
    /// <returns>True if a valid currency was created.</returns>
    public static bool TryCreate(string? code, out Currency currency)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            currency = Empty;
            return false;
        }

        var normalized = Normalize(code);
        if (!IsValidCode(normalized, out _))
        {
            currency = Empty;
            return false;
        }

        currency = new Currency(skipValidation: true, normalized);
        return true;
    }

    /// <summary>
    /// Validates that a string can be used as a currency code.
    /// </summary>
    public static bool IsValid(string? code) => !string.IsNullOrWhiteSpace(code) && IsValidCode(Normalize(code), out _);

    /// <inheritdoc />
    public static Currency Parse(string s, IFormatProvider? provider)
    {
        if (TryParse(s, provider, out var result))
            return result;

        throw new FormatException($"Cannot parse '{s}' as Currency.");
    }

    /// <inheritdoc />
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Currency result)
        => TryCreate(s, out result);

    /// <inheritdoc />
    public static Currency Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        if (TryParse(s, provider, out var result))
            return result;

        throw new FormatException("Cannot parse as Currency.");
    }

    /// <inheritdoc />
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Currency result)
        => TryCreate(s.ToString(), out result);

    /// <summary>
    /// Returns the list of all predefined (known) currencies.
    /// </summary>
    public static IReadOnlyCollection<Currency> GetKnownCurrencies()
        => KnownCurrencies.Keys.Select(c => new Currency(skipValidation: true, c)).ToArray();

    // ===== Private helpers =====

    /// <summary>Internal constructor that bypasses validation (for trusted callers).</summary>
    private Currency(bool skipValidation, string normalizedCode)
    {
        _ = skipValidation;
        _code = normalizedCode;
    }

    private static string Normalize(string code) => code.Trim().ToUpperInvariant();

    private static bool IsValidCode(string normalized, out string error)
    {
        if (string.IsNullOrEmpty(normalized))
        {
            error = "Currency code is required.";
            return false;
        }

        if (normalized.Length < MinLength)
        {
            error = $"Currency code must be at least {MinLength} characters long.";
            return false;
        }

        if (normalized.Length > MaxLength)
        {
            error = $"Currency code cannot exceed {MaxLength} characters.";
            return false;
        }

        for (var i = 0; i < normalized.Length; i++)
        {
            var c = normalized[i];
            if (!((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')))
            {
                error = "Currency code can only contain letters and digits.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private CurrencyMetadata GetMetadata()
    {
        if (!HasValue) return CurrencyMetadata.Empty;
        return KnownCurrencies.TryGetValue(_code!, out var meta) ? meta : CurrencyMetadata.Unknown;
    }

    private enum SymbolPosition { Before, After }

    private readonly record struct CurrencyMetadata(string Symbol, string Name, int DecimalDigits, bool IsCrypto, SymbolPosition SymbolPosition)
    {
        public static readonly CurrencyMetadata Empty = new(string.Empty, string.Empty, 2, false, SymbolPosition.Before);
        public static readonly CurrencyMetadata Unknown = new(string.Empty, string.Empty, 2, false, SymbolPosition.Before);
    }

    // ===== Predefined currencies =====

    /// <summary>United States Dollar.</summary>
    public static readonly Currency USD = new(skipValidation: true, "USD");
    /// <summary>Euro.</summary>
    public static readonly Currency EUR = new(skipValidation: true, "EUR");
    /// <summary>Polish Złoty.</summary>
    public static readonly Currency PLN = new(skipValidation: true, "PLN");
    /// <summary>British Pound Sterling.</summary>
    public static readonly Currency GBP = new(skipValidation: true, "GBP");
    /// <summary>Japanese Yen.</summary>
    public static readonly Currency JPY = new(skipValidation: true, "JPY");
    /// <summary>Swiss Franc.</summary>
    public static readonly Currency CHF = new(skipValidation: true, "CHF");
    /// <summary>Canadian Dollar.</summary>
    public static readonly Currency CAD = new(skipValidation: true, "CAD");
    /// <summary>Australian Dollar.</summary>
    public static readonly Currency AUD = new(skipValidation: true, "AUD");
    /// <summary>Czech Koruna.</summary>
    public static readonly Currency CZK = new(skipValidation: true, "CZK");
    /// <summary>Hungarian Forint.</summary>
    public static readonly Currency HUF = new(skipValidation: true, "HUF");
    /// <summary>Norwegian Krone.</summary>
    public static readonly Currency NOK = new(skipValidation: true, "NOK");
    /// <summary>Swedish Krona.</summary>
    public static readonly Currency SEK = new(skipValidation: true, "SEK");
    /// <summary>Danish Krone.</summary>
    public static readonly Currency DKK = new(skipValidation: true, "DKK");
    /// <summary>Chinese Yuan Renminbi.</summary>
    public static readonly Currency CNY = new(skipValidation: true, "CNY");
    /// <summary>Indian Rupee.</summary>
    public static readonly Currency INR = new(skipValidation: true, "INR");
    /// <summary>Russian Ruble.</summary>
    public static readonly Currency RUB = new(skipValidation: true, "RUB");
    /// <summary>Ukrainian Hryvnia.</summary>
    public static readonly Currency UAH = new(skipValidation: true, "UAH");
    /// <summary>Turkish Lira.</summary>
    public static readonly Currency TRY = new(skipValidation: true, "TRY");
    /// <summary>Brazilian Real.</summary>
    public static readonly Currency BRL = new(skipValidation: true, "BRL");
    /// <summary>Mexican Peso.</summary>
    public static readonly Currency MXN = new(skipValidation: true, "MXN");
    /// <summary>South Korean Won.</summary>
    public static readonly Currency KRW = new(skipValidation: true, "KRW");
    /// <summary>Singapore Dollar.</summary>
    public static readonly Currency SGD = new(skipValidation: true, "SGD");
    /// <summary>Hong Kong Dollar.</summary>
    public static readonly Currency HKD = new(skipValidation: true, "HKD");
    /// <summary>New Zealand Dollar.</summary>
    public static readonly Currency NZD = new(skipValidation: true, "NZD");
    /// <summary>South African Rand.</summary>
    public static readonly Currency ZAR = new(skipValidation: true, "ZAR");
    /// <summary>UAE Dirham.</summary>
    public static readonly Currency AED = new(skipValidation: true, "AED");
    /// <summary>Israeli New Shekel.</summary>
    public static readonly Currency ILS = new(skipValidation: true, "ILS");
    /// <summary>Romanian Leu.</summary>
    public static readonly Currency RON = new(skipValidation: true, "RON");
    /// <summary>Bulgarian Lev.</summary>
    public static readonly Currency BGN = new(skipValidation: true, "BGN");
    /// <summary>Thai Baht.</summary>
    public static readonly Currency THB = new(skipValidation: true, "THB");

    // ----- Crypto -----

    /// <summary>Bitcoin.</summary>
    public static readonly Currency BTC = new(skipValidation: true, "BTC");
    /// <summary>Ethereum.</summary>
    public static readonly Currency ETH = new(skipValidation: true, "ETH");
    /// <summary>Tether (USDT).</summary>
    public static readonly Currency USDT = new(skipValidation: true, "USDT");
    /// <summary>USD Coin.</summary>
    public static readonly Currency USDC = new(skipValidation: true, "USDC");
    /// <summary>Binance Coin.</summary>
    public static readonly Currency BNB = new(skipValidation: true, "BNB");
    /// <summary>Ripple.</summary>
    public static readonly Currency XRP = new(skipValidation: true, "XRP");
    /// <summary>Cardano.</summary>
    public static readonly Currency ADA = new(skipValidation: true, "ADA");
    /// <summary>Solana.</summary>
    public static readonly Currency SOL = new(skipValidation: true, "SOL");
    /// <summary>Dogecoin.</summary>
    public static readonly Currency DOGE = new(skipValidation: true, "DOGE");
    /// <summary>Polkadot.</summary>
    public static readonly Currency DOT = new(skipValidation: true, "DOT");
    /// <summary>Litecoin.</summary>
    public static readonly Currency LTC = new(skipValidation: true, "LTC");
    /// <summary>Polygon.</summary>
    public static readonly Currency MATIC = new(skipValidation: true, "MATIC");

    private static readonly Dictionary<string, CurrencyMetadata> KnownCurrencies = new(StringComparer.Ordinal)
    {
        // Fiat - symbol before amount
        ["USD"] = new("$",   "United States Dollar", 2, false, SymbolPosition.Before),
        ["GBP"] = new("£",   "British Pound Sterling", 2, false, SymbolPosition.Before),
        ["JPY"] = new("¥",   "Japanese Yen", 0, false, SymbolPosition.Before),
        ["CNY"] = new("¥",   "Chinese Yuan Renminbi", 2, false, SymbolPosition.Before),
        ["AUD"] = new("A$",  "Australian Dollar", 2, false, SymbolPosition.Before),
        ["CAD"] = new("C$",  "Canadian Dollar", 2, false, SymbolPosition.Before),
        ["NZD"] = new("NZ$", "New Zealand Dollar", 2, false, SymbolPosition.Before),
        ["HKD"] = new("HK$", "Hong Kong Dollar", 2, false, SymbolPosition.Before),
        ["SGD"] = new("S$",  "Singapore Dollar", 2, false, SymbolPosition.Before),
        ["MXN"] = new("$",   "Mexican Peso", 2, false, SymbolPosition.Before),
        ["BRL"] = new("R$",  "Brazilian Real", 2, false, SymbolPosition.Before),
        ["INR"] = new("₹",   "Indian Rupee", 2, false, SymbolPosition.Before),
        ["KRW"] = new("₩",   "South Korean Won", 0, false, SymbolPosition.Before),
        ["ILS"] = new("₪",   "Israeli New Shekel", 2, false, SymbolPosition.Before),
        ["THB"] = new("฿",   "Thai Baht", 2, false, SymbolPosition.Before),
        ["TRY"] = new("₺",   "Turkish Lira", 2, false, SymbolPosition.Before),
        ["ZAR"] = new("R",   "South African Rand", 2, false, SymbolPosition.Before),
        ["AED"] = new("د.إ", "United Arab Emirates Dirham", 2, false, SymbolPosition.Before),

        // Fiat - symbol after amount (European convention for these)
        ["EUR"] = new("€",   "Euro", 2, false, SymbolPosition.After),
        ["PLN"] = new("zł",  "Polish Złoty", 2, false, SymbolPosition.After),
        ["CHF"] = new("CHF", "Swiss Franc", 2, false, SymbolPosition.After),
        ["CZK"] = new("Kč",  "Czech Koruna", 2, false, SymbolPosition.After),
        ["HUF"] = new("Ft",  "Hungarian Forint", 2, false, SymbolPosition.After),
        ["NOK"] = new("kr",  "Norwegian Krone", 2, false, SymbolPosition.After),
        ["SEK"] = new("kr",  "Swedish Krona", 2, false, SymbolPosition.After),
        ["DKK"] = new("kr",  "Danish Krone", 2, false, SymbolPosition.After),
        ["RON"] = new("lei", "Romanian Leu", 2, false, SymbolPosition.After),
        ["BGN"] = new("лв",  "Bulgarian Lev", 2, false, SymbolPosition.After),
        ["RUB"] = new("₽",   "Russian Ruble", 2, false, SymbolPosition.After),
        ["UAH"] = new("₴",   "Ukrainian Hryvnia", 2, false, SymbolPosition.After),

        // Crypto - ticker after amount, dedicated symbols when standardised
        ["BTC"]   = new("₿",     "Bitcoin", 8, true, SymbolPosition.After),
        ["ETH"]   = new("Ξ",     "Ethereum", 18, true, SymbolPosition.After),
        ["LTC"]   = new("Ł",     "Litecoin", 8, true, SymbolPosition.After),
        ["DOGE"]  = new("Ð",     "Dogecoin", 8, true, SymbolPosition.After),
        ["USDT"]  = new(string.Empty, "Tether", 6, true, SymbolPosition.After),
        ["USDC"]  = new(string.Empty, "USD Coin", 6, true, SymbolPosition.After),
        ["BNB"]   = new(string.Empty, "Binance Coin", 8, true, SymbolPosition.After),
        ["XRP"]   = new(string.Empty, "Ripple", 6, true, SymbolPosition.After),
        ["ADA"]   = new(string.Empty, "Cardano", 6, true, SymbolPosition.After),
        ["SOL"]   = new(string.Empty, "Solana", 9, true, SymbolPosition.After),
        ["DOT"]   = new(string.Empty, "Polkadot", 10, true, SymbolPosition.After),
        ["MATIC"] = new(string.Empty, "Polygon", 18, true, SymbolPosition.After),
    };
}

/// <summary>
/// JSON converter for <see cref="Currency"/>. Serializes as the alphabetic code (e.g., "USD").
/// Accepts strings on read; returns <see cref="Currency.Empty"/> for null or invalid values.
/// </summary>
public sealed class CurrencyJsonConverter : JsonConverter<Currency>
{
    /// <inheritdoc />
    public override Currency Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return Currency.Empty;

        var value = reader.GetString();
        return Currency.TryCreate(value, out var currency) ? currency : Currency.Empty;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Currency value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Code);
    }
}
