namespace Zonit.Extensions.Cultures.Options;

public class CultureOption
{
    /// <summary>
    /// Default culture
    /// </summary>
    public string DefaultCulture { get; set; } = "en-US";

    /// <summary>
    /// Default timezone
    /// </summary>
    public string DefaultTimeZone { get; set; } = "Europe/Warsaw";

    /// <summary>
    /// Supported cultures
    /// </summary>
    public string[] SupportedCultures { get; set; } = [
        "en-us",
        "ar-sa",
        "fr-fr",
        "de-de",
        "es-es",
        "it-it",
        "nl-nl",
        "sv-se",
        "da-dk",
        "no-no",
        "fi-fi",
        "ru-ru",
        "pl-pl",
        "cs-cz",
        "hu-hu",
        "sk-sk",
        "pt-pt"
    ];
}
