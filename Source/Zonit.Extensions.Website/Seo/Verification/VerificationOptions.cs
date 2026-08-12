namespace Zonit.Extensions.Website.Verification;

/// <summary>
/// Whether this Site serves Apple's ownership documents from <c>/.well-known/</c>.
/// </summary>
/// <remarks>
/// <para>Search engines verify with a meta tag, and those are rendered from
/// <c>Tenant.Settings.Verification</c> into every page's head with nothing to configure here.
/// Apple is the exception: Universal Links, App Clips and Apple Pay each read a file from a fixed
/// address, so those need routes.</para>
///
/// <para>On by default and harmless when unused — both endpoints answer <c>404</c> until the
/// tenant supplies a value, and no request pays for a route that never matches.</para>
/// </remarks>
public sealed class VerificationOptions
{
    /// <summary>Map the <c>/.well-known/</c> endpoints. <see langword="true"/> by default.</summary>
    public bool Enabled { get; set; } = true;
}
