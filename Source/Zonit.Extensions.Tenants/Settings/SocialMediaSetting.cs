using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Zonit.Extensions.Tenants.Settings;

/// <summary>
/// Built-in tenant setting carrying social-media profile URLs. Each link is exposed
/// at <c>{domain}/{platform}</c> by the host's redirect handler — this setting only
/// stores the target URLs.
/// </summary>
public sealed class SocialMediaSetting : Setting<SocialMediaModel>
{
    public override string Key => "social_media";
    public override string Name => "Social Media";
    public override string Description => "Links to social media profiles.";

    public override SocialMediaModel Hydrate(string json)
        => JsonSerializer.Deserialize(json, TenantsJsonContext.Default.SocialMediaModel) ?? new();
}

/// <summary>Model for <see cref="SocialMediaSetting"/>.</summary>
public sealed class SocialMediaModel
{
    [Display(Name = "Facebook")]
    [StringLength(200), Url(ErrorMessage = "The Facebook link must be a valid URL.")]
    public string? Facebook { get; set; }

    [Display(Name = "X (formerly Twitter)")]
    [StringLength(200), Url(ErrorMessage = "The X link must be a valid URL.")]
    public string? X { get; set; }

    [Display(Name = "Instagram")]
    [StringLength(200), Url(ErrorMessage = "The Instagram link must be a valid URL.")]
    public string? Instagram { get; set; }

    [Display(Name = "LinkedIn")]
    [StringLength(200), Url(ErrorMessage = "The LinkedIn link must be a valid URL.")]
    public string? LinkedIn { get; set; }

    [Display(Name = "YouTube")]
    [StringLength(200), Url(ErrorMessage = "The YouTube link must be a valid URL.")]
    public string? YouTube { get; set; }

    [Display(Name = "TikTok")]
    [StringLength(200), Url(ErrorMessage = "The TikTok link must be a valid URL.")]
    public string? TikTok { get; set; }

    [Display(Name = "Pinterest")]
    [StringLength(200), Url(ErrorMessage = "The Pinterest link must be a valid URL.")]
    public string? Pinterest { get; set; }

    [Display(Name = "Snapchat")]
    [StringLength(200), Url(ErrorMessage = "The Snapchat link must be a valid URL.")]
    public string? Snapchat { get; set; }

    [Display(Name = "Reddit")]
    [StringLength(200), Url(ErrorMessage = "The Reddit link must be a valid URL.")]
    public string? Reddit { get; set; }

    [Display(Name = "Twitch")]
    [StringLength(200), Url(ErrorMessage = "The Twitch link must be a valid URL.")]
    public string? Twitch { get; set; }

    [Display(Name = "Threads")]
    [StringLength(200), Url(ErrorMessage = "The Threads link must be a valid URL.")]
    public string? Threads { get; set; }

    [Display(Name = "Discord")]
    [StringLength(200), Url(ErrorMessage = "The Discord link must be a valid URL.")]
    public string? Discord { get; set; }
}
