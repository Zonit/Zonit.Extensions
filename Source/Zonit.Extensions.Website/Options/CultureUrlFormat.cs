namespace Zonit.Extensions.Website;

/// <summary>
/// Shape of the culture segment under <see cref="CultureUrlStrategy.Prefix"/> —
/// <c>/pl/pricing</c> versus <c>/pl-pl/pricing</c>.
/// </summary>
/// <remarks>
/// <para>This picks the <b>canonical</b> form only. Both spellings stay routable either way:
/// the non-canonical one answers with a permanent redirect to the canonical one, so a
/// hand-typed or externally linked <c>/pl-pl/pricing</c> never dead-ends and never becomes a
/// second live address for the same page.</para>
///
/// <para>The choice is not applied blindly. <see cref="Short"/> degrades to the full tag for
/// any language whose primary subtag is claimed by more than one supported culture — with
/// <c>pt-pt</c> and <c>pt-br</c> both configured there is no truthful expansion of
/// <c>/pt/</c>, so those two keep their regions while <c>/de/</c>, <c>/fr/</c> and the rest
/// stay short. Adding Brazilian Portuguese lengthens two URLs, not all twenty.</para>
/// </remarks>
public enum CultureUrlFormat
{
    /// <summary>
    /// Primary subtag only — <c>/pl/</c>, <c>/de/</c>, <c>/fr/</c>. The common choice when
    /// each language is served in a single regional flavour. Falls back to <see cref="Full"/>
    /// per-language for ambiguous subtags.
    /// </summary>
    Short = 0,

    /// <summary>
    /// Complete BCP-47 tag — <c>/pl-pl/</c>, <c>/en-us/</c>. Use when regional variants are a
    /// first-class part of the offering and the precision is worth the length.
    /// </summary>
    Full = 1,
}
