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

        return input.Replace('–', '-').Replace('—', '-');
    }

    public static string ReplaceSmartQuotes(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return input.Replace('"', '"').Replace('"', '"').Replace('„', '"');
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

        return input.Replace('–', '-').Replace('—', '-');
    }

    public static string NormalizeSmartQuotes(this string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return input.Replace('"', '"').Replace('"', '"').Replace('„', '"');
    }

    #region Regex Patterns

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    #endregion
}