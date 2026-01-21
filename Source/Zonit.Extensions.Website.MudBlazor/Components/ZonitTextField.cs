using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;
using MudBlazor.Utilities.Exceptions;
using System.Text.RegularExpressions;

namespace Zonit.Extensions.MudBlazor;

/// <summary>
/// MudTextField with automatic Value Object converter support.
/// Type T is inferred from @bind-Value - no need to specify T explicitly.
/// </summary>
/// <typeparam name="T">Inferred automatically from @bind-Value expression</typeparam>
/// <remarks>
/// <para>AOT and Trimming compatible - type resolution happens at compile time.</para>
/// <para><strong>Supported Value Objects:</strong> Title, Description, UrlSlug, Content, Url, Culture</para>
/// <para>
/// The converter catches all exceptions from Value Objects and displays user-friendly error messages.
/// When validation fails, the text is preserved in the input field (not cleared).
/// </para>
/// <para><strong>Counter=0:</strong> Automatically uses MaxLength from Value Object</para>
/// <para><strong>Copyable:</strong> Shows a copy-to-clipboard button on the right side</para>
/// </remarks>
/// <example>
/// <code>
/// &lt;ZonitTextField @bind-Value="Model.Title" Label="Title" /&gt;
/// &lt;ZonitTextField @bind-Value="Model.Title" Label="Title" Counter="0" /&gt;
/// &lt;ZonitTextField @bind-Value="Model.Title" Label="Title" Copyable /&gt;
/// </code>
/// </example>
public partial class ZonitTextField<T> : MudTextField<T>
{
    [Inject] private IJSRuntime JsRuntime { get; set; } = default!;
    
    private int? _detectedMaxLength;
    private bool _isCopied;
    private CancellationTokenSource? _copyResetCts;
    
    /// <summary>
    /// When true, shows a copy-to-clipboard button on the right side of the input.
    /// Icon changes to green checkmark for 2 seconds after copying.
    /// </summary>
    [Parameter]
    public bool Copyable { get; set; }
    
    public ZonitTextField()
    {
        var type = typeof(T);
        
        // Handle Nullable<T> - get underlying type
        var underlyingType = Nullable.GetUnderlyingType(type);
        var isNullable = underlyingType is not null;
        var targetType = underlyingType ?? type;
        
        var converter = CreateConverterForType(targetType, isNullable);
        if (converter is not null)
        {
            Converter = converter;
        }
        
        // Auto-detect MaxLength from Value Object's const field
        // Set MaxLength + 1 so user can exceed limit by 1 char and see the validation error
        var maxLengthField = targetType.GetField("MaxLength", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        if (maxLengthField is not null && maxLengthField.FieldType == typeof(int))
        {
            var maxLength = (int?)maxLengthField.GetValue(null);
            if (maxLength.HasValue)
            {
                _detectedMaxLength = maxLength.Value;
                // Allow 1 extra char so Value Object throws exception and user sees the error
                MaxLength = maxLength.Value + 1;
            }
        }
    }
    
    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        
        // If Counter is 0 and we detected a MaxLength from VO, use it
        if (Counter == 0 && _detectedMaxLength.HasValue)
        {
            Counter = _detectedMaxLength.Value;
        }
        
        // Setup copy button adornment
        if (Copyable)
        {
            Adornment = Adornment.End;
            AdornmentIcon = _isCopied ? Icons.Material.Filled.Check : Icons.Material.Filled.ContentCopy;
            AdornmentColor = _isCopied ? MudBlazor.Color.Success : MudBlazor.Color.Default;
            AdornmentAriaLabel = _isCopied ? "Copied!" : "Copy to clipboard";
            OnAdornmentClick = EventCallback.Factory.Create<MouseEventArgs>(this, CopyToClipboardAsync);
        }
    }
    
    private async Task CopyToClipboardAsync(MouseEventArgs args)
    {
        var text = ReadText;
        if (!string.IsNullOrEmpty(text))
        {
            await JsRuntime.InvokeVoidAsync("mudWindow.copyToClipboard", text);
            
            // Show "Copied!" feedback
            _isCopied = true;
            AdornmentIcon = Icons.Material.Filled.Check;
            AdornmentColor = MudBlazor.Color.Success;
            AdornmentAriaLabel = "Copied!";
            StateHasChanged();
            
            // Reset after 2 seconds
            _copyResetCts?.Cancel();
            _copyResetCts = new CancellationTokenSource();
            try
            {
                await Task.Delay(2000, _copyResetCts.Token);
                _isCopied = false;
                AdornmentIcon = Icons.Material.Filled.ContentCopy;
                AdornmentColor = MudBlazor.Color.Default;
                AdornmentAriaLabel = "Copy to clipboard";
                StateHasChanged();
            }
            catch (TaskCanceledException)
            {
                // Ignore - user clicked again before timeout
            }
        }
    }
    
