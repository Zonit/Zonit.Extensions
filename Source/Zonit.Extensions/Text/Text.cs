namespace Zonit.Extensions.Text;


public static class Text
{
    public static TextCounter Count(string text)
        => new(text);

    public static TextAnalyzer Analyzer(string text)
        => new(text);


    // AI CONTENT:
    public static HeaderAnalyzer Headers(string html)
        => new(html);
    public static KeywordAnalyzer Keywords(string text)
        => new(text);
    public static SeoAnalyzer Seo(string html)
        => new(html);
}