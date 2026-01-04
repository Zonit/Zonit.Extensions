using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Zonit.Extensions;

/// <summary>
/// Base exception class without generics - allows manual setting of all properties.
/// Use this for custom exceptions with strongly-typed properties.
/// </summary>
public abstract class BaseException : Exception
{
    /// <summary>
    /// Localization key (e.g., "Wallets.NotFound")
    /// </summary>
    public string ErrorKey { get; }
    
    /// <summary>
    /// Message template with placeholders (e.g., "Wallet {0} was not found")
    /// IMPORTANT: Always use placeholders {0}, {1} instead of string interpolation
    /// to enable later formatting with translations.
    /// </summary>
    public string Template { get; }
    
    /// <summary>
    /// Additional error parameters (for template formatting and logging)
    /// </summary>
    public object[]? Parameters { get; }

    /// <summary>
    /// Main constructor - creates exception with template and parameters
    /// </summary>
    /// <param name="errorKey">Error key for localization (e.g., "Wallets.NotFound")</param>
    /// <param name="template">Message template with placeholders {0}, {1}, etc.</param>
    /// <param name="parameters">Parameters to substitute in the template</param>
    protected BaseException(string errorKey, string template, params object[] parameters)
        : base(FormatTemplate(template, parameters))
    {
        ErrorKey = errorKey;
        Template = template;
        Parameters = parameters.Length > 0 ? parameters : null;
    }

    /// <summary>
    /// Formats template with parameters, handling formatting errors gracefully
    /// </summary>
    protected static string FormatTemplate(string template, object[] parameters)
    {
        if (parameters.Length > 0)
        {
            try
            {
                return string.Format(template, parameters);
            }
            catch
            {
                // If formatting fails, return template without formatting
                return template;
            }
        }
        return template;
    }
}

/// <summary>
/// Base exception class with generic error code (enum).
/// Use this for modules with multiple error types.
/// </summary>
public abstract class BaseException<TErrorCode> : BaseException
    where TErrorCode : Enum
{
    /// <summary>
    /// Error code as enum (e.g., WalletErrorCode.NotFound)
    /// </summary>
    public TErrorCode Code { get; }

    /// <summary>
    /// Main constructor - accepts error code and automatically retrieves template from Display attribute
    /// </summary>
    /// <param name="code">Error code (enum value)</param>
    /// <param name="parameters">Parameters to substitute in the template</param>
    protected BaseException(TErrorCode code, params object[] parameters)
        : base(GetErrorKey(code), GetTemplate(code), parameters)
    {
        Code = code;
    }

    /// <summary>
    /// Retrieves Display.Name as localization key
    /// </summary>
    private static string GetErrorKey(TErrorCode code)
    {
        var field = code.GetType().GetField(code.ToString());
        var displayAttr = field?.GetCustomAttribute<DisplayAttribute>();
        return displayAttr?.Name ?? code.ToString();
    }
    
    /// <summary>
    /// Retrieves Display.Description as template
    /// </summary>
    private static string GetTemplate(TErrorCode code)
    {
        var field = code.GetType().GetField(code.ToString());
        var displayAttr = field?.GetCustomAttribute<DisplayAttribute>();
        return displayAttr?.Description ?? displayAttr?.Name ?? code.ToString();
    }
}