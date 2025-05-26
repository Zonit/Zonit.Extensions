//using System.Text.RegularExpressions;

//namespace Zonit.Extensions.Text;

///// <summary>
///// Kompleksowy analizator SEO.
///// </summary>
//public class SeoAnalyzer
//{
//    private readonly string _html;
//    private readonly HeaderAnalyzer _headerAnalyzer;
//    private readonly KeywordAnalyzer _keywordAnalyzer;
//    private readonly TextCounter _textCount;
//    private readonly TextAnalyzer _textAnalyzer;

//    /// <summary>
//    /// Inicjalizuje analizator SEO.
//    /// </summary>
//    /// <param name="html">Kod HTML do analizy.</param>
//    public SeoAnalyzer(string html)
//    {
//        _html = html ?? string.Empty;

//        // Usuń tagi HTML do analizy czystego tekstu
//        string plainText = Regex.Replace(html, "<[^>]*>", string.Empty);

//        _headerAnalyzer = new HeaderAnalyzer(html);
//        _keywordAnalyzer = new KeywordAnalyzer(plainText);
//        _textCount = new TextCounter(plainText);
//        _textAnalyzer = new TextAnalyzer(plainText);
//    }

//    /// <summary>
//    /// Generuje pełny raport SEO.
//    /// </summary>
//    /// <returns>Raport SEO.</returns>
//    public SeoReport GenerateReport()
//    {
//        var report = new SeoReport();

//        // Analiza nagłówków i ich struktury
//        report.HeaderAnalysis = _headerAnalyzer.AnalyzeStructure();
//        report.Headers = _headerAnalyzer.GetHeaders();
//        report.HeaderHierarchy = _headerAnalyzer.GetHeadersHierarchy();

//        // Analiza tekstu
//        report.TextLength = _textCount.Characters;
//        report.WordCount = _textCount.Words;
//        report.ReadabilityScore = _textAnalyzer.ReadabilityScore;
//        report.ReadingTime = _textAnalyzer.ReadingTime.Seconds;

//        // Analiza słów kluczowych
//        report.TopKeywords = _keywordAnalyzer.GetTopKeywords();
//        report.TopPhrases = _keywordAnalyzer.AnalyzePhrases();

//        // Grupowanie słów kluczowych według gęstości
//        if (report.TopKeywords.Count > 0)
//        {
//            // Grupuj słowa kluczowe według przedziałów gęstości
//            report.KeywordsByDensity = GroupKeywordsByDensity(report.TopKeywords);

//            // Identyfikacja głównego słowa kluczowego
//            var mainKeyword = report.TopKeywords[0];
//            report.MainKeywordReport = _keywordAnalyzer.GenerateKeywordReport(mainKeyword.Word);
//        }

//        // Dodatkowe sprawdzenia i reguły SEO
//        report.Issues = GenerateSeoIssues();
//        report.Recommendations = GenerateRecommendations();

//        return report;
//    }

//    /// <summary>
//    /// Grupuje słowa kluczowe według przedziałów gęstości.
//    /// </summary>
//    /// <param name="keywords">Lista słów kluczowych do pogrupowania.</param>
//    /// <returns>Słownik z pogrupowanymi słowami kluczowymi.</returns>
//    private Dictionary<string, List<Keyword>> GroupKeywordsByDensity(List<Keyword> keywords)
//    {
//        var result = new Dictionary<string, List<Keyword>>
//        {
//            { "Niskie (< 0.5%)", new List<Keyword>() },
//            { "Optymalne (0.5% - 2%)", new List<Keyword>() },
//            { "Wysokie (2% - 3%)", new List<Keyword>() },
//            { "Nadmiernie wysokie (> 3%)", new List<Keyword>() }
//        };

//        foreach (var keyword in keywords)
//        {
//            if (keyword.Density < 0.5)
//            {
//                result["Niskie (< 0.5%)"].Add(keyword);
//            }
//            else if (keyword.Density <= 2.0)
//            {
//                result["Optymalne (0.5% - 2%)"].Add(keyword);
//            }
//            else if (keyword.Density <= 3.0)
//            {
//                result["Wysokie (2% - 3%)"].Add(keyword);
//            }
//            else
//            {
//                result["Nadmiernie wysokie (> 3%)"].Add(keyword);
//            }
//        }

