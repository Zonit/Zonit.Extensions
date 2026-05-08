using System.Text.RegularExpressions;

namespace Zonit.Extensions.Text;

public abstract partial class TextBase<T>(string text) where T : TextBase<T>
{
    protected internal char[] Separators { get; private set; }
        = [' ', '.', '!', '?', ';', ':', ',', '"', '\r', '\n'];

    protected internal StringSplitOptions SplitOptions { get; private set; }
        = StringSplitOptions.RemoveEmptyEntries;

    public T RemoveHtml =>
        Create(HtmlTagRegex().Replace(text, string.Empty));

    public T RemoveSpecialChars =>
        Create(SpecialCharRegex().Replace(text, string.Empty));

    public T NormalizeWhitespace =>
        Create(WhitespaceRegex().Replace(text.Trim(), " "));

    /// <summary>
    /// Factory method that subclasses must implement to construct a new instance with the given text.
    /// Replaces the previous reflection-based implementation for full AOT/trimming compatibility.
    /// </summary>
    /// <param name="text">The text content for the new instance.</param>
    /// <returns>A new <typeparamref name="T"/> instance wrapping <paramref name="text"/>.</returns>
    protected abstract T Create(string text);

    public override string ToString() => text;
    public string Result => text;

    #region Configuration

    /// <summary>
    /// Ustawia własne separatory słów.
    /// </summary>
    /// <param name="separators">Znaki separujące słowa.</param>
    /// <returns>Ten sam analizator z nowymi ustawieniami.</returns>
    public T WithSeparators(params char[] separators)
    {
        Separators = separators;
        return Create(text);
    }

    /// <summary>
    /// Ustawia opcje podziału tekstu.
    /// </summary>
    /// <param name="options">Opcje podziału.</param>
    /// <returns>Ten sam analizator z nowymi ustawieniami.</returns>
    public T WithSplitOptions(StringSplitOptions options)
    {
        SplitOptions = options;
        return Create(text);
    }
    #endregion

    [GeneratedRegex("<.*?>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"[^\w\s]", RegexOptions.Compiled)]
    private static partial Regex SpecialCharRegex();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();
}