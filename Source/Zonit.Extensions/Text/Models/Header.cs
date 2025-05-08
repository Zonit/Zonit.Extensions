namespace Zonit.Extensions.Text;

/// <summary>
/// Model reprezentujący pojedynczy nagłówek HTML.
/// </summary>
public class Header
{
    /// <summary>
    /// Poziom nagłówka (1-6).
    /// </summary>
    public int Level { get; }

    /// <summary>
    /// Treść nagłówka.
    /// </summary>
    public string Content { get; }

    /// <summary>
    /// Indeks rozpoczęcia nagłówka w oryginalnym tekście.
    /// </summary>
    public int StartIndex { get; }

    /// <summary>
    /// Indeks zakończenia nagłówka w oryginalnym tekście.
    /// </summary>
    public int EndIndex { get; }

    /// <summary>
    /// Identyfikator nagłówka (generowany z treści jeśli dostępny).
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Oryginalny tag HTML nagłówka.
    /// </summary>
    public string OriginalTag { get; }

    /// <summary>
    /// Inicjalizuje nowy nagłówek.
    /// </summary>
    public Header(int level, string content, int startIndex, int endIndex, string originalTag, string id = null)
    {
        Level = level;
        Content = content?.Trim() ?? string.Empty;
        StartIndex = startIndex;
        EndIndex = endIndex;
        OriginalTag = originalTag;
        Id = id ?? new UrlSlug(Content).Value;
    }

    /// <summary>
    /// Zwraca strukturę nagłówka jako string.
    /// </summary>
    public override string ToString() => $"H{Level}: {Content}";
}
