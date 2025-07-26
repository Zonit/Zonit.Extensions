using Microsoft.AspNetCore.Components;
using System.Runtime.CompilerServices;
using CulturesTranslated = Zonit.Extensions.Cultures.Translated;

namespace Zonit.Extensions.Website;

/// <summary>
/// Wrapper dla Translated z Zonit.Extensions.Cultures
/// dodający wsparcie dla MarkupString w aplikacjach Blazor
/// </summary>
public readonly struct Translated : IEquatable<Translated>
{
    private readonly CulturesTranslated _inner;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Translated(CulturesTranslated translated) => _inner = translated;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Translated(string text) => _inner = new CulturesTranslated(text);

    // Deleguj wszystkie właściwości do _inner
    public string Value => _inner.Value;
    public bool IsEmpty => _inner.IsEmpty;
    public bool IsNullOrWhiteSpace => _inner.IsNullOrWhiteSpace;

    // ZMIANA: MarkupString ma priorytet jako implicit - będzie używany w @T("text")
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator MarkupString(Translated translated) 
        => new(translated._inner.ToString() ?? string.Empty);

    // String jest explicit - trzeba używać .ToString() dla komponentów
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator string(Translated translated) => translated._inner;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Translated(string text) => new(text);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator CulturesTranslated(Translated translated) => translated._inner;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Translated(CulturesTranslated translated) => new(translated);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Translated(MarkupString markupString)
        => new(markupString.Value ?? string.Empty);

    // Skrótowa metoda dla explicit conversion do MarkupString (już niepotrzebna bo mamy implicit)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MarkupString AsMarkup() => this; // Teraz używa implicit conversion

    // Metoda ToMarkupString() dla zgodności z Blazor patterns
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MarkupString ToMarkupString() => this; // Teraz używa implicit conversion

    public MarkupString ToMarkupString(params object?[]? args)
    {
        var text = _inner.ToString() ?? string.Empty;

        if (args is null || args.Length == 0)
            return this; // Używa implicit conversion

        try
        {
            return new MarkupString(string.Format(text, args));
        }
        catch (FormatException)
        {
            return this; // Używa implicit conversion
        }
        catch (ArgumentNullException)
        {
            return new MarkupString(string.Empty);
        }
    }

    // Deleguj metody
    public override string ToString() => _inner.ToString();
    public bool Equals(Translated other) => _inner.Equals(other._inner);
    public override bool Equals(object? obj) => obj is Translated other && Equals(other);
    public override int GetHashCode() => _inner.GetHashCode();

    public static bool operator ==(Translated left, Translated right) => left.Equals(right);
    public static bool operator !=(Translated left, Translated right) => !left.Equals(right);

    public static readonly Translated Empty = new(CulturesTranslated.Empty);
}