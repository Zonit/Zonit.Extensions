namespace Zonit.Extensions.Website;

/// <summary>
/// Per-page document metadata — what goes into <c>&lt;title&gt;</c>, the description, the social
/// preview and the indexing directives.
/// </summary>
/// <remarks>
/// <para>Declared on a page by overriding <c>PageBase.Meta</c>, and mutable afterwards so a page
/// that only knows its title once data has loaded can set it in <c>OnInitializedAsync</c>:</para>
///
/// <code>
/// protected override PageMeta Meta { get; } = new() { Description = "Manage your profile." };
///
/// protected override async Task OnInitializedAsync(CancellationToken token)
/// {
///     _user = await _users.GetAsync(Id, token);
///     Meta.Title = T("User profile {0}", _user.Name);
/// }
/// </code>
///
/// <para>Everything here is optional. A page that sets nothing still renders a correct document:
/// the title falls back to the tenant's website title, the description to the tenant's meta
/// description, and the canonical URL to the page's own address. The point of the type is that a
/// page never has to know how those are composed.</para>
/// </remarks>
public sealed class PageMeta
{
    /// <summary>
    /// The page's own title, before composition. <c>"Pricing"</c> becomes
    /// <c>"Pricing - Zonit"</c> or <c>"Zonit - Pricing"</c> according to the tenant's
    /// <c>SiteSettingsModel.TitlePosition</c> and separator; <see langword="null"/> renders the
    /// website title alone.
    /// </summary>
    /// <remarks>
    /// Write the human title, already translated — <c>T("Pricing")</c>. Composition is a
    /// presentation concern and belongs to the tenant, not to the page.
    /// </remarks>
    public string? Title { get; set; }

    /// <summary>
    /// Meta description and the social-preview description. Falls back to the tenant's
    /// <c>MetaDescription</c>. Keep it under ~160 characters; nothing truncates it for you,
    /// because silently cutting an author's sentence mid-word is worse than a long one.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Social-preview image (<c>og:image</c>). Absolute URL, or a site-relative path which is
    /// resolved against the canonical origin.
    /// </summary>
    public string? Image { get; set; }

    /// <summary>
    /// <c>og:type</c> — <c>"website"</c> for ordinary pages, <c>"article"</c> for editorial
    /// content. Defaults to <c>"website"</c>.
    /// </summary>
    public string Type { get; set; } = "website";

    /// <summary>
    /// Keeps this page out of search indexes while leaving its links crawlable
    /// (<c>noindex, follow</c>).
    /// </summary>
    /// <remarks>
    /// For pages that are reachable and useful but should not be a search result — a filtered
    /// listing, a thank-you page, a print view. It does not gate access; use
    /// <c>[RequirePermission]</c> for that.
    /// </remarks>
    public bool NoIndex { get; set; }

    /// <summary>
    /// The languages this page's <em>content</em> actually exists in. <see langword="null"/> — the
    /// default — means every indexed language, which is right for anything translated from
    /// resource files.
    /// </summary>
    /// <remarks>
    /// <para><b>The problem it solves.</b> Content translated per item, in a row rather than a
    /// resource file, arrives unevenly: a signal exists in eight of ten languages. The Site still
    /// routes <c>/cs/signals/x</c>, the page still renders — with the fallback rendition and a
    /// notice — and now the same English text answers at three addresses. Each is a separate
    /// indexable URL of identical content, and each claims through <c>hreflang</c> to be a distinct
    /// language version, which is a claim a crawler can check and find false.</para>
    ///
    /// <para><b>What setting it does.</b> Two things, from one declaration, so they cannot drift:</para>
    /// <list type="bullet">
    ///   <item>Rendering in a language outside the set is <c>noindex, follow</c> — the fallback
    ///         page stays reachable and its links stay crawlable, but it is not offered as a
    ///         result.</item>
    ///   <item>The <c>hreflang</c> cluster on the versions that <em>do</em> exist lists only those.
    ///         A cluster naming a version that answers <c>noindex</c> is discarded whole, taking
    ///         the working languages down with it — so filtering here is what keeps the eight
    ///         real translations clustered.</item>
    /// </list>
    ///
    /// <code>
    /// protected override async Task OnInitializedAsync(CancellationToken token)
    /// {
    ///     _signal = await _signals.GetAsync(Id, token);
    ///     Meta.Cultures = _signal.Translations.Keys.Select(c => new Culture(c)).ToArray();
    /// }
    /// </code>
    ///
    /// <para>Assign it as soon as the data is loaded — <see cref="PageMeta"/> is re-announced after
    /// the page's own lifecycle, so a value set past an <c>await</c> still reaches the head. The
    /// matching declaration for the sitemap is <c>SitemapEntry.Cultures</c>; the two answer the
    /// same question and should be fed from the same place.</para>
    /// </remarks>
    public IReadOnlyList<Culture>? Cultures { get; set; }

