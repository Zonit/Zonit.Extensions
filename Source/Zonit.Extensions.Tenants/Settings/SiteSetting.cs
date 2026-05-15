using System.ComponentModel.DataAnnotations;
using System.Text.Json;

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

    public override SiteSettingsModel Hydrate(string json)
        => JsonSerializer.Deserialize(json, TenantsJsonContext.Default.SiteSettingsModel) ?? new();
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

    [Display(Name = "Default Language", Description = "The default language of the website (BCP 47, e.g., 'en-US', 'pl-PL').")]
    [Required(ErrorMessage = "Default language is required.")]
    [StringLength(10, MinimumLength = 2, ErrorMessage = "Default language code must be 2-10 characters.")]
    public string Language { get; set; } = "pl-PL";

    [Display(Name = "Logo URL", Description = "URL to the main logo displayed on the website.")]
    [StringLength(200, ErrorMessage = "Logo URL cannot exceed 200 characters.")]
    public string? LogoUrl { get; set; }

    [Display(Name = "Favicon URL", Description = "URL to the favicon icon displayed in the browser tab.")]
    [StringLength(200, ErrorMessage = "Favicon URL cannot exceed 200 characters.")]
    public string? FaviconUrl { get; set; }
}
