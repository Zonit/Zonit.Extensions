using System.Globalization;

namespace Zonit.Extensions.Cultures;

/// <summary>
/// Static metadata for a language supported by the Cultures extension. Each concrete
/// subclass is a singleton, discovered by <c>LanguageService</c> at startup, and serves
/// as the source of truth for language pickers, RTL layout switching, and any UI that
/// needs to display the language identity itself.
/// </summary>
/// <remarks>
/// <para>Date / time / number formatting deliberately live on <see cref="CultureInfo"/>
/// (resolved on the consumer side via <c>Culture.ToCultureInfo()</c> or the cached
/// <c>DateTimeFormatModel</c>) — duplicating them here would invite drift. The model
/// concentrates on what <see cref="CultureInfo"/> doesn't carry well (a curated flag
/// icon, RTL flag, primary-subtag aliases).</para>
/// </remarks>
public abstract class LanguageModel
{
    /// <summary>
    /// The language tag in BCP-47 form (lowercased, e.g. <c>"en-us"</c>, <c>"pl-pl"</c>).
    /// </summary>
    public abstract string Code { get; }

    /// <summary>
    /// The name of the language in English (e.g. <c>"Polish"</c>).
    /// </summary>
    public abstract string EnglishName { get; }

    /// <summary>
    /// The name of the language in its own script / locale (e.g. <c>"Polski"</c>,
    /// <c>"العربية"</c>). Defaults to <see cref="EnglishName"/> when not overridden —
    /// safe fallback for languages where the two are identical.
    /// </summary>
    public virtual string NativeName => EnglishName;

    /// <summary>
    /// The icon representing the flag of the language's primary country (e.g. emoji
    /// or CSS class — left up to the concrete model). Used as a visual cue in language
    /// pickers; never treated as semantic identity.
    /// </summary>
    public abstract string IconFlag { get; }

    /// <summary>
    /// <see langword="true"/> for right-to-left scripts (Arabic, Hebrew, Persian, …).
    /// Drives the <c>dir="rtl"</c> attribute on the document root and any per-component
    /// flips. Default is <see langword="false"/>; RTL subclasses override.
    /// </summary>
    public virtual bool IsRightToLeft => false;

    /// <summary>
    /// Additional BCP-47 tags that should resolve to this language model. Lets a single
    /// <c>"en-us"</c> entry match incoming <c>"en"</c>, <c>"en-gb"</c>, <c>"en-au"</c>
    /// requests without forcing the consumer to register one model per region.
    /// </summary>
    /// <remarks>
    /// Resolution order in <c>LanguageService</c> is: exact <see cref="Code"/> match
    /// first, then primary-subtag (the prefix before the hyphen), then anything in
    /// <see cref="AlternativeCodes"/>. Keep entries lowercased and BCP-47-shaped.
    /// </remarks>
    public virtual IReadOnlyList<string> AlternativeCodes => Array.Empty<string>();
}
