namespace Zonit.Extensions.Website.MudBlazor.Converters;

/// <summary>
/// Generic MudBlazor converter for Value Objects that automatically captures validation exceptions.
/// Supports any Value Object with a string Value property and a static TryCreate/Create method.
/// </summary>
/// <typeparam name="T">The Value Object type</typeparam>
/// <remarks>
/// This converter automatically extracts error messages from exceptions thrown during Value Object creation,
/// eliminating the need to manually define error messages for each converter.
/// 
/// <para>Supported Value Objects must have:</para>
/// <list type="bullet">
///   <item>A <c>Value</c> property returning string</item>
///   <item>A static <c>Empty</c> field</item>
///   <item>A constructor accepting string value</item>
/// </list>
/// </remarks>
/// <example>
/// Usage in MudBlazor component:
/// <code>
/// &lt;MudTextField T="Title" @bind-Value="model.Title" Converter="ValueObjectConverters.Title" /&gt;
/// </code>
/// </example>
public class ValueObjectConverter<T> : global::MudBlazor.Converter<T>
    where T : struct
{
    private readonly Func<T, string> _getValue;
    private readonly Func<string?, T> _createFromString;
    private readonly T _emptyValue;

    /// <summary>
    /// Creates a new Value Object converter with custom get/create functions.
    /// </summary>
    /// <param name="getValue">Function to extract string value from Value Object</param>
    /// <param name="createFromString">Function to create Value Object from string (should throw on validation failure)</param>
    /// <param name="emptyValue">Empty/default value to return on null/whitespace input</param>
    public ValueObjectConverter(
        Func<T, string> getValue,
        Func<string?, T> createFromString,
        T emptyValue)
    {
        _getValue = getValue ?? throw new ArgumentNullException(nameof(getValue));
        _createFromString = createFromString ?? throw new ArgumentNullException(nameof(createFromString));
        _emptyValue = emptyValue;

        SetFunc = ConvertToString;
        GetFunc = ConvertFromString;
    }

    private string? ConvertToString(T value)
    {
        if (value.Equals(_emptyValue))
            return string.Empty;

        return _getValue(value);
    }

    private T ConvertFromString(string? value)
    {
        // Reset previous errors
        GetError = false;
        GetErrorMessage = null;

        if (string.IsNullOrWhiteSpace(value))
            return _emptyValue;

        try
        {
            return _createFromString(value);
        }
        catch (ArgumentException ex)
        {
            GetError = true;
            GetErrorMessage = (ex.Message, []);
            return _emptyValue;
        }
        catch (FormatException ex)
        {
            GetError = true;
            GetErrorMessage = (ex.Message, []);
            return _emptyValue;
        }
        catch (Exception ex)
        {
            GetError = true;
            GetErrorMessage = (ex.Message, []);
            return _emptyValue;
        }
    }
}