    /// <summary>
    /// Replaces the whole <c>robots</c> directive for this page. <see langword="null"/> uses the
    /// Site's default; an empty string emits no tag at all.
    /// </summary>
    /// <remarks>
    /// The escape hatch for directives the framework does not model — <c>noarchive</c>,
    /// <c>unavailable_after</c>, a per-crawler rule. It wins outright over <see cref="NoIndex"/>,
    /// so a page setting both is stating the full string on purpose. Site-level withholding still
    /// takes precedence: a Site that is not indexable, or a language deliberately kept out of the
    /// index, cannot be talked into <c>index</c> by a page.
    /// </remarks>
    public string? Robots { get; set; }

    /// <summary>
    /// Overrides the canonical URL. <see langword="null"/> — the default — derives it from the
    /// request, which is right for almost every page.
    /// </summary>
    /// <remarks>
    /// Set it when several addresses legitimately render this page and one of them is the real
    /// one: a paginated listing pointing at page 1, a filtered view pointing at the unfiltered
    /// listing. Accepts an absolute URL or a site-relative path.
    /// </remarks>
    public string? Canonical { get; set; }

    /// <summary>
    /// Per-culture paths for this page when the slug itself is translated, keyed by BCP-47 tag.
    /// </summary>
    /// <remarks>
    /// <para>Only needed when the address differs per language in a way configuration cannot
    /// know — a news article whose slug lives in the content store. Routes whose <em>static</em>
    /// path is translated are declared once in <c>SiteOptions.Cultures.LocalizeRoute</c> and need
    /// nothing here.</para>
    ///
    /// <para>Paths are site-relative and without the culture segment; the framework adds the
    /// prefix. Cultures absent from the map keep the current path, which is the correct default
    /// for the great majority of pages.</para>
    ///
    /// <code>
    /// Meta.Alternates["pl-pl"] = $"/aktualnosci/{article.SlugPl}";
    /// Meta.Alternates["en-us"] = $"/news/{article.SlugEn}";
    /// </code>
    /// </remarks>
    public Dictionary<string, string> Alternates { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Structured data for this page, emitted as JSON-LD in the head.
    /// </summary>
    /// <remarks>
    /// <para>Typed schema.org nodes rather than a JSON string, because nothing validates a
    /// string: a misspelled property, an <c>@type</c> that does not exist or a headline past the
    /// 110 characters Google truncates at all serialize happily and are silently ignored by the
    /// crawler. The rich result simply never appears and there is nothing in the page to point
    /// at.</para>
    ///
    /// <para>Several nodes are emitted as one <c>@graph</c>. Any node that sets no
    /// <c>url</c> inherits the page's canonical, so the common case needs no repetition:</para>
    ///
    /// <code>
    /// Meta.Schema.Add(new SchemaArticle
    /// {
    ///     Headline      = _article.Title,
    ///     Images        = [_article.CoverUrl],
    ///     DatePublished = _article.PublishedAt,
    ///     DateModified  = _article.UpdatedAt,
    ///     Author        = [new SchemaPerson { Name = _article.AuthorName }],
    /// });
    /// </code>
    ///
    /// <para><b>Usually you add nothing.</b> The framework already derives a <c>WebPage</c> (or
    /// <c>Article</c>) from the title, description, image and canonical, a <c>BreadcrumbList</c>
    /// from the active trail, and <c>Organization</c> + <c>WebSite</c> from tenant settings on the
    /// home page. What you add here is what inference cannot know — publication dates, authors,
    /// prices — and a node you supply <b>replaces</b> the derived one of the same type, so a
    /// fully-formed <c>Article</c> takes over completely.</para>
    /// </remarks>
    public List<Schema.SchemaThing> Schema { get; } = [];

    /// <summary>
    /// Whether the framework derives structured data for this page. <see langword="true"/> by
    /// default.
    /// </summary>
    /// <remarks>
    /// Turn it off for a page whose graph must be exactly what <see cref="Schema"/> contains —
    /// a hand-tuned product or recipe page, or one being debugged against the Rich Results Test.
    /// With it off, an empty <see cref="Schema"/> emits no block at all.
    /// </remarks>
    public bool AutoSchema { get; set; } = true;

    /// <summary>
    /// Whether <see cref="Title"/> and <see cref="Description"/> go through the translation
    /// registry before being rendered. <see langword="true"/> by default.
    /// </summary>
    /// <remarks>
    /// <para>The translation key in this framework <b>is</b> the English source string, so
    /// <c>Description = "What it costs."</c> is already a valid key and translating it is free.
    /// Requiring <c>T(…)</c> here would be one more thing to forget, and forgetting it produces a
    /// page that is correct in English and silently untranslated everywhere else — the failure
    /// nobody notices until a native speaker opens the site.</para>
    ///
    /// <para>Text with no rendition falls through to itself, so this is safe for dynamic values:
    /// <c>Meta.Title = article.Headline</c> renders the headline whether or not anything matches.
    /// Turn it off for content that must never be looked up — a product name, a person's name, or
    /// text you already translated yourself with <c>T(…)</c> and do not want translated twice.</para>
    /// </remarks>
    public bool Translate { get; set; } = true;
}