    /// <summary>
    /// Override to prevent Text from being cleared when there's a conversion error.
    /// When Value is null/default due to conversion failure, we don't want to update Text.
    /// </summary>
    protected override Task UpdateTextPropertyAsync(bool updateValue)
    {
        // Don't update Text if there's a conversion error - preserve user input
        if (ConversionError)
        {
            return Task.CompletedTask;
        }
        
        return base.UpdateTextPropertyAsync(updateValue);
    }
    
    private static IReversibleConverter<T?, string?>? CreateConverterForType(Type targetType, bool isNullable)
    {
        if (targetType == typeof(Title))
            return new ValueObjectConverter<T, Title>(
                v => v.Value,
                s => new Title(s),
                Title.Empty,
                isNullable);
                
        if (targetType == typeof(Description))
            return new ValueObjectConverter<T, Description>(
                v => v.Value,
                s => new Description(s),
                Description.Empty,
                isNullable);
                
        if (targetType == typeof(UrlSlug))
            return new ValueObjectConverter<T, UrlSlug>(
                v => v.Value,
                s => new UrlSlug(s),
                UrlSlug.Empty,
                isNullable);
                
        if (targetType == typeof(Content))
            return new ValueObjectConverter<T, Content>(
                v => v.Value,
                s => new Content(s),
                Content.Empty,
                isNullable);
                
        if (targetType == typeof(Url))
            return new ValueObjectConverter<T, Url>(
                v => v.Value,
                s => new Url(s),
                Url.Empty,
                isNullable);
                
        if (targetType == typeof(Extensions.Culture))
            return new ValueObjectConverter<T, Extensions.Culture>(
                v => v.Value,
                s => new Extensions.Culture(s),
                Extensions.Culture.Empty,
                isNullable);
                
        return null;
    }
}

/// <summary>
/// Converter for Value Objects using MudBlazor v9 IReversibleConverter API.
/// Catches all exceptions from Value Objects and displays user-friendly error messages.
/// Throws ConversionException on validation failure - MudBlazor automatically displays it.
/// </summary>
internal partial class ValueObjectConverter<T, TValueObject> : IReversibleConverter<T?, string?>
    where TValueObject : struct
{
    private readonly Func<TValueObject, string> _getValue;
    private readonly Func<string, TValueObject> _createFromString;
    private readonly TValueObject _emptyValue;
    private readonly bool _isNullable;

    public ValueObjectConverter(
        Func<TValueObject, string> getValue,
        Func<string, TValueObject> createFromString,
        TValueObject emptyValue,
        bool isNullable)
    {
        _getValue = getValue;
        _createFromString = createFromString;
        _emptyValue = emptyValue;
        _isNullable = isNullable;
    }

    /// <summary>
    /// Converts Value Object to string for display (T → string).
    /// </summary>
    public string? Convert(T? value)
    {
        if (value is null)
            return string.Empty;
            
        if (value is TValueObject vo)
        {
            if (vo.Equals(_emptyValue))
                return string.Empty;
            return _getValue(vo);
        }

        return string.Empty;
    }

    /// <summary>
    /// Converts string back to Value Object (string → T).
    /// Throws ConversionException on validation failure - MudBlazor displays the error.
    /// </summary>
    public T? ConvertBack(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ConvertToT(_emptyValue);
        }

        try
        {
            var result = _createFromString(value);
            return ConvertToT(result);
        }
        catch (Exception ex)
        {
            // Extract user-friendly message (remove technical details)
            var errorMessage = CleanErrorMessage(ex.Message);
            
            // Throw ConversionException - MudBlazor v9 catches this and displays the error
            // The text in input is preserved by our UpdateTextPropertyAsync override
            throw new ConversionException(errorMessage, null, ex);
        }
    }
    
    /// <summary>
    /// Removes technical details from exception message.
    /// Removes: "(Parameter 'xxx')" suffix and "Current length: xxx." part
    /// Example: "Title cannot exceed 60 characters. Current length: 110. (Parameter 'value')"
    /// Becomes: "Title cannot exceed 60 characters."
    /// </summary>
    private static string CleanErrorMessage(string message)
    {
        // Remove "(Parameter 'xxx')" suffix
        var parameterIndex = message.LastIndexOf(" (Parameter '", StringComparison.Ordinal);
        if (parameterIndex > 0)
        {
            message = message[..parameterIndex];
        }
        
        // Remove "Current length: xxx." part using regex
        message = CurrentLengthPattern().Replace(message, "");
        
        return message.Trim();
    }
    
    [GeneratedRegex(@"\s*Current length:\s*\d+\.?", RegexOptions.IgnoreCase)]
    private static partial Regex CurrentLengthPattern();
    
    private T? ConvertToT(TValueObject value)
    {
        if (_isNullable)
        {
            // T is Nullable<TValueObject>
            return (T)(object)(TValueObject?)value;
        }
        else
        {
            // T is TValueObject
            return (T)(object)value;
        }
    }
}
