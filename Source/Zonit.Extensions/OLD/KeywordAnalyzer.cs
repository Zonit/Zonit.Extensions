//namespace Zonit.Extensions.Text;

///// <summary>
///// Analizator słów kluczowych.
///// </summary>
//public class KeywordAnalyzer : TextBase<KeywordAnalyzer>
//{
//    private readonly string _text;
//    private readonly string _plainText;
//    private readonly char[] _separators;
//    private readonly StringSplitOptions _splitOptions;

//    /// <summary>
//    /// Inicjalizuje analizator słów kluczowych.
//    /// </summary>
//    /// <param name="text">Tekst do analizy.</param>
//    public KeywordAnalyzer(string text)
//        : this(text, StringSplitOptions.RemoveEmptyEntries)
//    {
//    }

//    /// <summary>
//    /// Inicjalizuje analizator słów kluczowych z niestandardowymi separatorami.
//    /// </summary>
//    /// <param name="text">Tekst do analizy.</param>
//    /// <param name="separators">Separatory słów.</param>
//    /// <param name="splitOptions">Opcje podziału tekstu.</param>
//    public KeywordAnalyzer(string text, StringSplitOptions splitOptions) : base(text)
//    {
//        _text = text ?? string.Empty;
//        _separators = this.Separators;
//        _splitOptions = splitOptions;

//        // Usuwamy tagi HTML dla czystego tekstu
//        _plainText = System.Text.RegularExpressions.Regex.Replace(_text, "<[^>]*>", string.Empty);
//    }

//    /// <summary>
//    /// Znajduje najczęściej występujące słowa.
//    /// </summary>
//    /// <param name="minLength">Minimalna długość słowa.</param>
//    /// <param name="maxCount">Maksymalna liczba słów do zwrócenia.</param>
//    /// <param name="excludeCommonWords">Czy wykluczyć popularne słowa (przyimki, spójniki itp.).</param>
//    /// <returns>Lista najczęściej występujących słów.</returns>
//    public List<Keyword> GetTopKeywords(int minLength = 3, int maxCount = 20, bool excludeCommonWords = true)
//    {
//        var words = _plainText.Split(_separators, _splitOptions)
//            .Where(word => word.Length >= minLength)
//            .Select(word => word.ToLower());

//        if (excludeCommonWords)
//        {
//            words = words.Where(word => !IsCommonWord(word));
//        }

//        var wordCounts = words
//            .GroupBy(word => word)
//            .Select(group => new { Word = group.Key, Count = group.Count() })
//            .OrderByDescending(item => item.Count)
//            .Take(maxCount);

//        int totalWords = _plainText.Split(_separators, _splitOptions).Length;

//        var keywords = new List<Keyword>();
//        foreach (var word in wordCounts)
//        {
//            var keyword = new Keyword(word.Word)
//            {
//                Count = word.Count,
//                Density = (double)word.Count / totalWords * 100
//            };

//            // Znajdź pozycje słowa w tekście
//            int pos = 0;
//            string lowerText = _plainText.ToLower();
//            string lowerWord = word.Word.ToLower();

//            while ((pos = lowerText.IndexOf(lowerWord, pos)) != -1)
//            {
//                keyword.Positions.Add(pos);
//                pos += lowerWord.Length;
//            }

//            keywords.Add(keyword);
//        }

//        return keywords;
//    }

//    /// <summary>
//    /// Analizuje gęstość słów kluczowych.
//    /// </summary>
//    /// <param name="keywords">Lista słów kluczowych do analizy.</param>
//    /// <returns>Słownik z gęstością każdego słowa kluczowego.</returns>
//    public Dictionary<string, double> AnalyzeKeywordDensity(IEnumerable<string> keywords)
//    {
//        var result = new Dictionary<string, double>();
//        var words = _plainText.Split(_separators, _splitOptions);
//        int totalWords = words.Length;

//        foreach (string keyword in keywords)
//        {
//            string lowerKeyword = keyword.ToLower();
//            int count = words.Count(w => w.ToLower() == lowerKeyword);
//            double density = (double)count / totalWords * 100;
//            result[keyword] = density;
//        }

//        return result;
//    }

//    /// <summary>
//    /// Analizuje słowa kluczowe wielowyrazowe (frazy).
//    /// </summary>
//    /// <param name="maxLength">Maksymalna liczba słów w frazie.</param>
//    /// <param name="topCount">Liczba najpopularniejszych fraz do zwrócenia.</param>
//    /// <returns>Lista najpopularniejszych fraz.</returns>
//    public List<(string Phrase, int Count)> AnalyzePhrases(int maxLength = 4, int topCount = 10)
//    {
//        var words = _plainText.Split(_separators, _splitOptions)
//            .Where(w => w.Length >= 3 && !IsCommonWord(w))
//            .ToArray();

//        var phrases = new Dictionary<string, int>();

//        for (int length = 2; length <= maxLength; length++)
//        {
//            for (int i = 0; i <= words.Length - length; i++)
//            {
//                string phrase = string.Join(" ", words.Skip(i).Take(length));
//                if (phrases.ContainsKey(phrase))
//                {
//                    phrases[phrase]++;
//                }
//                else
//                {
//                    phrases[phrase] = 1;
//                }
//            }
//        }

