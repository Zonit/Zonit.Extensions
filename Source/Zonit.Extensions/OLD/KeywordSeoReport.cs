//namespace Zonit.Extensions.Text;

///// <summary>
///// Raport SEO dla słowa kluczowego.
///// </summary>
//public class KeywordSeoReport
//{
//    /// <summary>
//    /// Analizowane słowo kluczowe.
//    /// </summary>
//    public string Keyword { get; set; }

//    /// <summary>
//    /// Liczba wystąpień w tekście.
//    /// </summary>
//    public int Occurrences { get; set; }

//    /// <summary>
//    /// Gęstość słowa kluczowego.
//    /// </summary>
//    public double Density { get; set; }

//    /// <summary>
//    /// Lista pozycji słowa kluczowego w tekście.
//    /// </summary>
//    public List<int> Positions { get; set; } = new List<int>();

//    /// <summary>
//    /// Czy słowo kluczowe występuje w pierwszym akapicie.
//    /// </summary>
//    public bool IsInFirstParagraph { get; set; }

//    /// <summary>
//    /// Liczba wystąpień w nagłówkach.
//    /// </summary>
//    public int OccurrencesInHeaders { get; set; }

//    /// <summary>
//    /// Sugestie dotyczące optymalizacji.
//    /// </summary>
//    public List<string> Suggestions => GenerateSuggestions();

//    /// <summary>
//    /// Generuje sugestie SEO na podstawie analizy.
//    /// </summary>
//    private List<string> GenerateSuggestions()
//    {
//        var suggestions = new List<string>();

//        // Gęstość słowa kluczowego
//        if (Density < 0.5)
//        {
//            suggestions.Add($"Gęstość słowa kluczowego '{Keyword}' jest zbyt niska ({Density:F2}%). Zalecane jest zwiększenie do 1-3%.");
//        }
//        else if (Density > 3.0)
//        {
//            suggestions.Add($"Gęstość słowa kluczowego '{Keyword}' jest zbyt wysoka ({Density:F2}%). Zalecane jest zmniejszenie do 1-3% aby uniknąć nadoptymalizacji.");
//        }

//        // Występowanie w pierwszym akapicie
//        if (!IsInFirstParagraph)
//        {
//            suggestions.Add($"Słowo kluczowe '{Keyword}' nie występuje w pierwszym akapicie. Zalecane jest umieszczenie go na początku treści.");
//        }

//        // Występowanie w nagłówkach
//        if (OccurrencesInHeaders == 0)
//        {
//            suggestions.Add($"Słowo kluczowe '{Keyword}' nie występuje w żadnym nagłówku. Zaleca się umieszczenie go przynajmniej w jednym nagłówku.");
//        }

//        // Rozmieszczenie w tekście
//        if (Positions.Count >= 2)
//        {
//            int textLength = Positions.Last() + Keyword.Length;
//            double averageDistance = textLength / (double)(Positions.Count);
//            bool isEvenly = true;

//            foreach (var position in Positions)
//            {
//                int nearestPosition = Positions.Where(p => p != position)
//                    .OrderBy(p => Math.Abs(p - position))
//                    .First();

//                if (Math.Abs(nearestPosition - position) > 2 * averageDistance)
//                {
//                    isEvenly = false;
//                    break;
//                }
//            }

//            if (!isEvenly)
//            {
//                suggestions.Add($"Słowo kluczowe '{Keyword}' nie jest równomiernie rozmieszczone w tekście. Zaleca się bardziej równomierne rozmieszczenie.");
//            }
//        }

//        return suggestions;
//    }
//}

