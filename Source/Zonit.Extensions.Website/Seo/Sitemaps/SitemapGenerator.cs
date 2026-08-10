using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text;
using System.Xml;
using Zonit.Extensions.Website.Cultures;

namespace Zonit.Extensions.Website.Sitemaps;

/// <summary>One generated file, ready to serve.</summary>
/// <param name="Name">File name without extension — <c>"news-1"</c>.</param>
/// <param name="Xml">Complete document.</param>
/// <param name="UrlCount">Number of <c>&lt;url&gt;</c> entries, for diagnostics.</param>
public sealed record SitemapFile(string Name, string Xml, int UrlCount);

/// <summary>The index plus every part it points at.</summary>
/// <param name="Index">The <c>sitemapindex</c> document served at <c>/sitemap.xml</c>.</param>
/// <param name="Files">Parts, keyed by name.</param>
public sealed record SitemapSet(string Index, IReadOnlyDictionary<string, SitemapFile> Files);

/// <summary>
/// Walks every registered <see cref="ISitemapSource"/> and turns their entries into a complete,
/// limit-respecting set of sitemap documents.
/// </summary>
/// <remarks>
/// <para><b>What a source never has to think about.</b> Absolute URLs, the mount path base, the
/// culture prefix, translated route segments, the <c>hreflang</c> cluster, the 50 000-URL limit,
/// the 50 MB limit, splitting into parts, numbering them, and the index that ties them together.
/// A source yields <c>("/news/first-post", updatedAt)</c> and stops.</para>
///
/// <para><b>Both limits are enforced, and the byte one usually binds first.</b> With twenty
/// languages every entry emits twenty <c>&lt;url&gt;</c> elements, each carrying twenty
/// <c>xhtml:link</c> alternates — around four hundred lines of XML per logical page. A file hits
/// 50 MB long before it hits 50 000 URLs, so counting URLs alone would silently produce documents
/// search engines reject wholesale.</para>
///
/// <para><b>Streaming.</b> Entries are consumed as the source yields them and written straight
/// into the current part, which is closed and started fresh the moment either limit is reached.
/// A source with two million rows never has its result set materialised.</para>
/// </remarks>
public sealed class SitemapGenerator(
    IEnumerable<ISitemapSource> sources,
    IOptions<SitemapOptions> options,
    ICurrentSite site)
{
    private const string Xmlns = "http://www.sitemaps.org/schemas/sitemap/0.9";
    private const string Xhtml = "http://www.w3.org/1999/xhtml";

    private readonly SitemapOptions _options = options.Value;

    // Injected as a sequence, not resolved from a list of Types: an area registers its own maps
    // in its ConfigureServices, so the host never enumerates them and the trimmer never has to
    // keep a constructor it cannot see.
    private readonly IReadOnlyList<ISitemapSource> _sources = [.. sources];

    /// <summary>
    /// Generates every file for the given origin.
    /// </summary>
    /// <param name="origin">
    /// Absolute origin with no trailing slash — <c>"https://example.com"</c>. Part of the cache
    /// key, because the same content served on two hostnames is two different sitemaps.
    /// </param>
    /// <param name="cancellationToken">Cancels mid-walk; partial work is discarded.</param>
    public async Task<SitemapSet> GenerateAsync(string origin, CancellationToken cancellationToken)
    {
        var prefix = origin.TrimEnd('/') + (site.Directory.HasValue ? site.Directory.Value.TrimEnd('/') : string.Empty);
        var policy = site.UrlPolicy;
        var routes = site.LocalizedRoutes;

        var files = new Dictionary<string, SitemapFile>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in _sources)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!source.IsEnabled)
                continue;

            await foreach (var file in BuildFilesAsync(source, prefix, policy, routes, cancellationToken))
                files[file.Name] = file;
        }

        return new SitemapSet(BuildIndex(prefix, files.Values), files);
    }

    private async IAsyncEnumerable<SitemapFile> BuildFilesAsync(
        ISitemapSource source,
        string prefix,
        CultureUrlPolicy? policy,
        LocalizedRouteTable routes,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var part = 1;
        var writer = new PartWriter(_options);

        await foreach (var entry in source.GetAsync(cancellationToken).WithCancellation(cancellationToken))
        {
            foreach (var url in Expand(entry, prefix, policy, routes))
            {
                if (!writer.TryWrite(url))
                {
                    yield return writer.Close($"{source.Name}-{part++}");
                    writer = new PartWriter(_options);

                    // The freshly-opened part is empty, so this write cannot fail for the same
                    // reason. If a single URL ever exceeded a whole file's budget the entry would
                    // be pathological, and dropping it beats looping forever.
                    writer.TryWrite(url);
                }
            }
        }

        if (writer.UrlCount > 0)
            yield return writer.Close($"{source.Name}-{part}");
    }

    /// <summary>
    /// Turns one logical entry into the URLs that represent it — one per culture on a prefixed
    /// Site, each carrying the full alternate cluster, or exactly one when the Site does not
    /// encode culture in its paths.
    /// </summary>
    private static IEnumerable<SitemapUrl> Expand(
        SitemapEntry entry, string prefix, CultureUrlPolicy? policy, LocalizedRouteTable routes)
    {
        if (policy is null || !policy.IsPrefixed)
        {
            yield return new SitemapUrl(prefix + Rooted(entry.Path), entry, []);
            yield break;
        }

        // Indexed cultures only, intersected with what the entry says it exists in. A sitemap is
        // a list of pages a crawler should fetch; listing one that answers noindex wastes the
        // fetch and contradicts the page.
        var cultures = new List<string>(policy.IndexedCultures.Length);
        foreach (var culture in policy.IndexedCultures)
        {
            if (entry.Cultures is not null &&
                !entry.Cultures.Contains(culture, StringComparer.OrdinalIgnoreCase))
                continue;

            cultures.Add(culture);
        }

        if (cultures.Count == 0)
            yield break;

        var byCulture = new List<(string Culture, string Url)>(cultures.Count);
        foreach (var culture in cultures)
        {
            var path = entry.PathsByCulture is not null && entry.PathsByCulture.TryGetValue(culture, out var custom)
                ? Rooted(custom)
                : routes.ToLocalized(culture, Rooted(entry.Path));

            var built = policy.BuildPath(culture, path);
            if (built is not null)
                byCulture.Add((culture, prefix + built));
        }

        var alternates = new List<(string Hreflang, string Url)>(byCulture.Count);
        foreach (var (culture, url) in byCulture)
            alternates.Add((Hreflang(policy, culture), url));

        foreach (var (_, url) in byCulture)
            yield return new SitemapUrl(url, entry, alternates);
    }

    private static string BuildIndex(string prefix, IEnumerable<SitemapFile> files)
    {
        var text = new StringBuilder(512);
        using var sink = new Utf8StringWriter(text);
        using var writer = XmlWriter.Create(sink, WriterSettings);

        writer.WriteStartDocument();
        writer.WriteStartElement("sitemapindex", Xmlns);

        foreach (var file in files)
        {
            writer.WriteStartElement("sitemap", Xmlns);
            writer.WriteElementString("loc", Xmlns, $"{prefix}/sitemap/{file.Name}.xml");
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndDocument();
        writer.Flush();

        return text.ToString();
    }

    internal static XmlWriterSettings WriterSettings => new()
    {
        Indent = false,
        OmitXmlDeclaration = false,
        Encoding = Encoding.UTF8,
        CheckCharacters = true,
    };

    /// <summary>
    /// A <see cref="StringWriter"/> that reports UTF-8.
    /// </summary>
    /// <remarks>
    /// <see cref="XmlWriter"/> takes the encoding for its declaration from the sink, not from
    /// <see cref="XmlWriterSettings.Encoding"/>, and a plain <see cref="StringWriter"/> reports
    /// UTF-16 because that is what a .NET string is. The document would then declare
    /// <c>encoding="utf-16"</c> while being served as UTF-8 — a contradiction strict parsers
    /// reject and lenient ones resolve by guessing.
    /// </remarks>
    private sealed class Utf8StringWriter(StringBuilder builder) : StringWriter(builder)
    {
        public override Encoding Encoding => Encoding.UTF8;
    }

    /// <summary>
    /// The <c>hreflang</c> value mirrors the URL segment — a site serving <c>/pl/</c> declares
    /// <c>pl</c>. Claiming a regional target the URLs do not distinguish splits signals the site
    /// never meant to split.
    /// </summary>
    private static string Hreflang(CultureUrlPolicy policy, string culture)
    {
        var segment = policy.SegmentFor(culture) ?? culture;
        var dash = segment.IndexOf('-');
        return dash < 0
            ? segment
            : string.Concat(segment.AsSpan(0, dash + 1), segment.AsSpan(dash + 1).ToString().ToUpperInvariant());
    }

    private static string Rooted(string path)
        => string.IsNullOrEmpty(path) || path[0] == '/' ? path : "/" + path;

    private readonly record struct SitemapUrl(
        string Location,
        SitemapEntry Entry,
        IReadOnlyList<(string Hreflang, string Url)> Alternates);

    /// <summary>
    /// Accumulates URLs into one file, refusing the write that would breach either limit.
    /// </summary>
    private sealed class PartWriter
    {
        private readonly SitemapOptions _options;
        private readonly StringBuilder _text = new(64 * 1024);
        private readonly Utf8StringWriter _sink;
        private readonly XmlWriter _writer;

        public PartWriter(SitemapOptions options)
        {
            _options = options;
            _sink = new Utf8StringWriter(_text);
            _writer = XmlWriter.Create(_sink, WriterSettings);
            _writer.WriteStartDocument();
            _writer.WriteStartElement("urlset", Xmlns);
            _writer.WriteAttributeString("xmlns", "xhtml", null, Xhtml);
        }

        public int UrlCount { get; private set; }

        /// <summary>
        /// Writes one URL, or returns <see langword="false"/> when the file is full.
        /// </summary>
        /// <remarks>
        /// The size check is made <em>after</em> writing and the entry is kept, which overshoots
        /// the ceiling by at most one entry — hence the deliberate margin in the defaults.
        /// Checking beforehand would mean predicting the serialized length of an element that has
        /// not been built yet, and being wrong about that in the other direction produces a file
        /// a search engine rejects outright.
        /// </remarks>
        public bool TryWrite(in SitemapUrl url)
        {
            if (UrlCount >= _options.MaxUrlsPerFile || _text.Length >= _options.MaxBytesPerFile)
                return false;

            _writer.WriteStartElement("url", Xmlns);
            _writer.WriteElementString("loc", Xmlns, url.Location);

            if (url.Entry.LastModified is { } modified)
                _writer.WriteElementString("lastmod", Xmlns, modified.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture));

            if (url.Entry.ChangeFrequency is { } frequency)
                _writer.WriteElementString("changefreq", Xmlns, frequency.ToString().ToLowerInvariant());

            if (url.Entry.Priority is { } priority)
                _writer.WriteElementString("priority", Xmlns, Math.Clamp(priority, 0d, 1d).ToString("0.0", CultureInfo.InvariantCulture));

            foreach (var (hreflang, href) in url.Alternates)
            {
                _writer.WriteStartElement("link", Xhtml);
                _writer.WriteAttributeString("rel", "alternate");
                _writer.WriteAttributeString("hreflang", hreflang);
                _writer.WriteAttributeString("href", href);
                _writer.WriteEndElement();
            }

            _writer.WriteEndElement();
            _writer.Flush();

            UrlCount++;
            return true;
        }

        public SitemapFile Close(string name)
        {
            _writer.WriteEndElement();
            _writer.WriteEndDocument();
            _writer.Flush();
            _writer.Dispose();
            _sink.Dispose();

            return new SitemapFile(name, _text.ToString(), UrlCount);
        }
    }
}
