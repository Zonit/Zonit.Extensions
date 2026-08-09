using System.ComponentModel.DataAnnotations;

namespace Zonit.Extensions.Tenants.Settings;

/// <summary>
/// Built-in tenant setting carrying the visual theme (brand / surface colors,
/// typography, roundness, shadows). Ships with empty <see cref="Setting{T}.Templates"/>
/// — hosts can supply presets via a derived class or the admin UI.
/// </summary>
public sealed class ThemeSetting : Setting<ThemeSettingsModel>
{
    public override string Key => "theme";
    public override string Name => "Theme";
    public override string Description => "Visual theme and styling settings for the website.";
    public override IReadOnlyCollection<ThemeSettingsModel>? Templates { get; } = [];
}

/// <summary>
/// Colour-scheme preference. <see cref="System"/> means "whatever the operating system says",
/// which is the right default — a visitor whose OS is set to dark has already expressed a
/// preference and should not have to express it again.
/// </summary>
/// <remarks>
/// Named for the CSS property it drives rather than "theme", which in this settings tree already
/// means the palette. This is only light versus dark.
/// </remarks>
public enum ColorScheme
{
    /// <summary>Follow <c>prefers-color-scheme</c>.</summary>
    System = 0,

    /// <summary>Default to light when the visitor has expressed no preference.</summary>
    Light = 1,

    /// <summary>Default to dark when the visitor has expressed no preference.</summary>
    Dark = 2,
}

/// <summary>Model for <see cref="ThemeSetting"/>.</summary>
public sealed class ThemeSettingsModel
{
    [Required]
    [Display(Name = "Color Scheme", Description = "Light or dark by default. 'System' follows the visitor's operating system.")]
    public ColorScheme ColorScheme { get; set; } = ColorScheme.System;

    // Brand
    [Required, ColorPicker]
    [Display(Name = "Primary Color", Description = "Main brand color used throughout the site for buttons and key elements")]
    public string PrimaryColor { get; set; } = "#2563EB";

    [Required, ColorPicker]
    [Display(Name = "Secondary Color", Description = "Supporting brand color for accents and secondary elements")]
    public string SecondaryColor { get; set; } = "#7C3AED";

    [Required, ColorPicker]
    [Display(Name = "Accent Color", Description = "Highlight color for special elements and call-to-action buttons")]
    public string AccentColor { get; set; } = "#DC2626";

    // Surface
    [Required, ColorPicker]
    [Display(Name = "Background Color", Description = "Main page background color and sections")]
    public string NeutralColor { get; set; } = "#F1F5F9";

    [Required, ColorPicker]
    [Display(Name = "Surface Color", Description = "Background color for cards, panels and elevated elements")]
    public string SurfaceColor { get; set; } = "#FFFFFF";

    [Required, ColorPicker]
    [Display(Name = "Text Color", Description = "Primary text color for content and headings")]
    public string ContentColor { get; set; } = "#0F172A";

    // Typography
    [Required]
    [Display(Name = "Font Family", Description = "Main font for all text")]
    public FontFamily FontFamily { get; set; } = FontFamily.Inter;

    [Required]
    [Display(Name = "Font Scale", Description = "Overall text size scale")]
    public FontScale FontScale { get; set; } = FontScale.Normal;

    // Style
    [Required]
    [Display(Name = "Roundness", Description = "Corner rounding intensity")]
    public Roundness Roundness { get; set; } = Roundness.Medium;

    [Required]
    [Display(Name = "Shadow", Description = "Shadow intensity for elevated elements")]
    public Shadow Shadow { get; set; } = Shadow.Small;
}

public enum FontFamily { Inter, Roboto, OpenSans, Poppins, Montserrat, Nunito, PlusJakartaSans }
public enum FontScale  { Small, Normal, Large }
public enum Roundness  { None, Small, Medium, Large }
public enum Shadow     { None, Small, Medium, Large }
