namespace Zonit.Extensions.Tenants.Settings;

/// <summary>
/// Proof-of-ownership tokens for the platforms that ask a domain to prove itself.
/// </summary>
public sealed class VerificationSetting : Setting<VerificationModel>
{
    public override string Key => "verification";
    public override string Name => "Verification";
    public override string Description => "Ownership tokens for search engines and app platforms.";
}

/// <summary>
/// Tokens a platform hands out to confirm that whoever configures this site controls the domain.
/// </summary>
/// <remarks>
/// <para><b>Why the tenant and not configuration.</b> A token belongs to a <em>domain</em>, and in
/// a multi-site host each tenant answers on its own — one <c>appsettings.json</c> value would give
/// every brand the same token and verify none of them. It is also obtained by a person clicking
/// through a console, pasted once, and never touched again: exactly the kind of value that should
/// not need a deployment.</para>
///
/// <para><b>Not a secret.</b> Every one of these is published in the page or at a well-known URL —
/// that is the entire mechanism. Treat them as public identifiers, not credentials.</para>
///
/// <para>Store the <em>token</em>, not the tag. Consoles offer a full
/// <c>&lt;meta name="…" content="…"&gt;</c> snippet to copy; paste only the <c>content</c> value.
/// The framework writes the tag, so the name can never be wrong.</para>
/// </remarks>
public sealed class VerificationModel
{
    /// <summary>Google Search Console — rendered as <c>google-site-verification</c>.</summary>
    public string? Google { get; set; }

    /// <summary>Bing Webmaster Tools — rendered as <c>msvalidate.01</c>.</summary>
    public string? Bing { get; set; }

    /// <summary>Yandex Webmaster — rendered as <c>yandex-verification</c>.</summary>
    public string? Yandex { get; set; }

    /// <summary>Pinterest claimed domain — rendered as <c>p:domain_verify</c>.</summary>
    public string? Pinterest { get; set; }

    /// <summary>Meta / Facebook Business domain — rendered as <c>facebook-domain-verification</c>.</summary>
    public string? Facebook { get; set; }

    /// <summary>
    /// Anything with a meta-tag mechanism the properties above do not name, as
    /// <c>meta name</c> → token.
    /// </summary>
    /// <remarks>
    /// The key is the tag's <c>name</c> attribute verbatim, because that is what the platform's
    /// console tells you and any translation here would be a second place to be wrong:
    /// <code>
    /// "Custom": { "ahrefs-site-verification": "…", "norton-safeweb-site-verification": "…" }
    /// </code>
    /// </remarks>
    public Dictionary<string, string> Custom { get; set; } = [];

    /// <summary>
    /// Apple's <c>apple-app-site-association</c> document, verbatim JSON, served from
    /// <c>/.well-known/</c>.
    /// </summary>
    /// <remarks>
    /// <para>Apple does not verify domains with a meta tag. Universal Links, Handoff, Shared Web
    /// Credentials and App Clips are all driven by this one file, and the association is what
    /// proves the app and the domain belong together.</para>
    ///
    /// <para>Stored as raw JSON rather than modelled: the schema is Apple's, it gains keys between
    /// OS releases, and a model here would silently drop whatever it does not know about. Paste
    /// what the console or your app team produced.</para>
    /// </remarks>
    public string? AppleAppSiteAssociation { get; set; }

    /// <summary>
    /// Apple Pay merchant domain verification file, verbatim, served from <c>/.well-known/</c> as
    /// <c>apple-developer-merchantid-domain-association</c>.
    /// </summary>
    /// <remarks>
    /// A separate mechanism from <see cref="AppleAppSiteAssociation"/> and required before a domain
    /// may take Apple Pay. The file Apple hands out has no extension and is served as plain text.
    /// </remarks>
    public string? AppleMerchant { get; set; }

    /// <summary>
    /// Every meta-tag token the tenant filled in, as <c>name</c> → content, blanks skipped.
    /// </summary>
    /// <remarks>
    /// One enumeration for every consumer, so a platform added here appears in the head without a
    /// second list being edited. <see cref="AppleAppSiteAssociation"/> and
    /// <see cref="AppleMerchant"/> are deliberately absent — they are files, not tags.
    /// </remarks>
    public IEnumerable<(string Name, string Content)> Metas()
    {
        if (Present(Google)) yield return ("google-site-verification", Google!.Trim());
        if (Present(Bing)) yield return ("msvalidate.01", Bing!.Trim());
        if (Present(Yandex)) yield return ("yandex-verification", Yandex!.Trim());
        if (Present(Pinterest)) yield return ("p:domain_verify", Pinterest!.Trim());
        if (Present(Facebook)) yield return ("facebook-domain-verification", Facebook!.Trim());

        foreach (var (name, content) in Custom)
        {
            if (Present(name) && Present(content))
                yield return (name.Trim(), content.Trim());
        }

        static bool Present(string? value) => !string.IsNullOrWhiteSpace(value);
    }
}
