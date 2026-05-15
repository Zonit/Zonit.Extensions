using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Zonit.Extensions.Tenants;

/// <summary>
/// Validates that a property holds a HEX color (<c>#RGB</c>, <c>#RGBA</c>, <c>#RRGGBB</c>,
/// or <c>#RRGGBBAA</c>) and signals to the admin UI that the field should render as a
/// color picker.
/// </summary>
/// <remarks>
/// Used together with <see cref="DisplayAttribute"/> on
/// <see cref="Settings.Setting{T}"/> models. The Blazor renderer in
/// <c>Zonit.Extensions.Website</c> picks this attribute up to swap the default
/// <c>InputText</c> for a color-picker control.
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public partial class ColorPickerAttribute : ValidationAttribute
{
    private readonly StringLengthAttribute _length;

    public ColorPickerAttribute()
        : base("Please provide a valid HEX color (e.g., #123ABC or #123ABCFF).")
    {
        _length = new StringLengthAttribute(9)
        {
            MinimumLength = 4,
            ErrorMessage = "Color must be in #RGB, #RGBA, #RRGGBB, or #RRGGBBAA format."
        };
    }

    public int MaximumLength => _length.MaximumLength;
    public int MinimumLength => _length.MinimumLength;

    /// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null) return ValidationResult.Success;
        if (value is not string str) return new ValidationResult(ErrorMessage);

        var lengthResult = _length.GetValidationResult(str, validationContext);
        if (lengthResult != ValidationResult.Success) return lengthResult;

        // Only the canonical lengths — guards against e.g. "#12345" (6 chars, looks legal but isn't).
        if (str.Length is not (4 or 5 or 7 or 9))
            return new ValidationResult("Color must be in #RGB, #RGBA, #RRGGBB, or #RRGGBBAA format.");

        return HexColorRegex().IsMatch(str)
            ? ValidationResult.Success
            : new ValidationResult(ErrorMessage);
    }

    [GeneratedRegex("^#([A-Fa-f0-9]{3}|[A-Fa-f0-9]{4}|[A-Fa-f0-9]{6}|[A-Fa-f0-9]{8})$")]
    private static partial Regex HexColorRegex();
}
