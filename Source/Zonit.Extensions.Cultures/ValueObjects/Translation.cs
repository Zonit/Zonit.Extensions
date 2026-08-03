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
/// <see cref="string.Empty"/>; <see cref="Empty"/> is the canonical "no translation"
/// sentinel.</para>
///
/// <para>Null-safety is enforced at the <see cref="Value"/> accessor, not in the primary
/// constructor: a field initialiser does not run for <c>default(Translation)</c>, for an
/// element of <c>new Translation[n]</c>, or for any struct produced by default
/// initialisation, so a constructor-only guard would leave <c>_text</c> null on exactly the
/// paths the "never null" contract is meant to cover. Every member — including
/// <see cref="ToString"/>, the string conversion and equality — therefore reads
/// <see cref="Value"/> rather than the backing field. This mirrors the sibling
/// <see cref="Culture"/> value object. Consequence: <c>default(Translation)</c> and
/// <see cref="Empty"/> are equal and share a hash code.</para>
///
/// <para>Equality is ordinal — translations are technical content, not user-visible
/// labels we want to compare case-insensitively. Hash code mirrors that.</para>
/// </remarks>
public readonly struct Translation(string text) : IEquatable<Translation>
{
    private readonly string? _text = text;

    /// <summary>
    /// The rendered translation text. Never <see langword="null"/> — returns
    /// <see cref="string.Empty"/> for <c>default(Translation)</c> and for a null constructor
    /// argument.
    /// </summary>
    public string Value => _text ?? string.Empty;

    /// <summary><see langword="true"/> when there is no rendered text.</summary>
    public bool IsEmpty => string.IsNullOrEmpty(_text);

    /// <summary><see langword="true"/> when the rendered text is whitespace-only.</summary>
    public bool IsNullOrWhiteSpace => string.IsNullOrWhiteSpace(_text);

    public static implicit operator string(Translation translation) => translation.Value;

    public static implicit operator Translation(string text) => new(text);

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <inheritdoc />
    public bool Equals(Translation other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Translation other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    public static bool operator ==(Translation left, Translation right) => left.Equals(right);

    public static bool operator !=(Translation left, Translation right) => !left.Equals(right);

    /// <summary>Canonical empty translation. Use for "no value" sentinels.</summary>
    public static readonly Translation Empty = new(string.Empty);
}
