namespace Zonit.Extensions.Cultures;

public abstract class LanguageModel
{
    /// <summary>
    /// The language tag in BCP 47 standard format
    /// </summary>
    public abstract string Code { get; }

    /// <summary>
    /// The name of the language in English
    /// </summary>
    public abstract string EnglishName { get; }

    /// <summary>
    /// The icon representing the flag of the language's primary country
    /// </summary>
    public abstract string IconFlag { get; }

    /* TODO:
     * - format daty
     * - format czasu
     * - format liczb
     * - alternatywne kody/podobne odmiany np, en-us, en-gb itp.
     * - widocznosć lewy do prawej, prawej do lewej
     * - Native name, nazwa języka w lokalnym języku np Polish -> Polski
     * 
     * Do pomyślenia:
     * - kraje które używają tego języka
     */
}