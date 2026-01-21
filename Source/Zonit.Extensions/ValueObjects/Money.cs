using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zonit.Extensions;

/// <summary>
/// Represents a monetary amount that can be positive or negative.
/// Used for balances, transactions, adjustments, refunds, and any monetary value that may go negative.
/// Uses decimal(19,8) precision internally, rounds to 2 decimal places for display.
/// </summary>
/// <remarks>
/// <para><strong>Use Money for:</strong> Wallet balances, transaction amounts, adjustments, refunds, credits/debits</para>
/// <para><strong>Use Price for:</strong> Product prices, unit costs (always non-negative)</para>
/// <para>In Blazor, use InputNumber which binds directly to decimal - no string conversion needed.</para>
/// </remarks>
[JsonConverter(typeof(MoneyJsonConverter))]
public readonly struct Money : IEquatable<Money>, IComparable<Money>, IParsable<Money>, IFormattable
{
    private const int InternalPrecision = 8;
    private const int DisplayPrecision = 2;

    /// <summary>
    /// Zero money value.
    /// </summary>
    public static readonly Money Zero = new(0m);

    /// <summary>
    /// Internal value with full precision (8 decimal places).
    /// </summary>
    public decimal Value { get; }

    /// <summary>
    /// Display value rounded to 2 decimal places (accounting format).
    /// </summary>
    public decimal DisplayValue => Math.Round(Value, DisplayPrecision, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Returns true if value is negative.
    /// </summary>
    public bool IsNegative => Value < 0;

    /// <summary>
    /// Returns true if value is positive (greater than zero).
    /// </summary>
    public bool IsPositive => Value > 0;

    /// <summary>
    /// Returns true if value is zero.
    /// </summary>
    public bool IsZero => Value == 0;

    /// <summary>
    /// Creates a new money amount with the specified value.
    /// Negative values are allowed.
    /// </summary>
    /// <param name="value">Money value (will be stored with 8 decimal places precision).</param>
    public Money(decimal value)
    {
        Value = Math.Round(value, InternalPrecision, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Adds two money amounts.
    /// </summary>
    public static Money operator +(Money left, Money right)
    {
        return new Money(left.Value + right.Value);
    }

    /// <summary>
    /// Subtracts two money amounts.
    /// </summary>
    public static Money operator -(Money left, Money right)
    {
        return new Money(left.Value - right.Value);
    }

    /// <summary>
    /// Negates a money amount.
    /// </summary>
    public static Money operator -(Money money)
    {
        return new Money(-money.Value);
    }

    /// <summary>
    /// Multiplies money by a quantity.
    /// </summary>
    public static Money operator *(Money money, decimal multiplier)
    {
        return new Money(money.Value * multiplier);
    }

    /// <summary>
    /// Multiplies money by a quantity.
    /// </summary>
    public static Money operator *(decimal multiplier, Money money) => money * multiplier;

    /// <summary>
    /// Divides money by a divisor.
    /// </summary>
    public static Money operator /(Money money, decimal divisor)
    {
        if (divisor == 0)
        {
            throw new DivideByZeroException("Cannot divide money by zero.");
        }
        return new Money(money.Value / divisor);
    }

    /// <summary>
    /// Compares two money amounts for greater than.
    /// </summary>
    public static bool operator >(Money left, Money right)
    {
        return left.Value > right.Value;
    }

    /// <summary>
    /// Compares two money amounts for less than.
    /// </summary>
    public static bool operator <(Money left, Money right)
    {
        return left.Value < right.Value;
    }

    /// <summary>
    /// Compares two money amounts for greater than or equal.
    /// </summary>
    public static bool operator >=(Money left, Money right)
    {
        return left.Value >= right.Value;
    }

    /// <summary>
    /// Compares two money amounts for less than or equal.
    /// </summary>
    public static bool operator <=(Money left, Money right)
    {
        return left.Value <= right.Value;
    }

    /// <summary>
    /// Converts decimal to Money.
    /// </summary>
    public static implicit operator Money(decimal value) => new(value);

    /// <summary>
    /// Converts Money to decimal (returns full precision value).
    /// </summary>
    public static implicit operator decimal(Money money) => money.Value;

    /// <summary>
    /// Converts Price to Money.
    /// </summary>
    public static implicit operator Money(Price price) => new(price.Value);

    /// <inheritdoc />
    public bool Equals(Money other)
    {
        return Value == other.Value;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Money other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode();

    /// <summary>
    /// Compares two money amounts for equality.
    /// </summary>
    public static bool operator ==(Money left, Money right) =>
        left.Equals(right);

    /// <summary>
    /// Compares two money amounts for inequality.
    /// </summary>
    public static bool operator !=(Money left, Money right) => !(left == right);

    /// <inheritdoc />
    public int CompareTo(Money other)
    {
        return Value.CompareTo(other.Value);
    }

    /// <summary>
    /// Returns the display value formatted with 2 decimal places.
    /// </summary>
    public override string ToString() => DisplayValue.ToString("F2");

    /// <summary>
    /// Returns the display value formatted with the specified format and culture.
    /// </summary>
    /// <param name="format">Format string (e.g., "C" for currency, "N" for number, "F" for fixed-point). If null or empty, defaults to "F2".</param>
    /// <param name="formatProvider">Format provider for culture-specific formatting.</param>
    /// <returns>Formatted string representation of the display value.</returns>
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        return string.IsNullOrEmpty(format)
            ? DisplayValue.ToString("F2", formatProvider)
            : DisplayValue.ToString(format, formatProvider);
    }

    /// <summary>
    /// Returns the display value formatted with the specified culture.
    /// </summary>
    public string ToString(IFormatProvider formatProvider) => ToString(null, formatProvider);

    /// <summary>
    /// Returns the full precision value as string.
    /// </summary>
    public string ToFullPrecisionString() => Value.ToString("F8");

    /// <summary>
    /// Creates a money amount from the specified value.
    /// </summary>
    public static Money Create(decimal value) => new(value);

    /// <summary>
    /// Creates a positive money amount. Throws if value is negative.
    /// </summary>
    /// <param name="value">Money value (must be non-negative).</param>
    /// <returns>Money amount.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when value is negative.</exception>
    public static Money CreatePositive(decimal value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Value must be non-negative. Use Create() for negative values.");
        }
        return new Money(value);
    }

    /// <summary>
    /// Tries to create a positive money amount.
    /// </summary>
    /// <param name="value">Money value.</param>
    /// <param name="money">Created money or default if value is negative.</param>
    /// <returns>True if money was created (value >= 0), false otherwise.</returns>
    public static bool TryCreatePositive(decimal value, out Money money)
    {
        if (value < 0)
        {
            money = default;
            return false;
        }

        money = new Money(value);
        return true;
    }

    /// <summary>
    /// Applies a percentage to the money amount.
    /// </summary>
    /// <param name="percentage">Percentage (e.g., 20 for 20%).</param>
    /// <returns>New money with percentage applied.</returns>
    public Money ApplyPercentage(decimal percentage)
    {
        return new Money(Value * (1 + percentage / 100m));
    }

    /// <summary>
    /// Calculates the percentage of this money amount.
    /// </summary>
    /// <param name="percentage">Percentage (e.g., 20 for 20%).</param>
    /// <returns>Money representing the percentage amount.</returns>
    public Money CalculatePercentage(decimal percentage)
    {
        return new Money(Value * percentage / 100m);
    }

    /// <summary>
    /// Returns the absolute value of the money amount.
    /// </summary>
    public Money Abs()
    {
        return new Money(Math.Abs(Value));
    }

    /// <summary>
    /// Returns the negated value of the money amount.
    /// </summary>
    public Money Negate()
    {
        return new Money(-Value);
    }

    /// <summary>
    /// Returns the minimum of two money amounts.
    /// </summary>
    public static Money Min(Money left, Money right)
    {
        return left.Value <= right.Value ? left : right;
    }

    /// <summary>
    /// Returns the maximum of two money amounts.
    /// </summary>
    public static Money Max(Money left, Money right)
    {
        return left.Value >= right.Value ? left : right;
    }

    /// <summary>
    /// Clamps the money amount between minimum and maximum values.
    /// </summary>
    public Money Clamp(Money min, Money max)
    {
        if (Value < min.Value) return min;
        if (Value > max.Value) return max;
        return this;
    }

    /// <summary>
    /// Converts to Price. Throws if value is negative.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when value is negative.</exception>
    public Price ToPrice()
    {
        if (Value < 0)
        {
            throw new InvalidOperationException("Cannot convert negative Money to Price. Use Abs() first if needed.");
        }
        return new Price(Value);
    }

    /// <summary>
    /// Tries to convert to Price.
    /// </summary>
    /// <param name="price">Converted Price or default if value is negative.</param>
    /// <returns>True if conversion succeeded (value >= 0), false otherwise.</returns>
    public bool TryToPrice(out Price price)
    {
        if (Value < 0)
        {
            price = default;
            return false;
        }

        price = new Price(Value);
        return true;
    }

    /// <summary>
    /// Parses a string to Money.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <param name="provider">Format provider for parsing.</param>
    /// <returns>Parsed Money.</returns>
    /// <exception cref="FormatException">Thrown when parsing fails.</exception>
    public static Money Parse(string s, IFormatProvider? provider)
    {
        if (TryParse(s, provider, out var result))
            return result;

        throw new FormatException($"Cannot parse '{s}' as Money.");
    }

    /// <summary>
    /// Tries to parse a string to Money.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <param name="provider">Format provider for parsing.</param>
    /// <param name="result">Parsed Money or default if parsing fails.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Money result)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            result = default;
            return false;
        }

        if (decimal.TryParse(s, provider, out var decimalValue))
        {
            result = new Money(decimalValue);
            return true;
        }

        result = default;
        return false;
    }
}

/// <summary>
/// JSON converter for Money value object.
/// </summary>
public sealed class MoneyJsonConverter : JsonConverter<Money>
{
    /// <inheritdoc />
    public override Money Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            var value = reader.GetDecimal();
            return new Money(value);
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var stringValue = reader.GetString();
            return Money.TryParse(stringValue, null, out var money) ? money : Money.Zero;
        }

        return Money.Zero;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Money value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value.Value);
    }
}
