using System.Text.RegularExpressions;

namespace Zonit.Extensions.Text;

public static partial class TextNormalizer
{
    // Zachowujemy stare metody statyczne dla wstecznej kompatybilności
    public static string Whitespace(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return WhitespaceRegex().Replace(input, " ");
    }

    public static string HyphensToDash(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return ReplaceHyphensCore(input);
    }

    public static string ReplaceSmartQuotes(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return ReplaceSmartQuotesCore(input);
    }

    public static string NormalizeWhitespace(this string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return WhitespaceRegex().Replace(input, " ");
    }

    public static string NormalizeHyphensToDash(this string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return ReplaceHyphensCore(input);
    }

    public static string NormalizeSmartQuotes(this string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return ReplaceSmartQuotesCore(input);
    }

    #region Core replacements

    // WHY the \uXXXX escapes instead of the characters themselves: this file was once saved
    // through a lossy encoding, which turned the curly quotes U+201C/U+201D into plain ASCII
    // '"'. The replacements then read Replace('"', '"') - identities - so smart-quote
    // normalization silently did nothing for years while still looking correct in a diff.
    // Escapes are ASCII, so no editor or tool can damage them the same way again.

    private const char LeftDoubleQuote = '\u201C';              // left double quotation mark
    private const char RightDoubleQuote = '\u201D';             // right double quotation mark
    private const char DoubleLow9Quote = '\u201E';              // Polish opening quote
    private const char DoubleHighReversed9Quote = '\u201F';     // double high-reversed-9
    private const char LeftSingleQuote = '\u2018';              // left single quotation mark
    private const char RightSingleQuote = '\u2019';             // right single quote / typographic apostrophe
    private const char SingleLow9Quote = '\u201A';              // single low-9
    private const char SingleHighReversed9Quote = '\u201B';     // single high-reversed-9
    private const char EnDash = '\u2013';                       // en dash
    private const char EmDash = '\u2014';                       // em dash

    /// <summary>
    /// Single implementation shared by <see cref="ReplaceSmartQuotes"/> and
    /// <see cref="NormalizeSmartQuotes"/> so the two can never drift apart again.
    /// </summary>
    private static string ReplaceSmartQuotesCore(string input) =>
        input.Replace(LeftDoubleQuote, '"')
             .Replace(RightDoubleQuote, '"')
             .Replace(DoubleLow9Quote, '"')
             .Replace(DoubleHighReversed9Quote, '"')
             .Replace(LeftSingleQuote, '\'')
             .Replace(RightSingleQuote, '\'')
             .Replace(SingleLow9Quote, '\'')
             .Replace(SingleHighReversed9Quote, '\'');

    /// <summary>
    /// Single implementation shared by <see cref="HyphensToDash"/> and
    /// <see cref="NormalizeHyphensToDash"/>.
    /// </summary>
    private static string ReplaceHyphensCore(string input) =>
        input.Replace(EnDash, '-')
             .Replace(EmDash, '-');

    #endregion

    #region Regex Patterns

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    #endregion
}