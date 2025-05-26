//// Modyfikacja klasy SeoReport
//using Zonit.Extensions.Text.OLD.Models;

//namespace Zonit.Extensions.Text;

///// <summary>
///// Pełny raport SEO.
///// </summary>
//public class SeoReport
//{
//    /// <summary>
//    /// Analiza nagłówków.
//    /// </summary>
//    public HeaderAnalysisReport HeaderAnalysis { get; set; }

//    /// <summary>
//    /// Lista nagłówków znalezionych w dokumencie.
//    /// </summary>
//    public IReadOnlyList<Header> Headers { get; set; } = new List<Header>();

//    /// <summary>
//    /// Struktura hierarchiczna nagłówków.
//    /// </summary>
//    public List<(Header Header, int Depth)> HeaderHierarchy { get; set; } = new List<(Header, int)>();

//    /// <summary>
//    /// Długość tekstu w znakach.
//    /// </summary>
//    public int TextLength { get; set; }

//    /// <summary>
//    /// Liczba słów.
//    /// </summary>
//    public int WordCount { get; set; }

//    /// <summary>
//    /// Wskaźnik czytelności tekstu.
//    /// </summary>
//    public double ReadabilityScore { get; set; }

//    /// <summary>
//    /// Czas czytania w sekundach.
//    /// </summary>
//    public int ReadingTime { get; set; }

//    /// <summary>
//    /// Najczęściej występujące słowa kluczowe.
//    /// </summary>
//    public List<Keyword> TopKeywords { get; set; } = new List<Keyword>();

//    /// <summary>
//    /// Najczęściej występujące frazy.
//    /// </summary>
//    public List<(string Phrase, int Count)> TopPhrases { get; set; } = new List<(string, int)>();

//    /// <summary>
//    /// Raport dla głównego słowa kluczowego.
//    /// </summary>
//    public KeywordSeoReport MainKeywordReport { get; set; }

//    /// <summary>
//    /// Słowo kluczowe z największą liczbą wystąpień.
//    /// </summary>
//    public Keyword MostFrequentKeyword => TopKeywords.Count > 0 ? TopKeywords[0] : null;

//    /// <summary>
//    /// Słowa kluczowe pogrupowane według gęstości występowania.
//    /// </summary>
//    public Dictionary<string, List<Keyword>> KeywordsByDensity { get; set; } = new Dictionary<string, List<Keyword>>();

//    /// <summary>
//    /// Problemy SEO.
//    /// </summary>
//    public List<string> Issues { get; set; } = new List<string>();

//    /// <summary>
//    /// Rekomendacje SEO.
//    /// </summary>
//    public List<string> Recommendations { get; set; } = new List<string>();

//    /// <summary>
//    /// Ocena ogólna SEO (0-100).
//    /// </summary>
//    public int OverallScore => CalculateOverallScore();

//    /// <summary>
//    /// Oblicza ogólną ocenę SEO.
//    /// </summary>
//    private int CalculateOverallScore()
//    {
//        int score = 100;

//        // Odejmij punkty za problemy
//        score -= Issues.Count * 10;

//        // Sprawdź długość tekstu
//        if (WordCount < 300) score -= 20;
//        else if (WordCount < 600) score -= 10;

//        // Sprawdź czytelność
//        if (ReadabilityScore < 30) score -= 20;
//        else if (ReadabilityScore < 60) score -= 10;

//        // Sprawdź nagłówki
//        if (HeaderAnalysis != null)
//        {
//            if (HeaderAnalysis.HeadersByLevel.GetValueOrDefault(1) != 1) score -= 15;
//            if (HeaderAnalysis.Issues.Count > 0) score -= 5 * HeaderAnalysis.Issues.Count;
//        }

//        // Sprawdź słowo kluczowe
//        if (MainKeywordReport != null)
//        {
//            if (MainKeywordReport.Density < 0.5 || MainKeywordReport.Density > 3.0) score -= 15;
//            if (!MainKeywordReport.IsInFirstParagraph) score -= 10;
//            if (MainKeywordReport.OccurrencesInHeaders == 0) score -= 10;
//        }

//        return Math.Max(0, Math.Min(100, score));
//    }
//}
