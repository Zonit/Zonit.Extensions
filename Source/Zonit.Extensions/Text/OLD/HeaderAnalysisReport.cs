namespace Zonit.Extensions.Text;


/// <summary>
/// Raport z analizy struktury nagłówków.
/// </summary>
public class HeaderAnalysisReport
{
    /// <summary>
    /// Łączna liczba nagłówków.
    /// </summary>
    public int HeadersCount { get; set; }

    /// <summary>
    /// Maksymalna głębokość struktury (najwyższy poziom nagłówka).
    /// </summary>
    public int StructureDepth { get; set; }

    /// <summary>
    /// Liczba nagłówków każdego poziomu (1-6).
    /// </summary>
    public Dictionary<int, int> HeadersByLevel { get; set; } = new Dictionary<int, int>();

    /// <summary>
    /// Problemy znalezione w strukturze nagłówków.
    /// </summary>
    public List<string> Issues { get; set; } = new List<string>();

    /// <summary>
    /// Określa czy struktura nagłówków jest prawidłowa (brak problemów).
    /// </summary>
    public bool IsValid => Issues.Count == 0;
}