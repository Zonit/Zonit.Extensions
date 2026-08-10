using System.ComponentModel.DataAnnotations;

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

    /// <summary>
    /// Anything the named properties above do not cover, as label → URL:
    /// <c>"Facebook group"</c>, <c>"Community forum"</c>, <c>"Status page"</c>.
    /// </summary>
    /// <remarks>
    /// <para>The named properties exist because the common platforms deserve validation, a stable
    /// key and a translated label. This dictionary exists because the list is never finished —
    /// a group is not a page, a Mastodon instance is not a platform, and waiting for a framework
    /// release to add a link is the wrong shape of dependency.</para>
    ///
    /// <para><b>Not folded into structured data.</b> <c>sameAs</c> means "a page that unambiguously
    /// identifies this organisation", which an official profile is and an arbitrary link may not
    /// be — a partner site or a status page would make the claim false. Custom entries surface in
    /// <c>llms.txt</c>, where they are simply described rather than asserted as identity.</para>
    /// </remarks>
    [Display(Name = "Other links", Description = "Label → URL for anything not covered above (a group, a forum, a status page).")]
    public Dictionary<string, string> Custom { get; set; } = [];

    /// <summary>
    /// Every profile the tenant filled in, as label → URL, in declaration order, blanks skipped.
    /// </summary>
    /// <param name="includeCustom">Include <see cref="Custom"/>. See its remarks for when not to.</param>
    /// <remarks>
    /// One enumeration for every consumer. The structured-data composer used to carry its own
    /// hand-written list and it had drifted to six of the twelve platforms — Reddit, Twitch,
    /// Threads, Discord, Pinterest and Snapchat were filled in by tenants and silently absent from
    /// <c>sameAs</c>. A list that has to be edited in two places to stay right will not stay right.
    /// </remarks>
    public IEnumerable<(string Label, string Url)> All(bool includeCustom = true)
    {
        if (Present(Facebook)) yield return ("Facebook", Facebook!.Trim());
        if (Present(X)) yield return ("X", X!.Trim());
        if (Present(Instagram)) yield return ("Instagram", Instagram!.Trim());
        if (Present(LinkedIn)) yield return ("LinkedIn", LinkedIn!.Trim());
        if (Present(YouTube)) yield return ("YouTube", YouTube!.Trim());
        if (Present(TikTok)) yield return ("TikTok", TikTok!.Trim());
        if (Present(Pinterest)) yield return ("Pinterest", Pinterest!.Trim());
        if (Present(Snapchat)) yield return ("Snapchat", Snapchat!.Trim());
        if (Present(Reddit)) yield return ("Reddit", Reddit!.Trim());
        if (Present(Twitch)) yield return ("Twitch", Twitch!.Trim());
        if (Present(Threads)) yield return ("Threads", Threads!.Trim());
        if (Present(Discord)) yield return ("Discord", Discord!.Trim());

        if (!includeCustom)
            yield break;

        foreach (var (label, url) in Custom)
        {
            if (Present(label) && Present(url))
                yield return (label.Trim(), url.Trim());
        }

        static bool Present(string? value) => !string.IsNullOrWhiteSpace(value);
    }
}
