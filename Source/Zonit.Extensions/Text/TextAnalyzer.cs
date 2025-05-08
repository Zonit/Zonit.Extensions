using System.Text.RegularExpressions;

namespace Zonit.Extensions.Text;

public sealed partial class TextAnalyzer(string text) : TextBase<TextAnalyzer>(text)
{
    private readonly string Text = text;

    /// <summary>
    /// Oblicza przybliżony czas czytania tekstu.
    /// </summary>
    /// <param name="wordsPerMinute">Średnia prędkość czytania (słów na minutę).</param>
    /// <returns>Przybliżony czas czytania.</returns>
    public TimeSpan ReadingTime
    {
        get {
            var wordsPerMinute = 200;

            if (string.IsNullOrWhiteSpace(Text) || wordsPerMinute <= 0)
                return TimeSpan.Zero;

            int wordCount = Text.Split(Separators, SplitOptions).Length;
            double minutes = wordCount / (double)wordsPerMinute;

            // Typowe znaki interpunkcyjne, które mogą spowalniać czytanie
            int punctuationMarks = Text.Count(c => c == '.' || c == ',' || c == ';' || c == ':' || c == '!' || c == '?');
            double additionalSeconds = punctuationMarks * 0.5;

            return TimeSpan.FromSeconds(minutes * 60 + additionalSeconds);
        }
    }

    public Dictionary<string, int> CountWordOccurrences(bool caseSensitive = false)
    {
        if (string.IsNullOrWhiteSpace(Text))
            return [];

        string processedText = caseSensitive ? Text : Text.ToLower();
        string[] words = processedText.Split(Separators, SplitOptions);

        var wordOccurrences = new Dictionary<string, int>();

        foreach (string word in words)
        {
            if (wordOccurrences.TryGetValue(word, out int value))
                wordOccurrences[word] = value + 1;
            else
                wordOccurrences[word] = 1;
        }

        return wordOccurrences;
    }



    private int EstimateSyllables()
    {
        var words = Text.Split(Separators, SplitOptions);
        int total = 0;

        // Dodano polskie samogłoski: ąęółśźćń
        var vowelRegex = new Regex("[aeiouyAEIOUYąęółśźćńĄĘÓŁŚŹĆŃ]+");
        foreach (var word in words)
        {
            int count = vowelRegex.Matches(word).Count;
            total += Math.Max(1, count);
        }

        return total;
    }



    #region Advanced Analysis

    /// <summary>
    /// Wskaźnik czytelności tekstu za pomocą wskaźnika Flesch-Kincaid.
    /// Wyższe wartości (bliżej 100) oznaczają łatwiejszy tekst, niższe trudniejszy.
    /// </summary>
    public double ReadabilityScore
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Text))
                return 0;

            int totalWords = Text.Split(Separators, SplitOptions).Length;
            int totalSentences = Math.Max(1, SentenceRegex().Matches(Text).Count);
            int totalSyllables = EstimateSyllables();

            // Formuła Flesch Reading-Ease
            double score = 206.835 - 1.015 * (totalWords / (double)totalSentences) - 84.6 * (totalSyllables / (double)totalWords);

            // Ograniczamy wynik do zakresu 0-100
            return Math.Max(0, Math.Min(100, score));
        }
    }

    /// <summary>
    /// Wskaźnik złożoności słownictwa na podstawie długości słów.
    /// </summary>
    public double VocabularyComplexity
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Text))
                return 0;

            var words = Text.Split(Separators, SplitOptions);
            if (words.Length == 0)
                return 0;

            var uniqueWords = new HashSet<string>(
                words.Select(w => w.ToLower().Trim()),
                StringComparer.OrdinalIgnoreCase
            );

            // Stosunek unikalnych słów do wszystkich słów
            double uniqueRatio = uniqueWords.Count / (double)words.Length;

            // Średnia długość słów
            double avgWordLength = words.Average(w => w.Length);

            // Procent długich słów (powyżej 6 znaków)
            double longWordsPercent = words.Count(w => w.Length > 6) / (double)words.Length;

            // Łączony wskaźnik złożoności (0-100)
            return Math.Min(100, (uniqueRatio * 33) + (avgWordLength * 3) + (longWordsPercent * 40));
        }
    }

    #endregion

    [GeneratedRegex(@"[.!?]+(?=\s+|$)")]
    private static partial Regex SentenceRegex();
}
