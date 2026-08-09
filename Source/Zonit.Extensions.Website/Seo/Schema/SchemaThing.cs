using System.Text.Json.Serialization;

namespace Zonit.Extensions.Website.Schema;

/// <summary>
/// Base of the schema.org vocabulary — the shape every structured-data node shares.
/// </summary>
/// <remarks>
/// <para>Typed nodes instead of hand-written JSON-LD, for the reason hand-written JSON-LD always
/// eventually needs: nothing validates a string. A misspelled <c>datePublished</c>, a
/// <c>headline</c> past the 110 characters Google truncates at, an <c>@type</c> that does not
/// exist — all of them serialize happily and are silently dropped by the crawler, so the rich
/// result simply never appears and there is nothing in the page to point at.</para>
///
/// <para>Only the subset with a documented effect in Google Search is modelled. schema.org has
/// hundreds of types; the ones that change what a result looks like are few, and a model of the
/// rest would be a vocabulary dump nobody reads.</para>
///
/// <para><b>Serialization is source-generated</b> through <c>SchemaJsonContext</c>, so emitting
/// structured data does not drag reflection-based JSON into a trimmed or AOT-published site.
/// A custom node type outside this hierarchy therefore needs its own context — see
/// <see cref="PageMeta.Schema"/>.</para>
/// </remarks>
public abstract class SchemaThing
{
    /// <summary>
    /// Whether this node describes the page it is emitted on, and should therefore inherit the
    /// page's canonical URL when it sets none.
    /// </summary>
    /// <remarks>
    /// True for <see cref="SchemaWebPage"/> and <see cref="SchemaArticle"/>. False for everything
    /// else, because the alternative is wrong: a breadcrumb trail is not located at the page, and
    /// an organization is certainly not — stamping the canonical onto them asserts an identity
    /// that does not hold and makes the graph describe two different things as the same resource.
    /// </remarks>
    [System.Text.Json.Serialization.JsonIgnore]
    internal virtual bool DescribesPage => false;


    /// <summary>Node name — the headline of a page, the legal name of an organization.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Canonical URL of the thing this node describes.</summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>Short description. Distinct from the meta description, though usually the same text.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Authoritative URLs identifying the same entity elsewhere — a Wikipedia article, a
    /// LinkedIn company page, an official social profile.
    /// </summary>
    /// <remarks>
    /// The one property worth filling on an organization even when nothing else is: it is how a
    /// search engine connects a name on a page to an entity it already knows about, rather than
    /// guessing from the string.
    /// </remarks>
    [JsonPropertyName("sameAs")]
    public List<string>? SameAs { get; set; }
}