//        return phrases
//            .OrderByDescending(p => p.Value)
//            .Take(topCount)
//            .Select(p => (p.Key, p.Value))
//            .ToList();
//    }

//    /// <summary>
//    /// Analizuje rozmieszczenie słów kluczowych w tekście.
//    /// </summary>
//    /// <param name="keyword">Słowo kluczowe do analizy.</param>
//    /// <returns>Lista pozycji słowa kluczowego w tekście.</returns>
//    public List<int> GetKeywordPositions(string keyword)
//    {
//        var positions = new List<int>();
//        int pos = 0;
//        string lowerText = _plainText.ToLower();
//        string lowerKeyword = keyword.ToLower();

//        while ((pos = lowerText.IndexOf(lowerKeyword, pos)) != -1)
//        {
//            positions.Add(pos);
//            pos += lowerKeyword.Length;
//        }

//        return positions;
//    }

//    /// <summary>
//    /// Generuje raport SEO dla słowa kluczowego.
//    /// </summary>
//    /// <param name="keyword">Słowo kluczowe do analizy.</param>
//    /// <returns>Raport SEO.</returns>
//    public KeywordSeoReport GenerateKeywordReport(string keyword)
//    {
//        var report = new KeywordSeoReport { Keyword = keyword };

//        // Liczba wystąpień
//        string[] words = _plainText.Split(_separators, _splitOptions);
//        int totalWords = words.Length;
//        int count = words.Count(w => w.ToLower() == keyword.ToLower());

//        report.Occurrences = count;
//        report.Density = (double)count / totalWords * 100;

//        // Pozycje w tekście
//        report.Positions = GetKeywordPositions(keyword);

//        // Sprawdzenie czy słowo kluczowe występuje w pierwszym akapicie
//        int firstParagraphEnd = _plainText.IndexOfAny(['\n', '\r']);
//        string firstParagraph = firstParagraphEnd > 0
//            ? _plainText.Substring(0, firstParagraphEnd)
//            : _plainText;

//        report.IsInFirstParagraph = firstParagraph.ToLower().Contains(keyword.ToLower());

//        // Zliczenie wystąpień w nagłówkach
//        if (_text != _plainText)
//        {
//            var headerAnalyzer = new HeaderAnalyzer(_text);
//            var headers = headerAnalyzer.GetHeaders();
//            report.OccurrencesInHeaders = headers.Count(h =>
//                h.Content.ToLower().Contains(keyword.ToLower()));
//        }

//        return report;
//    }

//    /// <summary>
//    /// Sprawdza czy słowo jest powszechnym słowem (przyimek, spójnik itp.).
//    /// </summary>
//    private bool IsCommonWord(string word)
//    {
//        // Popularne słowa w języku polskim, które zwykle nie są istotne jako słowa kluczowe
//        var commonWords = new HashSet<string>
//        {
//            "a", "aby", "ach", "acz", "aczkolwiek", "ale", "ależ", "and", "ani", "aż", "bardziej", "bardzo", "bez", "bo",
//            "bowiem", "by", "byli", "bym", "był", "była", "było", "były", "być", "będzie", "będą", "cali", "cała", "cały",
//            "ci", "cię", "co", "coraz", "coś", "czy", "czyli", "często", "dla", "do", "gdy", "gdyby", "gdyż", "gdzie",
//            "go", "i", "ich", "im", "inne", "iż", "ja", "jak", "jakie", "jako", "je", "jeden", "jednak", "jednym",
//            "jego", "jej", "jest", "jestem", "jeszcze", "jeśli", "jeżeli", "już", "ją", "kiedy", "kierunku", "kto",
//            "która", "które", "którego", "której", "który", "których", "którym", "którzy", "lub", "ma", "mają", "mi",
//            "mnie", "mogą", "może", "można", "mu", "my", "mój", "na", "nad", "nam", "nas", "naszego", "naszych", "natomiast",
//            "nawet", "nic", "nich", "nie", "nigdy", "nim", "niż", "no", "nowe", "np", "nr", "o", "od", "ok", "on", "ona",
//            "one", "oni", "ono", "oraz", "pan", "po", "pod", "podczas", "pomimo", "ponad", "ponieważ", "poprzez", "potem",
//            "potrzebne", "przed", "przede", "przedtem", "przez", "przy", "raz", "razie", "również", "sam", "się", "skąd",
//            "so", "sobie", "sposób", "swoje", "są", "ta", "tak", "taka", "taki", "takich", "takie", "także", "tam", "te",
//            "tego", "tej", "temu", "ten", "teraz", "też", "to", "tobie", "toteż", "trzeba", "tu", "tych", "tylko", "tym",
//            "tys", "tzw", "tę", "u", "w", "wam", "wami", "was", "we", "według", "wiele", "wielu", "więc", "więcej", "wszyscy",
//            "wszystkich", "wszystkie", "wszystkim", "wszystko", "wtedy", "wy", "właśnie", "z", "za", "zapewne", "zawsze",
//            "ze", "znowu", "znów", "został", "żaden", "że", "żeby"
//        };

//        return commonWords.Contains(word.ToLower());
//    }
//}
