namespace Zonit.Extensions;

/// <summary>
/// Value object reprezentujący przetłumaczony tekst, który może być automatycznie 
/// konwertowany na MarkupString i string bez jawnych wywołań .ToString()
/// </summary>
public readonly partial struct Translated(string text) : IEquatable<Translated>
{
    private readonly string _text = text ?? string.Empty;

    public string Value => _text;
    
    public bool IsEmpty => string.IsNullOrEmpty(_text);
    
    public bool IsNullOrWhiteSpace => string.IsNullOrWhiteSpace(_text);

    public static implicit operator string(Translated translated) => translated._text;

    public static implicit operator Translated(string text) => new(text);

    public override string ToString() => _text;

    public bool Equals(Translated other) => string.Equals(_text, other._text, StringComparison.Ordinal);
    
    public override bool Equals(object? obj) => obj is Translated other && Equals(other);
    
    public override int GetHashCode() => _text?.GetHashCode() ?? 0;

    public static bool operator ==(Translated left, Translated right) => left.Equals(right);
    
    public static bool operator !=(Translated left, Translated right) => !left.Equals(right);

    public static readonly Translated Empty = new(string.Empty);
}