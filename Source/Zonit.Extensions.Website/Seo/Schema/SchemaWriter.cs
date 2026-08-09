using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zonit.Extensions.Website.Schema;

/// <summary>
/// Source-generated JSON metadata for the built-in schema.org nodes, so structured data can be
/// emitted from a trimmed or natively compiled site.
/// </summary>
[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SchemaThing))]
[JsonSerializable(typeof(SchemaOrganization))]
[JsonSerializable(typeof(SchemaWebSite))]
[JsonSerializable(typeof(SchemaWebPage))]
[JsonSerializable(typeof(SchemaArticle))]
[JsonSerializable(typeof(SchemaBreadcrumbList))]
[JsonSerializable(typeof(SchemaPerson))]
internal sealed partial class SchemaJsonContext : JsonSerializerContext;

/// <summary>
/// Renders schema.org nodes into the <c>&lt;script type="application/ld+json"&gt;</c> block the
/// document head carries.
/// </summary>
public static class SchemaWriter
{
    /// <summary>
    /// Serializes <paramref name="nodes"/> as JSON-LD, or returns <see langword="null"/> when
    /// there is nothing to emit.
    /// </summary>
    /// <param name="nodes">Nodes to render. Empty and all-null entries yield <see langword="null"/>.</param>
    /// <param name="context">
    /// Absolute URL of the page, used as <c>@id</c> and as the fallback <c>url</c> of any node
    /// that did not set one.
    /// </param>
    /// <remarks>
    /// <para>Several nodes are emitted as a JSON-LD <c>@graph</c> rather than as several script
    /// blocks. Both are legal, but a graph lets nodes be one connected description of the page
    /// instead of several unrelated assertions about it, which is how a crawler reads it.</para>
    ///
    /// <para>Serialization goes through the source-generated context and escapes with the
    /// relaxed encoder, so non-ASCII text is written literally rather than as <c>\uXXXX</c>
    /// noise. That is safe here precisely because the output never reaches an HTML parser as
    /// markup: <see cref="Escape"/> neutralises the only sequence that could end the script
    /// element early.</para>
    /// </remarks>
    public static string? Write(IReadOnlyList<SchemaThing>? nodes, string? context = null)
    {
        if (nodes is null || nodes.Count == 0)
            return null;

        var present = new List<SchemaThing>(nodes.Count);
        foreach (var node in nodes)
        {
            if (node is null)
                continue;

            if (node.DescribesPage)
                node.Url ??= context;

            present.Add(node);
        }

        if (present.Count == 0)
            return null;

        var json = new StringBuilder(256);
        json.Append("{\"@context\":\"https://schema.org\",");

        if (present.Count == 1)
        {
            // A single node inlines: "@context" plus the node's own members. Wrapping one node in
            // a graph is legal but reads as if more were intended.
            var single = Serialize(present[0]);
            json.Append(single.AsSpan(1)); // drop the object's opening brace, keep the rest
            return Escape(json.ToString());
        }

        json.Append("\"@graph\":[");
        for (var i = 0; i < present.Count; i++)
        {
            if (i > 0)
                json.Append(',');
            json.Append(Serialize(present[i]));
        }
        json.Append("]}");

        return Escape(json.ToString());
    }

    private static string Serialize(SchemaThing node)
        => JsonSerializer.Serialize(node, node.GetType(), SchemaJsonContext.Default);

    /// <summary>
    /// Neutralises the one sequence that can break out of a <c>&lt;script&gt;</c> element.
    /// </summary>
    /// <remarks>
    /// An HTML parser ends a script at the literal characters <c>&lt;/script</c> regardless of
    /// JSON quoting, so a title containing it would terminate the block and spill the rest of the
    /// payload into the document as markup. <c>&lt;!--</c> is escaped for the same reason: it
    /// switches the parser into a state where the next <c>&lt;/script&gt;</c> no longer closes
    /// the element. Escaping the slash keeps the JSON string equivalent — <c>\/</c> and <c>/</c>
    /// decode identically — so the structured data is unchanged, only the bytes are safe.
    /// </remarks>
    private static string Escape(string json)
        => json
            .Replace("</", "<\\/", StringComparison.Ordinal)
            .Replace("<!--", "\\u003C!--", StringComparison.Ordinal);

    /// <summary>Relaxed encoder — see <see cref="Write"/> for why it is safe here.</summary>
    internal static readonly JavaScriptEncoder Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
}