//        return result;
//    }

//    // Pozostała część klasy pozostaje bez zmian...

//    /// <summary>
//    /// Generuje listę problemów SEO.
//    /// </summary>
//    private List<string> GenerateSeoIssues()
//    {
//        var issues = new List<string>();

//        // Sprawdź długość tekstu
//        int wordCount = _textCount.Words;
//        if (wordCount < 300)
//        {
//            issues.Add($"Tekst jest zbyt krótki ({wordCount} słów). Zalecane minimum to 300 słów dla dobrej optymalizacji SEO.");
//        }

//        // Sprawdź nagłówki
//        var headers = _headerAnalyzer.GetHeaders();
//        if (!headers.Any(h => h.Level == 1))
//        {
//            issues.Add("Brak nagłówka H1. Każda strona powinna zawierać dokładnie jeden nagłówek H1.");
//        }
//        else if (headers.Count(h => h.Level == 1) > 1)
//        {
//            issues.Add($"Strona zawiera {headers.Count(h => h.Level == 1)} nagłówków H1. Zalecane jest używanie tylko jednego H1.");
//        }

//        // Sprawdź czytelność
//        double readabilityScore = _textAnalyzer.ReadabilityScore;
//        if (readabilityScore < 30)
//        {
//            issues.Add($"Wskaźnik czytelności tekstu jest niski ({readabilityScore:F1}/100). Tekst może być trudny do zrozumienia.");
//        }

//        // Sprawdź meta tagi
//        if (_html.Contains("<meta"))
//        {
//            if (!Regex.IsMatch(_html, "<meta[^>]*name=[\"']description[\"'][^>]*>", RegexOptions.IgnoreCase))
//            {
//                issues.Add("Brak meta opisu (description). Meta opis jest ważnym elementem SEO.");
//            }

//            var descMatch = Regex.Match(_html, "<meta[^>]*name=[\"']description[\"'][^>]*content=[\"']([^\"']*)[\"']", RegexOptions.IgnoreCase);
//            if (descMatch.Success && descMatch.Groups[1].Value.Length < 50)
//            {
//                issues.Add($"Meta opis jest zbyt krótki ({descMatch.Groups[1].Value.Length} znaków). Zalecana długość to 150-160 znaków.");
//            }
//        }

//        return issues;
//    }

//    /// <summary>
//    /// Generuje rekomendacje SEO.
//    /// </summary>
//    private List<string> GenerateRecommendations()
//    {
//        var recommendations = new List<string>();

//        // Rekomendacje dotyczące długości tekstu
//        int wordCount = _textCount.Words;
//        if (wordCount < 600)
//        {
//            recommendations.Add($"Rozważ wydłużenie tekstu. Obecnie ma {wordCount} słów, a treści o długości 600+ słów zwykle lepiej pozycjonują się w wyszukiwarkach.");
//        }

//        // Rekomendacje dotyczące słów kluczowych
//        var topKeywords = _keywordAnalyzer.GetTopKeywords(3, 5);
//        if (topKeywords.Count > 0)
//        {
//            var mainKeyword = topKeywords[0];

//            if (mainKeyword.Density < 1.0)
//            {
//                recommendations.Add($"Zwiększ gęstość głównego słowa kluczowego '{mainKeyword.Word}' z {mainKeyword.Density:F2}% do 1-2%.");
//            }

//            // Sprawdź czy słowo kluczowe jest w nagłówkach
//            var headers = _headerAnalyzer.GetHeaders();
//            bool keywordInHeader = headers.Any(h => h.Content.ToLower().Contains(mainKeyword.Word.ToLower()));

//            if (!keywordInHeader)
//            {
//                recommendations.Add($"Umieść główne słowo kluczowe '{mainKeyword.Word}' w przynajmniej jednym nagłówku.");
//            }

