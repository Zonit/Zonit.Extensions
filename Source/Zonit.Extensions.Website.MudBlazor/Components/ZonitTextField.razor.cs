using MudBlazor;

namespace Zonit.Extensions.MudBlazor;

/// <summary>
/// MudTextField with automatic Value Object converter support.
/// Type T is inferred from @bind-Value - no need to specify T explicitly.
/// </summary>
/// <typeparam name="T">Inferred automatically from @bind-Value expression</typeparam>
/// <remarks>
/// <para>AOT and Trimming compatible - type resolution happens at compile time.</para>
/// <para><strong>Supported Value Objects:</strong> Title, Description, UrlSlug, Content, Url, Culture</para>
/// </remarks>
/// <example>
/// <code>
/// &lt;ZonitTextField @bind-Value="Model.Title" Label="Title" /&gt;
/// &lt;ZonitTextField @bind-Value="Model.Description" Label="Description" /&gt;
/// </code>
/// </example>
public partial class ZonitTextField<T> : MudTextField<T>
{
    private static readonly Dictionary<Type, Func<Converter<T>>> ConverterFactories = new()
    {
        [typeof(Title)] = () => CreateConverter(
            t => ((Title)(object)t!).Value,
            s => (T)(object)new Title(s!),
            (T)(object)Title.Empty),
            
        [typeof(Description)] = () => CreateConverter(
            d => ((Description)(object)d!).Value,
            s => (T)(object)new Description(s!),
            (T)(object)Description.Empty),
            
        [typeof(UrlSlug)] = () => CreateConverter(
            u => ((UrlSlug)(object)u!).Value,
            s => (T)(object)new UrlSlug(s!),
            (T)(object)UrlSlug.Empty),
            
        [typeof(Content)] = () => CreateConverter(
            c => ((Content)(object)c!).Value,
            s => (T)(object)new Content(s!),
            (T)(object)Content.Empty),
            
        [typeof(Url)] = () => CreateConverter(
            u => ((Url)(object)u!).Value,
            s => (T)(object)new Url(s!),
            (T)(object)Url.Empty),
            
        [typeof(Extensions.Culture)] = () => CreateConverter(
            c => ((Extensions.Culture)(object)c!).Value,
            s => (T)(object)new Extensions.Culture(s!),
            (T)(object)Extensions.Culture.Empty)
    };

    private static Converter<T> CreateConverter(
        Func<T, string> getValue,
        Func<string, T> createFromString,
        T emptyValue)
    {
        return new ValueObjectInternalConverter<T>(getValue, createFromString, emptyValue);
    }

    /// <summary>
    /// Called when component is initialized. Sets up the converter if not already specified.
    /// </summary>
    protected override void OnInitialized()
    {
        base.OnInitialized();
        
        // Only set converter if not explicitly provided
        if (Converter is null)
        {
            var type = typeof(T);
            if (ConverterFactories.TryGetValue(type, out var factory))
            {
                Converter = factory();
            }
        }
    }
}

/// <summary>
/// Internal converter for Value Objects that captures validation exceptions.
/// </summary>
internal class ValueObjectInternalConverter<T> : Converter<T>
{
    private readonly Func<T, string> _getValue;
    private readonly Func<string, T> _createFromString;
    private readonly T _emptyValue;

    public ValueObjectInternalConverter(
        Func<T, string> getValue,
        Func<string, T> createFromString,
        T emptyValue)
    {
        _getValue = getValue;
        _createFromString = createFromString;
        _emptyValue = emptyValue;

        SetFunc = ConvertToString;
        GetFunc = ConvertFromString;
    }

    private string? ConvertToString(T? value)
    {
        if (value is null || value.Equals(_emptyValue))
            return string.Empty;

        return _getValue(value);
    }

    private T? ConvertFromString(string? value)
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
