using System.Text.RegularExpressions;

namespace Zonit.Extensions.Text;

public abstract partial class TextBase<T>(string text) where T : TextBase<T>
{
    protected internal char[] Separators { get; private set; }
        = [' ', '.', '!', '?', ';', ':', ',', '"', '\r', '\n'];

    protected internal StringSplitOptions SplitOptions { get; private set; }
        = StringSplitOptions.RemoveEmptyEntries;

    public T RemoveHtml =>
        Derive(HtmlTagRegex().Replace(text, string.Empty));

    public T RemoveSpecialChars =>
        Derive(SpecialCharRegex().Replace(text, string.Empty));

    public T NormalizeWhitespace =>
        Derive(WhitespaceRegex().Replace(text.Trim(), " "));

    /// <summary>
    /// Factory method that subclasses must implement to construct a new instance with the given text.
    /// Replaces the previous reflection-based implementation for full AOT/trimming compatibility.
    /// </summary>
    /// <param name="text">The text content for the new instance.</param>
    /// <returns>A new <typeparamref name="T"/> instance wrapping <paramref name="text"/>.</returns>
    /// <remarks>
    /// Implementations only construct - they must not try to carry configuration across.
    /// <see cref="Derive"/> copies <see cref="Separators"/> and <see cref="SplitOptions"/> onto
    /// the result, so every fluent step keeps the settings the caller configured.
    /// </remarks>
    protected abstract T Create(string text);

    /// <summary>
    /// Builds the next instance in a fluent chain: <see cref="Create"/> for the new text, then
    /// this instance's configuration copied over it. Without this step every chained member
    /// silently reverted to the default separators, which is what made
    /// <c>WithSeparators</c> a no-op.
    /// </summary>
    /// <param name="text">The text content for the new instance.</param>
    /// <returns>A configured <typeparamref name="T"/> instance wrapping <paramref name="text"/>.</returns>
    private T Derive(string text)
    {
        var instance = Create(text);
        instance.Separators = Separators;
        instance.SplitOptions = SplitOptions;
        return instance;
    }

    public override string ToString() => text;
    public string Result => text;

    #region Configuration

    /// <summary>
    /// Ustawia własne separatory słów.
    /// </summary>
    /// <param name="separators">Znaki separujące słowa. Pusta tablica oznacza separatory domyślne.</param>
    /// <returns>
    /// Nowy analizator z tym samym tekstem i podanymi separatorami.
    /// Analizator, na którym wywołano metodę, pozostaje niezmieniony.
    /// </returns>
    public T WithSeparators(params char[] separators)
    {
        ArgumentNullException.ThrowIfNull(separators);

        var instance = Create(text);
        // Pusta tablica separatorów sprawiłaby, że string.Split dzieli po białych znakach -
        // czyli po cichu ignoruje konfigurację. Zachowujemy wtedy domyślny zestaw.
        instance.Separators = separators.Length > 0 ? separators : Separators;
        instance.SplitOptions = SplitOptions;
        return instance;
    }

    /// <summary>
    /// Ustawia opcje podziału tekstu.
    /// </summary>
    /// <param name="options">Opcje podziału.</param>
    /// <returns>
    /// Nowy analizator z tym samym tekstem i podanymi opcjami podziału.
    /// Analizator, na którym wywołano metodę, pozostaje niezmieniony.
    /// </returns>
    public T WithSplitOptions(StringSplitOptions options)
    {
        var instance = Create(text);
        instance.Separators = Separators;
        instance.SplitOptions = options;
        return instance;
    }
    #endregion

    [GeneratedRegex("<.*?>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"[^\w\s]", RegexOptions.Compiled)]
    private static partial Regex SpecialCharRegex();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();
}