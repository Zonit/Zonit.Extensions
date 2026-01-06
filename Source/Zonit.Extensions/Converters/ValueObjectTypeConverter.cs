using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

namespace Zonit.Extensions.Converters;

/// <summary>
/// Generic type converter for value objects that have a TryCreate method.
/// Enables automatic model binding and validation in Blazor/ASP.NET Core.
/// </summary>
/// <typeparam name="TValueObject">The value object type.</typeparam>
public class ValueObjectTypeConverter<
    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicMethods |
        DynamicallyAccessedMemberTypes.PublicProperties |
        DynamicallyAccessedMemberTypes.PublicFields)] TValueObject> : TypeConverter
{
    private static readonly MethodInfo? TryCreateMethod = typeof(TValueObject).GetMethod(
        "TryCreate",
        BindingFlags.Public | BindingFlags.Static,
        binder: null,
        [typeof(string), typeof(TValueObject).MakeByRefType()],
        modifiers: null
    );

    private static readonly PropertyInfo? ValueProperty = typeof(TValueObject).GetProperty("Value");

    private static readonly FieldInfo? MinLengthField = typeof(TValueObject).GetField("MinLength", BindingFlags.Public | BindingFlags.Static);
    private static readonly FieldInfo? MaxLengthField = typeof(TValueObject).GetField("MaxLength", BindingFlags.Public | BindingFlags.Static);

    private static readonly string TypeName = typeof(TValueObject).Name;

    /// <inheritdoc />
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }

    /// <inheritdoc />
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is not string stringValue)
        {
            return base.ConvertFrom(context, culture, value);
        }

        if (TryCreateMethod is null)
        {
            throw new InvalidOperationException($"Type {TypeName} does not have a TryCreate method.");
        }

        object?[] parameters = [stringValue, null];
        var result = TryCreateMethod.Invoke(null, parameters);

        if (result is true)
        {
            return parameters[1];
        }

        throw new FormatException(GetValidationMessage(stringValue));
    }

    /// <inheritdoc />
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
    {
        return destinationType == typeof(string) || base.CanConvertTo(context, destinationType);
    }

    /// <inheritdoc />
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is not null && ValueProperty is not null)
        {
            return ValueProperty.GetValue(value)?.ToString() ?? string.Empty;
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }

    /// <summary>
    /// Gets a validation message based on the value object's constraints.
    /// </summary>
    private static string GetValidationMessage(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return $"{TypeName} is required.";
        }

        var trimmed = value.Trim();

        if (MinLengthField?.GetValue(null) is int minLength &&
            MaxLengthField?.GetValue(null) is int maxLength)
        {
            if (trimmed.Length < minLength)
            {
                return $"{TypeName} must be at least {minLength} character{(minLength > 1 ? "s" : "")} long.";
            }

            if (trimmed.Length > maxLength)
            {
                return $"{TypeName} cannot exceed {maxLength} characters.";
            }
        }

        return $"{TypeName} is invalid.";
    }
}
