using System.Text.RegularExpressions;

namespace Zonit.Extensions.Text;

public sealed partial class TextCounter(string text) : TextBase<TextCounter>(text)
{
    private readonly string Text = text;

    #region Basic Statistics

    /// <summary>
    /// Liczba wszystkich znaków w tekście.
    /// </summary>
    public int Characters
        => Text.Length;

    /// <summary>
    /// Liczba słów w tekście.
    /// </summary>
    public int Words
        => Text.Split(Separators, SplitOptions).Length;

    /// <summary>
    /// Liczba liter w tekście.
    /// </summary>
    public int Letters
        => Text.Count(char.IsLetter);

    /// <summary>
    /// Liczba cyfr w tekście.
    /// </summary>
    public int Numbers
        => Text.Count(char.IsDigit);

    /// <summary>
    /// Liczba znaków specjalnych w tekście.
    /// </summary>
    public int SpecialChars
        => Text.Count(c => !char.IsLetterOrDigit(c));

    /// <summary>
    /// Liczba akapitów w tekście.
    /// </summary>
    public int Paragraphs
         => ParagraphRegex().Matches(Text).Count + 1;

    /// <summary>
    /// Liczba zdań w tekście.
    /// </summary>
    public int Sentences
           => SentenceRegex().Matches(Text).Count;

    /// <summary>
    /// Średnia długość słowa w znakach.
    /// </summary>
    public double AverageWord
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Text))
                return 0;

            string[] words = Text.Split(Separators, SplitOptions);
            return words.Length == 0 ? 0 : words.Average(w => w.Length);
        }
    }

    #endregion


    #region Regex Patterns

    [GeneratedRegex(@"(\r\n|\n|\r){2,}")]
    private static partial Regex ParagraphRegex();

    [GeneratedRegex(@"[.!?]+(?=\s+|$)")]
    private static partial Regex SentenceRegex();

    #endregion
}
