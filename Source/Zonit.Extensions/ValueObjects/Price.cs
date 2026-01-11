using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zonit.Extensions;

/// <summary>
/// Represents a monetary price with high precision for calculations and standard rounding for display.
/// Uses decimal(19,8) precision internally, rounds to 2 decimal places for accounting display.
/// In Blazor, use InputNumber which binds directly to decimal - no string conversion needed.
/// </summary>
[JsonConverter(typeof(PriceJsonConverter))]
public readonly struct Price : IEquatable<Price>, IComparable<Price>, IParsable<Price>
{
    private const int InternalPrecision = 8;
    private const int DisplayPrecision = 2;

    /// <summary>
    /// Zero price value.
    /// </summary>
    public static readonly Price Zero = new(0m);

    /// <summary>
    /// Internal value with full precision (8 decimal places).
    /// </summary>
    public decimal Value { get; }

    /// <summary>
    /// Display value rounded to 2 decimal places (accounting format).
    /// </summary>
    public decimal DisplayValue => Math.Round(Value, DisplayPrecision, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Creates a new price with the specified value.
    /// </summary>
    /// <param name="value">Price value (will be stored with 8 decimal places precision).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when value is negative.</exception>
    public Price(decimal value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Price cannot be negative.");
        }

        Value = Math.Round(value, InternalPrecision, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Creates a new price, allowing negative values (for discounts, refunds, etc.).
    /// </summary>
    /// <param name="value">Price value (can be negative).</param>
    /// <param name="allowNegative">Must be true to allow negative values.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when value is negative and allowNegative is false.</exception>
    public Price(decimal value, bool allowNegative)
    {
        if (!allowNegative && value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Price cannot be negative.");
        }

        Value = Math.Round(value, InternalPrecision, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Adds two prices.
    /// </summary>
    public static Price operator +(Price left, Price right)
    {
        return new Price(left.Value + right.Value, allowNegative: true);
    }

    /// <summary>
    /// Subtracts two prices.
    /// </summary>
    public static Price operator -(Price left, Price right)
    {
        return new Price(left.Value - right.Value, allowNegative: true);
    }

    /// <summary>
    /// Multiplies price by a quantity.
    /// </summary>
    public static Price operator *(Price price, decimal multiplier)
    {
        return new Price(price.Value * multiplier, allowNegative: true);
    }

    /// <summary>
    /// Multiplies price by a quantity.
    /// </summary>
    public static Price operator *(decimal multiplier, Price price) => price * multiplier;

    /// <summary>
    /// Divides price by a divisor.
    /// </summary>
    public static Price operator /(Price price, decimal divisor)
    {
        if (divisor == 0)
        {
            throw new DivideByZeroException("Cannot divide price by zero.");
        }
        return new Price(price.Value / divisor, allowNegative: true);
    }

    /// <summary>
    /// Compares two prices for greater than.
    /// </summary>
    public static bool operator >(Price left, Price right)
    {
        return left.Value > right.Value;
    }

    /// <summary>
    /// Compares two prices for less than.
    /// </summary>
    public static bool operator <(Price left, Price right)
    {
        return left.Value < right.Value;
    }

    /// <summary>
    /// Compares two prices for greater than or equal.
    /// </summary>
    public static bool operator >=(Price left, Price right)
    {
        return left.Value >= right.Value;
    }

    /// <summary>
    /// Compares two prices for less than or equal.
    /// </summary>
    public static bool operator <=(Price left, Price right)
    {
        return left.Value <= right.Value;
    }

    /// <summary>
    /// Converts decimal to Price.
    /// </summary>
    public static implicit operator Price(decimal value) => new(value);

    /// <summary>
    /// Converts Price to decimal (returns full precision value).
    /// </summary>
    public static implicit operator decimal(Price price) => price.Value;

    /// <inheritdoc />
    public bool Equals(Price other)
    {
        return Value == other.Value;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Price other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode();

    /// <summary>
    /// Compares two prices for equality.
    /// </summary>
    public static bool operator ==(Price left, Price right) =>
        left.Equals(right);

    /// <summary>
    /// Compares two prices for inequality.
    /// </summary>
    public static bool operator !=(Price left, Price right) => !(left == right);

    /// <inheritdoc />
    public int CompareTo(Price other)
    {
        return Value.CompareTo(other.Value);
    }

    /// <summary>
    /// Returns the display value formatted with 2 decimal places.
    /// </summary>
    public override string ToString() => DisplayValue.ToString("F2");

    /// <summary>
    /// Returns the display value formatted with the specified culture.
    /// </summary>
    public string ToString(IFormatProvider formatProvider) => DisplayValue.ToString("F2", formatProvider);

    /// <summary>
    /// Returns the full precision value as string.
    /// </summary>
    public string ToFullPrecisionString() => Value.ToString("F8");

    /// <summary>
    /// Creates a price from the specified value.
    /// </summary>
    public static Price Create(decimal value) => new(value);

    /// <summary>
    /// Creates a price, allowing negative values.
    /// </summary>
    public static Price CreateAllowNegative(decimal value) => new(value, allowNegative: true);

    /// <summary>
    /// Tries to create a price from the specified value.
    /// </summary>
    /// <param name="value">Price value.</param>
    /// <param name="price">Created price or default if value is invalid.</param>
    /// <returns>True if price was created, false otherwise.</returns>
    public static bool TryCreate(decimal value, out Price price)
    {
        if (value < 0)
        {
            price = default;
            return false;
        }

        price = new Price(value);
        return true;
    }

    /// <summary>
    /// Applies a percentage to the price.
    /// </summary>
    /// <param name="percentage">Percentage (e.g., 20 for 20%).</param>
    /// <returns>New price with percentage applied.</returns>
    public Price ApplyPercentage(decimal percentage)
    {
        return new Price(Value * (1 + percentage / 100m), allowNegative: true);
    }

    /// <summary>
    /// Calculates the percentage of this price.
    /// </summary>
    /// <param name="percentage">Percentage (e.g., 20 for 20%).</param>
    /// <returns>Price representing the percentage amount.</returns>
    public Price CalculatePercentage(decimal percentage)
    {
        return new Price(Value * percentage / 100m, allowNegative: true);
    }

    /// <summary>
    /// Returns the absolute value of the price.
    /// </summary>
    public Price Abs()
    {
        return new Price(Math.Abs(Value), allowNegative: true);
    }

    /// <summary>
    /// Returns the negated value of the price.
    /// </summary>
    public Price Negate()
    {
        return new Price(-Value, allowNegative: true);
    }

    /// <summary>
    /// Parses a string to a Price.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <param name="provider">Format provider for parsing.</param>
    /// <returns>Parsed Price.</returns>
    /// <exception cref="FormatException">Thrown when parsing fails.</exception>
    public static Price Parse(string s, IFormatProvider? provider)
    {
        if (TryParse(s, provider, out var result))
            return result;

        throw new FormatException($"Cannot parse '{s}' as Price.");
    }

    /// <summary>
    /// Tries to parse a string to a Price.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <param name="provider">Format provider for parsing.</param>
    /// <param name="result">Parsed Price or default if parsing fails.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Price result)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            result = default;
            return false;
        }

        if (decimal.TryParse(s, provider, out var decimalValue) && decimalValue >= 0)
        {
            result = new Price(decimalValue);
            return true;
        }

        result = default;
        return false;
    }
}

/// <summary>
/// JSON converter for Price value object.
/// </summary>
public sealed class PriceJsonConverter : JsonConverter<Price>
{
    /// <inheritdoc />
    public override Price Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            var value = reader.GetDecimal();
            return Price.TryCreate(value, out var price) ? price : Price.Zero;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var stringValue = reader.GetString();
            return Price.TryParse(stringValue, null, out var price) ? price : Price.Zero;
        }

        return Price.Zero;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Price value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value.Value);
    }
}
