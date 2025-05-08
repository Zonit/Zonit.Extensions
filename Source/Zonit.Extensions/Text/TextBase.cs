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

    protected T Create(string text)
    {
        var constructor = typeof(T).GetConstructor([typeof(string)]);

        return constructor is null
            ? throw new InvalidOperationException("Constructor that takes a string parameter not found.")
            : (T)constructor.Invoke([text]);
    }

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