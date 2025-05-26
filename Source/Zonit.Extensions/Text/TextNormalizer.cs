using System.Text.RegularExpressions;

namespace Zonit.Extensions.Text;

public partial class TextNormalizer
{
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

        return input.Replace('“', '"').Replace('”', '"').Replace('„', '"');
    }

    #region Regex Patterns

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    #endregion
}