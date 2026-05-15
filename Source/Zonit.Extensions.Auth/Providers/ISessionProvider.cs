namespace Zonit.Extensions.Auth;

/// <summary>
/// Resolves an opaque session token (cookie value) into a hydrated <see cref="Identity"/>.
/// </summary>
/// <remarks>
/// <para>Implementation is consumer-side (typically backed by a database / cache). The contract
/// returns <see cref="Identity"/> — the lightweight VO containing Id, Name, Roles and
/// Permissions — which is then projected into a <see cref="System.Security.Claims.ClaimsPrincipal"/>
/// by the <c>IdentityClaimsBuilder</c> living in <c>Zonit.Extensions.Website.Authentication</c>
/// (the ASP.NET-specific glue is intentionally kept out of this core package).</para>
///
/// <para>TODO (future): split into <c>ISessionProvider</c> (returns full session metadata —
/// issued-at, expires-at, IP, device) and a thin <c>IIdentityProvider</c> on top. The current
/// shape is "session token → identity" because that is what authentication handlers actually need.</para>
/// </remarks>
public interface ISessionProvider
{
    /// <summary>
    /// Resolves a session token to an <see cref="Identity"/>. Returns <see cref="Identity.Empty"/>
    /// when the token is unknown / expired.
    /// </summary>
    Task<Identity> GetByTokenAsync(string token, CancellationToken cancellationToken = default);
}