//            // Sprawdź, czy słowa kluczowe są używane w wariantach
//            recommendations.Add($"Rozważ użycie wariantów głównego słowa kluczowego: '{mainKeyword.Word}' (np. synonimy, odmiany).");
//        }

//        // Rekomendacje dotyczące struktury
//        var headerStructure = _headerAnalyzer.AnalyzeStructure();
//        if (headerStructure.Issues.Count > 0)
//        {
//            recommendations.Add("Popraw strukturę nagłówków dla lepszej hierarchii treści i indeksowania.");
//        }

//        // Rekomendacje dotyczące czytelności
//        double readabilityScore = _textAnalyzer.ReadabilityScore;
//        if (readabilityScore < 60)
//        {
//            recommendations.Add("Uprość zdania i używaj krótszych słów, aby zwiększyć czytelność tekstu.");
//        }

//        return recommendations;
//    }

//    /// <summary>
//    /// Analizuje słowo kluczowe i generuje raport.
//    /// </summary>
//    /// <param name="keyword">Słowo kluczowe do analizy.</param>
//    /// <returns>Raport SEO dla słowa kluczowego.</returns>
//    public KeywordSeoReport AnalyzeKeyword(string keyword)
//    {
//        return _keywordAnalyzer.GenerateKeywordReport(keyword);
//    }

//    /// <summary>
//    /// Sprawdza czy treść spełnia określone kryteria SEO.
//    /// </summary>
//    /// <param name="mainKeyword">Główne słowo kluczowe.</param>
//    /// <param name="minWords">Minimalna liczba słów.</param>
//    /// <param name="minReadabilityScore">Minimalny wskaźnik czytelności.</param>
//    /// <returns>Informacja czy treść spełnia kryteria SEO.</returns>
//    public (bool IsSeoFriendly, List<string> Reasons) CheckSeoFriendliness(
//        string mainKeyword, int minWords = 300, double minReadabilityScore = 60)
//    {
//        var reasons = new List<string>();
//        bool isSeoFriendly = true;

//        // Sprawdź długość tekstu
//        int wordCount = _textCount.Words;
//        if (wordCount < minWords)
//        {
//            reasons.Add($"Tekst jest zbyt krótki ({wordCount} słów, minimum: {minWords}).");
//            isSeoFriendly = false;
//        }

//        // Sprawdź nagłówki
//        var headers = _headerAnalyzer.GetHeaders();
//        if (headers.Count(h => h.Level == 1) != 1)
//        {
//            reasons.Add($"Nieprawidłowa liczba nagłówków H1: {headers.Count(h => h.Level == 1)} (powinno być dokładnie 1).");
//            isSeoFriendly = false;
//        }

//        // Sprawdź słowo kluczowe
//        if (!string.IsNullOrEmpty(mainKeyword))
//        {
//            var keywordReport = _keywordAnalyzer.GenerateKeywordReport(mainKeyword);

//            if (keywordReport.Density < 0.5 || keywordReport.Density > 3.0)
//            {
//                reasons.Add($"Nieprawidłowa gęstość słowa kluczowego '{mainKeyword}': {keywordReport.Density:F2}% (zalecane: 1-3%).");
//                isSeoFriendly = false;
//            }

//            if (!keywordReport.IsInFirstParagraph)
//            {
//                reasons.Add($"Słowo kluczowe '{mainKeyword}' nie występuje w pierwszym akapicie.");
//                isSeoFriendly = false;
//            }

//            if (keywordReport.OccurrencesInHeaders == 0)
//            {
//                reasons.Add($"Słowo kluczowe '{mainKeyword}' nie występuje w żadnym nagłówku.");
//                isSeoFriendly = false;
//            }
//        }

//        // Sprawdź czytelność
//        double readabilityScore = _textAnalyzer.ReadabilityScore;
//        if (readabilityScore < minReadabilityScore)
//        {
//            reasons.Add($"Zbyt niski wskaźnik czytelności: {readabilityScore:F1}/100 (minimum: {minReadabilityScore}).");
//            isSeoFriendly = false;
//        }

//        return (isSeoFriendly, reasons);
//    }
//}
