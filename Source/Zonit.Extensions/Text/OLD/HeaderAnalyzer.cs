using System.Text;
using System.Text.RegularExpressions;
using Zonit.Extensions.Text.OLD.Models;

namespace Zonit.Extensions.Text;

/// <summary>
/// Analizator nagłówków HTML.
/// </summary>
public partial class HeaderAnalyzer
{
    private readonly string _html;
    private List<Header> _headers;

    /// <summary>
    /// Inicjalizuje analizator nagłówków.
    /// </summary>
    /// <param name="html">Tekst HTML do analizy.</param>
    public HeaderAnalyzer(string html)
    {
        _html = html ?? string.Empty;
    }

    /// <summary>
    /// Pobiera wszystkie nagłówki z dokumentu HTML.
    /// </summary>
    /// <returns>Lista nagłówków.</returns>
    public IReadOnlyList<Header> GetHeaders()
    {
        if (_headers == null)
        {
            _headers = new List<Header>();

            // Znajdowanie wszystkich nagłówków H1-H6
            foreach (Match match in HeaderRegex().Matches(_html))
            {
                string tagName = match.Groups["tag"].Value.ToLower();
                // Poprawka: Usuwamy NextSubstring, ponieważ tagName już zawiera cyfrę
                if (!int.TryParse(tagName, out int level))
                {
                    throw new FormatException($"Nieprawidłowy poziom nagłówka: {tagName}");
                }
                string content = match.Groups["content"].Value.Trim();
                string id = match.Groups["id"].Success ? match.Groups["id"].Value : null;

                _headers.Add(new Header(level, content, match.Index, match.Index + match.Length, match.Value, id));
            }
        }

        return _headers;
    }


    /// <summary>
    /// Filtruje nagłówki według poziomu.
    /// </summary>
    /// <param name="level">Poziom nagłówka (1-6).</param>
    /// <returns>Lista nagłówków danego poziomu.</returns>
    public IEnumerable<Header> GetHeadersByLevel(int level)
    {
        return GetHeaders().Where(h => h.Level == level);
    }

    /// <summary>
    /// Pobiera strukturę hierarchiczną nagłówków.
    /// </summary>
    /// <returns>Lista nagłówków z informacją o ich zagnieżdżeniu.</returns>
    public List<(Header Header, int Depth)> GetHeadersHierarchy()
    {
        var headers = GetHeaders();
        var result = new List<(Header Header, int Depth)>();
        var levelStack = new Stack<int>();

        foreach (var header in headers)
        {
            // Wyczyść stos dla wyższych poziomów
            while (levelStack.Count > 0 && levelStack.Peek() >= header.Level)
            {
                levelStack.Pop();
            }

            // Dodaj aktualny poziom do stosu
            levelStack.Push(header.Level);

            // Głębokość to liczba elementów na stosie minus 1
            result.Add((header, levelStack.Count - 1));
        }

        return result;
    }

    /// <summary>
    /// Generuje spis treści na podstawie nagłówków.
    /// </summary>
    /// <returns>HTML ze spisem treści.</returns>
    public string GenerateTableOfContents()
    {
        var hierarchy = GetHeadersHierarchy();
        var sb = new StringBuilder();

        sb.AppendLine("<ul class=\"table-of-contents\">");

        int previousDepth = 0;

        foreach (var (header, depth) in hierarchy)
        {
            if (depth > previousDepth)
            {
                // Zwiększ poziom zagnieżdżenia
                sb.AppendLine("<ul>");
            }
            else if (depth < previousDepth)
            {
                // Zmniejsz poziom zagnieżdżenia
                for (int i = 0; i < previousDepth - depth; i++)
                {
                    sb.AppendLine("</ul></li>");
                }
            }
            else if (previousDepth > 0)
            {
                // Zakończ poprzedni element
                sb.AppendLine("</li>");
            }

            // Dodaj element
            sb.Append($"<li><a href=\"#{header.Id}\">{header.Content}</a>");

            previousDepth = depth;
        }

        // Zamknij wszystkie otwarte tagi
        for (int i = 0; i < previousDepth; i++)
        {
            sb.AppendLine("</li></ul>");
        }

        sb.AppendLine("</li></ul>");

        return sb.ToString();
    }

    /// <summary>
    /// Sprawdza poprawność struktury nagłówków.
    /// </summary>
    /// <returns>True jeśli struktura jest poprawna, False w przeciwnym wypadku.</returns>
    public bool ValidateHeaderStructure()
    {
        var headers = GetHeaders();
        if (headers.Count == 0) return true;

        // Sprawdź czy dokument zaczyna się od H1
        if (headers.First().Level != 1) return false;

        // Sprawdź czy poziomy nagłówków nie przeskakują o więcej niż 1
        for (int i = 1; i < headers.Count; i++)
        {
            if (headers[i].Level > headers[i - 1].Level + 1) return false;
        }

        return true;
    }

    /// <summary>
    /// Analizuje strukturę nagłówków i generuje raport.
    /// </summary>
    /// <returns>Raport z analizy nagłówków.</returns>
    public HeaderAnalysisReport AnalyzeStructure()
    {
        var headers = GetHeaders();
        var report = new HeaderAnalysisReport();

        if (headers.Count == 0)
        {
            report.Issues.Add("Brak nagłówków w dokumencie.");
            return report;
        }

        // Sprawdzenie obecności H1
        var h1Headers = headers.Where(h => h.Level == 1).ToList();
        if (h1Headers.Count == 0)
        {
            report.Issues.Add("Dokument nie zawiera nagłówka H1.");
        }
        else if (h1Headers.Count > 1)
        {
            report.Issues.Add($"Dokument zawiera {h1Headers.Count} nagłówków H1. Zalecane jest użycie tylko jednego H1.");
        }

        // Sprawdzenie ciągłości nagłówków
        for (int i = 1; i < headers.Count; i++)
        {
            if (headers[i].Level > headers[i - 1].Level + 1)
            {
                report.Issues.Add($"Nieprawidłowa hierarchia: przejście z H{headers[i - 1].Level} do H{headers[i].Level} bez pośrednich poziomów.");
            }
        }

        // Sprawdzenie zbyt długich nagłówków
        foreach (var header in headers)
        {
            if (header.Content.Length > 70)
            {
                report.Issues.Add($"Nagłówek H{header.Level} \"{header.Content.Substring(0, 50)}...\" jest zbyt długi ({header.Content.Length} znaków).");
            }
        }

        // Dodaj szczegóły struktury
        report.HeadersCount = headers.Count;
        report.StructureDepth = headers.Max(h => h.Level);
        report.HeadersByLevel = Enumerable.Range(1, 6)
            .ToDictionary(level => level, level => headers.Count(h => h.Level == level));

        return report;
    }

    [GeneratedRegex("<h(?<tag>[1-6])(?:\\s+[^>]*?id=[\"'](?<id>[^\"']+)[\"'][^>]*?|[^>]*)>(?<content>.*?)</h\\1>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex HeaderRegex();
}
