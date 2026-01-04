using System.Globalization;

namespace Zonit.Extensions;

/// <summary>
/// Reprezentuje kulturê w formacie jêzykowym (np. "en-US", "pl-PL").
/// </summary>
public readonly struct Culture : IEquatable<Culture>
{
    /// <summary>
    /// Domyœlna kultura (en-US).
    /// </summary>
    public static readonly Culture Default = new("en-US");

    /// <summary>
    /// Wartoœæ kultury w formacie jêzykowym (np. "en-US").
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Tworzy domyœln¹ kulturê (en-US).
    /// </summary>
    public Culture()
    {
        Value = "en-US";
    }

    /// <summary>
    /// Tworzy now¹ kulturê na podstawie podanego kodu jêzykowego.
    /// </summary>
    /// <param name="value">Kod kultury w formacie jêzykowym (np. "en-US", "pl-PL").</param>
    /// <exception cref="ArgumentNullException">Rzucany gdy <paramref name="value"/> jest null.</exception>
    /// <exception cref="CultureNotFoundException">Rzucany gdy <paramref name="value"/> nie jest prawid³owym kodem kultury.</exception>
    public Culture(string value)
    {
        ArgumentNullException.ThrowIfNull(value, nameof(value));

        try
        {
            var cultureInfo = new CultureInfo(value);
            Value = cultureInfo.Name;
        }
        catch (CultureNotFoundException ex)
        {
            throw new CultureNotFoundException($"Culture '{value}' is not a valid culture code.", ex);
        }
    }

    /// <summary>
    /// Tworzy kulturê z obiektu CultureInfo.
    /// </summary>
    /// <param name="cultureInfo">Obiekt CultureInfo.</param>
    /// <exception cref="ArgumentNullException">Rzucany gdy <paramref name="cultureInfo"/> jest null.</exception>
    public Culture(CultureInfo cultureInfo)
    {
        ArgumentNullException.ThrowIfNull(cultureInfo, nameof(cultureInfo));
        Value = cultureInfo.Name;
    }

    /// <summary>
    /// Konwertuje kulturê na obiekt CultureInfo.
    /// </summary>
    public CultureInfo ToCultureInfo() => new(Value);

    /// <summary>
    /// Pobiera kod jêzyka (np. "en" dla "en-US").
    /// </summary>
    public string LanguageCode => ToCultureInfo().TwoLetterISOLanguageName;

    /// <summary>
    /// Pobiera nazwê wyœwietlan¹ kultury w jêzyku natywnym.
    /// </summary>
    public string NativeName => ToCultureInfo().NativeName;

    /// <summary>
    /// Pobiera nazwê wyœwietlan¹ kultury w jêzyku angielskim.
    /// </summary>
    public string EnglishName => ToCultureInfo().EnglishName;

    /// <summary>
    /// Konwertuje string na obiekt Culture.
    /// </summary>
    public static implicit operator Culture(string value) => new(value);

    /// <summary>
    /// Konwertuje Culture na string.
    /// </summary>
    public static implicit operator string(Culture culture) => culture.Value ?? string.Empty;

    /// <summary>
    /// Konwertuje CultureInfo na Culture.
    /// </summary>
    public static implicit operator Culture(CultureInfo cultureInfo) => new(cultureInfo);

    /// <summary>
    /// Konwertuje Culture na CultureInfo.
    /// </summary>
    public static implicit operator CultureInfo(Culture culture) => culture.ToCultureInfo();

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
    /// Porównuje dwie kultury.
    /// </summary>
    public static bool operator ==(Culture left, Culture right) =>
        left.Equals(right);

    /// <summary>
    /// Porównuje dwie kultury.
    /// </summary>
    public static bool operator !=(Culture left, Culture right) => !(left == right);

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>
    /// Tworzy kulturê z podanego kodu jêzykowego.
    /// </summary>
    public static Culture Create(string value) => new(value);

    /// <summary>
    /// Próbuje utworzyæ kulturê z podanego kodu jêzykowego.
    /// </summary>
    /// <param name="value">Kod kultury.</param>
    /// <param name="culture">Utworzona kultura lub null jeœli wartoœæ jest nieprawid³owa.</param>
    /// <returns>True jeœli kultura zosta³a utworzona, false w przeciwnym razie.</returns>
    public static bool TryCreate(string? value, out Culture? culture)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            culture = null;
            return false;
        }

        try
        {
            culture = new Culture(value);
            return true;
        }
        catch (CultureNotFoundException)
        {
            culture = null;
            return false;
        }
    }
}
