using System.ComponentModel.DataAnnotations;

namespace Zonit.Extensions.Tenants.Settings;

/// <summary>
/// Built-in tenant setting carrying the site identity (title, meta description,
/// language, logo, favicon). Most hosts customise these per tenant first.
/// </summary>
public sealed class SiteSetting : Setting<SiteSettingsModel>
{
    public override string Key => "site";
    public override string Name => "Site";
    public override string Description => "Basic website settings.";
}

/// <summary>Model for <see cref="SiteSetting"/>.</summary>
public sealed class SiteSettingsModel
{
    [Display(Name = "Website Title", Description = "The main title of the website, displayed in the browser tab and search results.")]
    [Required(ErrorMessage = "Website title is required.")]
    [StringLength(30, MinimumLength = 3, ErrorMessage = "Website title must be 3-30 characters.")]
    public string Title { get; set; } = "New website";

    [Display(Name = "Meta Description", Description = "A short description of the website content used in SEO and social media previews.")]
    [Required(ErrorMessage = "Meta description is required.")]
    [StringLength(160, MinimumLength = 10, ErrorMessage = "Meta description must be 10-160 characters.")]
    public string MetaDescription { get; set; } = "This is a new website created";

    /// <summary>
    /// What this site actually is, in prose, for a reader who has never seen it. Fills the
    /// summary block of <c>llms.txt</c>; falls back to <see cref="MetaDescription"/>.
    /// </summary>
    /// <remarks>
    /// Deliberately separate from <see cref="MetaDescription"/> rather than reusing it. That one
    /// is a 160-character search snippet written to earn a click, and it is the wrong text for an
    /// agent deciding whether this site can answer a question at all. This one has room to say
    /// what the product does, who it is for and what makes its data trustworthy — the paragraph a
    /// human would give if asked "what is this?".
    /// </remarks>
    [Display(Name = "About", Description = "What this site is, in prose — used to brief AI agents in llms.txt. Longer and more explanatory than the meta description.")]
    [StringLength(1000, ErrorMessage = "About must be at most 1000 characters.")]
    public string? About { get; set; }

    [Display(Name = "Default Language", Description = "Language used when the visitor has expressed no preference (BCP 47, e.g. 'en-US', 'pl-PL'). Must be one of the site's supported cultures.")]
    [Required(ErrorMessage = "Default language is required.")]
    [StringLength(10, MinimumLength = 2, ErrorMessage = "Default language code must be 2-10 characters.")]
    public string Language { get; set; } = "pl-PL";

    [Display(Name = "Logo URL", Description = "URL to the main logo displayed on the website.")]
    [StringLength(200, ErrorMessage = "Logo URL cannot exceed 200 characters.")]
    public string? LogoUrl { get; set; }

    [Display(Name = "Favicon URL", Description = "URL to the favicon icon displayed in the browser tab.")]
    [StringLength(200, ErrorMessage = "Favicon URL cannot exceed 200 characters.")]
    public string? FaviconUrl { get; set; }

    [Display(Name = "Social Share Image", Description = "Image shown when a page of this site is shared on social media. Used for any page that does not supply its own. Recommended 1200×630.")]
    [StringLength(200, ErrorMessage = "Social share image URL cannot exceed 200 characters.")]
    public string? SocialImageUrl { get; set; }

    [Display(Name = "Canonical URL", Description = "Absolute origin used in canonical and hreflang links, e.g. 'https://example.com'. Empty = derive from the request.")]
    [StringLength(200, ErrorMessage = "Canonical URL cannot exceed 200 characters.")]
    public string? CanonicalUrl { get; set; }

    [Display(Name = "Title Position", Description = "Where the website title goes relative to the page title.")]
    public SiteTitlePosition TitlePosition { get; set; } = SiteTitlePosition.Suffix;

    [Display(Name = "Title Separator", Description = "Text between the page title and the website title, e.g. ' - ', ' | ', ' :: '. Empty = show the page title alone.")]
    [StringLength(10, ErrorMessage = "Title separator cannot exceed 10 characters.")]
    public string? TitleSeparator { get; set; } = " - ";
}

/// <summary>
/// Where <see cref="SiteSettingsModel.Title"/> is placed relative to the current page's own
/// title — <c>"Pricing - Zonit"</c> versus <c>"Zonit - Pricing"</c>.
/// </summary>
/// <remarks>
/// A per-tenant setting rather than a build-time constant because it is a branding decision,
/// and a host serving several tenants routinely needs a different answer for each. A page that
/// declares no title of its own always renders <see cref="SiteSettingsModel.Title"/> alone,
/// whatever this says — there is nothing to compose it with.
/// </remarks>
public enum SiteTitlePosition
{
    /// <summary>Page title first: <c>"Pricing - Zonit"</c>. The usual choice; the distinctive part leads.</summary>
    Suffix = 0,

    /// <summary>Website title first: <c>"Zonit - Pricing"</c>.</summary>
    Prefix = 1,

    /// <summary>No composition — the page title stands alone, and pages without one fall back to the website title.</summary>
    None = 2,
}
