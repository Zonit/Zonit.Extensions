using System.Text.Json.Serialization;

namespace Zonit.Extensions.Website.Schema;

/// <summary>
/// A company or institution. Usually emitted once, site-wide, from the document shell rather than
/// from a page.
/// </summary>
public sealed class SchemaOrganization : SchemaThing
{
    /// <summary>schema.org discriminator.</summary>
    [JsonPropertyName("@type")]
    public string SchemaType => "Organization";

    /// <summary>Logo URL. Google wants it absolute, and at least 112×112 px.</summary>
    [JsonPropertyName("logo")]
    public string? Logo { get; set; }

    /// <summary>Contact points — support lines, sales, per-language desks.</summary>
    [JsonPropertyName("contactPoint")]
    public List<SchemaContactPoint>? ContactPoint { get; set; }
}

/// <param name="Telephone">Number in international format, e.g. <c>"+48-22-000-00-00"</c>.</param>
/// <param name="ContactType">
/// One of schema.org's controlled values — <c>"customer support"</c>, <c>"sales"</c>,
/// <c>"technical support"</c>. Free text here is ignored rather than shown.
/// </param>
/// <param name="Email">Contact address.</param>
/// <param name="AvailableLanguage">Languages this contact point is served in.</param>
public sealed record SchemaContactPoint(
    [property: JsonPropertyName("telephone")] string? Telephone = null,
    [property: JsonPropertyName("contactType")] string? ContactType = null,
    [property: JsonPropertyName("email")] string? Email = null,
    [property: JsonPropertyName("availableLanguage")] List<string>? AvailableLanguage = null)
{
    /// <summary>schema.org discriminator.</summary>
    [JsonPropertyName("@type")]
    public string SchemaType => "ContactPoint";
}

/// <summary>
/// The site as a whole. Emitted once from the shell; its main practical effect is enabling the
/// sitelinks search box when <see cref="SearchUrlTemplate"/> is supplied.
/// </summary>
public sealed class SchemaWebSite : SchemaThing
{
    /// <summary>schema.org discriminator.</summary>
    [JsonPropertyName("@type")]
    public string SchemaType => "WebSite";

    /// <summary>
    /// Search URL with a <c>{search_term_string}</c> placeholder, e.g.
    /// <c>"https://example.com/search?q={search_term_string}"</c>. <see langword="null"/> emits
    /// no search action.
    /// </summary>
    [JsonPropertyName("potentialAction")]
    public SchemaSearchAction? PotentialAction => SearchUrlTemplate is null
        ? null
        : new SchemaSearchAction(SearchUrlTemplate);

    /// <summary>Backing value for <see cref="PotentialAction"/>. Not serialized on its own.</summary>
    [JsonIgnore]
    public string? SearchUrlTemplate { get; set; }
}

/// <param name="Target">
/// Search URL template containing <c>{search_term_string}</c>. Not serialized on its own — it is
/// the backing value for <see cref="EntryPoint"/>, which is what <c>target</c> must actually
/// contain. Without the ignore both members claim the same JSON name and serialization throws.
/// </param>
public sealed record SchemaSearchAction([property: JsonIgnore] string Target)
{
    /// <summary>schema.org discriminator.</summary>
    [JsonPropertyName("@type")]
    public string SchemaType => "SearchAction";

    /// <summary>The template, wrapped in the entry-point shape Google expects.</summary>
    [JsonPropertyName("target")]
    public SchemaEntryPoint EntryPoint => new(Target);

    /// <summary>Names the placeholder in <see cref="Target"/>.</summary>
    [JsonPropertyName("query-input")]
    public string QueryInput => "required name=search_term_string";
}

/// <param name="UrlTemplate">The search URL template.</param>
public sealed record SchemaEntryPoint(
    [property: JsonPropertyName("urlTemplate")] string UrlTemplate)
{
    /// <summary>schema.org discriminator.</summary>
    [JsonPropertyName("@type")]
    public string SchemaType => "EntryPoint";
}

/// <summary>An ordinary content page. The safe default when nothing more specific fits.</summary>
public sealed class SchemaWebPage : SchemaThing
{
    /// <summary>schema.org discriminator.</summary>
    [JsonPropertyName("@type")]
    public string SchemaType => "WebPage";

    /// <inheritdoc />
    internal override bool DescribesPage => true;

    /// <summary>When the content was last meaningfully reviewed — not when the file changed.</summary>
    [JsonPropertyName("lastReviewed")]
    public DateTimeOffset? LastReviewed { get; set; }

    /// <summary>Primary image.</summary>
    [JsonPropertyName("primaryImageOfPage")]
    public string? PrimaryImage { get; set; }
}

/// <summary>
/// An editorial article — the node behind article rich results.
/// </summary>
/// <remarks>
/// <see cref="Headline"/>, <see cref="DatePublished"/> and at least one image are what actually
/// unlock the rich result; the rest refines it.
/// </remarks>
public sealed class SchemaArticle : SchemaThing
{
    /// <summary>schema.org discriminator.</summary>
    [JsonPropertyName("@type")]
    public string SchemaType => "Article";

    /// <inheritdoc />
    internal override bool DescribesPage => true;

    /// <summary>Article title. <b>Truncated by Google past 110 characters</b>, so keep it short.</summary>
    [JsonPropertyName("headline")]
    public string? Headline { get; set; }

    /// <summary>Images, absolute URLs. Several aspect ratios (16:9, 4:3, 1:1) is the documented ideal.</summary>
    [JsonPropertyName("image")]
    public List<string>? Images { get; set; }

    /// <summary>Authors.</summary>
    [JsonPropertyName("author")]
    public List<SchemaPerson>? Author { get; set; }

    /// <summary>Publishing organization.</summary>
    [JsonPropertyName("publisher")]
    public SchemaOrganization? Publisher { get; set; }

    /// <summary>First publication instant. Include the offset — a bare date loses the ordering.</summary>
    [JsonPropertyName("datePublished")]
    public DateTimeOffset? DatePublished { get; set; }

    /// <summary>Last substantive edit. Omit rather than setting it to the deployment time.</summary>
    [JsonPropertyName("dateModified")]
    public DateTimeOffset? DateModified { get; set; }
}

/// <summary>A person — an article author, a profile subject.</summary>
public sealed class SchemaPerson : SchemaThing
{
    /// <summary>schema.org discriminator.</summary>
    [JsonPropertyName("@type")]
    public string SchemaType => "Person";
}

/// <summary>
/// The breadcrumb trail, which search results render in place of the raw URL.
/// </summary>
/// <remarks>
/// Worth emitting on any site with real hierarchy: it is one of the few structured-data types
/// whose effect is visible on every result, not only on rich ones.
/// </remarks>
public sealed class SchemaBreadcrumbList : SchemaThing
{
    /// <summary>schema.org discriminator.</summary>
    [JsonPropertyName("@type")]
    public string SchemaType => "BreadcrumbList";

    /// <summary>Trail items, root first. Positions are assigned on emission.</summary>
    [JsonPropertyName("itemListElement")]
    public List<SchemaBreadcrumbItem> Items { get; set; } = [];
}

/// <param name="Position">1-based position in the trail.</param>
/// <param name="Name">Label.</param>
/// <param name="Item">Absolute URL. Omitted on the last item, which is the current page.</param>
public sealed record SchemaBreadcrumbItem(
    [property: JsonPropertyName("position")] int Position,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("item")] string? Item = null)
{
    /// <summary>schema.org discriminator.</summary>
    [JsonPropertyName("@type")]
    public string SchemaType => "ListItem";
}
