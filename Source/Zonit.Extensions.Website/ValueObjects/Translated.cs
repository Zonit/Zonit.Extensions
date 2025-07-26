using Microsoft.AspNetCore.Components;
using System.Runtime.CompilerServices;
using CulturesTranslated = Zonit.Extensions.Cultures.Translated;

namespace Zonit.Extensions.Website;

/// <summary>
/// Rozszerzenie dla klasy Translated z Zonit.Extensions.Cultures
/// dodające wsparcie dla MarkupString w aplikacjach Blazor
/// </summary>
public readonly partial struct Translated
{
    /// <summary>
    /// Konwertuje Translated na MarkupString dla renderowania HTML w Blazor
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator MarkupString(Translated translated)
        => new(translated.ToString() ?? string.Empty);

    /// <summary>
    /// Konwertuje MarkupString na Translated
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Translated(MarkupString markupString)
    {
        string text = markupString.Value ?? string.Empty;
        return CreateFromString(text);
    }

    /// <summary>
    /// Konwertuje Zonit.Extensions.Cultures.Translated na Zonit.Extensions.Website.Translated
    /// Używa Unsafe.As dla bezpośredniej konwersji bez boxing/unboxing
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Translated(CulturesTranslated culturesTranslated)
    {
        return Unsafe.As<CulturesTranslated, Translated>(ref culturesTranslated);
    }

    /// <summary>
    /// Konwertuje Zonit.Extensions.Website.Translated na Zonit.Extensions.Cultures.Translated
    /// Używa Unsafe.As dla bezpośredniej konwersji bez boxing/unboxing
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator CulturesTranslated(Translated websiteTranslated)
    {
        return Unsafe.As<Translated, CulturesTranslated>(ref websiteTranslated);
    }

    /// <summary>
    /// Wydajne tworzenie Translated z string przez delegację do kultury
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Translated CreateFromString(string text)
    {
        CulturesTranslated culturesTranslated = text;
        return Unsafe.As<CulturesTranslated, Translated>(ref culturesTranslated);
    }

    /// <summary>
    /// Tworzy MarkupString z obecnego obiektu Translated
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MarkupString ToMarkupString() => new(ToString() ?? string.Empty);

    /// <summary>
    /// Tworzy MarkupString z formatowaniem, używając obecnego tekstu jako szablonu
    /// </summary>
    public MarkupString ToMarkupString(params object?[]? args)
    {
        var text = ToString() ?? string.Empty;

        if (args is null || args.Length == 0)
        {
            return new MarkupString(text);
        }

        try
        {
            return new MarkupString(string.Format(text, args));
        }
        catch (FormatException)
        {
            return new MarkupString(text);
        }
        catch (ArgumentNullException)
        {
            return new MarkupString(string.Empty);
        }
    }
}