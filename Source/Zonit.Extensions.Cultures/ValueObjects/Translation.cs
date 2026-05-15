namespace Zonit.Extensions;

/// <summary>
/// Result of resolving a translation key for the active culture: a thin, allocation-light
/// wrapper around the rendered string. Lives in <c>Zonit.Extensions.Cultures</c> because
/// the concept only makes sense inside the translation pipeline — every consumer of
/// <see cref="Translation"/> already references the Cultures package transitively
/// through <c>ICultureProvider</c>.
/// </summary>
/// <remarks>
/// <para>The type is deliberately framework-agnostic: no Blazor, no ASP.NET. Web glue
/// (e.g. rendering to <c>MarkupString</c>) is added by <c>Zonit.Extensions.Website</c>
/// via extension methods, so console / mobile / WASM consumers can use translations
/// without dragging in a UI dependency.</para>
///
/// <para>The struct is implicitly convertible to / from <see cref="string"/> so it slots
/// into existing call sites that expect a plain string. Empty input is normalised to
/// <see cref="string.Empty"/> at construction; <see cref="Empty"/> is the canonical
/// "no translation" sentinel.</para>
///
/// <para>Equality is ordinal — translations are technical content, not user-visible
/// labels we want to compare case-insensitively. Hash code mirrors that.</para>
/// </remarks>
public readonly struct Translation(string text) : IEquatable<Translation>
{
    private readonly string _text = text ?? string.Empty;

    /// <summary>The rendered translation text. Never <see langword="null"/>.</summary>
    public string Value => _text;

    /// <summary><see langword="true"/> when there is no rendered text.</summary>
    public bool IsEmpty => string.IsNullOrEmpty(_text);

    /// <summary><see langword="true"/> when the rendered text is whitespace-only.</summary>
    public bool IsNullOrWhiteSpace => string.IsNullOrWhiteSpace(_text);

    public static implicit operator string(Translation translation) => translation._text;

    public static implicit operator Translation(string text) => new(text);

    public override string ToString() => _text;

    public bool Equals(Translation other) => string.Equals(_text, other._text, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is Translation other && Equals(other);

    public override int GetHashCode() => _text?.GetHashCode(StringComparison.Ordinal) ?? 0;

    public static bool operator ==(Translation left, Translation right) => left.Equals(right);

    public static bool operator !=(Translation left, Translation right) => !left.Equals(right);

    /// <summary>Canonical empty translation. Use for "no value" sentinels.</summary>
    public static readonly Translation Empty = new(string.Empty);
}
