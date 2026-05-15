using System.Collections.Immutable;
using System.Security.Claims;

namespace Zonit.Extensions.Website.Authentication;

/// <summary>
/// Central place that converts a <see cref="Identity"/> value object into a
/// <see cref="ClaimsIdentity"/> used by ASP.NET Core authentication / authorization.
/// </summary>
/// <remarks>
/// <para>Eliminates the previous duplication where both <c>SessionAuthenticationService</c>
/// and <c>AuthenticationSchemeService</c> built the principal manually and inconsistently.</para>
///
/// <para>Emitted claims:</para>
/// <list type="bullet">
///   <item><see cref="ClaimTypes.NameIdentifier"/> — <c>Identity.Id</c> (Guid string)</item>
///   <item><see cref="ClaimTypes.Name"/> — <c>Identity.Name</c> (only when non-empty)</item>
///   <item><see cref="ClaimTypes.Role"/> — one claim per <c>Identity.Roles</c></item>
///   <item><see cref="PermissionClaimType"/> — one claim per <c>Identity.Permissions</c>
///         (wildcards preserved, evaluation delegated to <c>Permission.Implies</c>)</item>
/// </list>
/// </remarks>
public static class IdentityClaimsBuilder
{
    /// <summary>Authentication type / scheme name used by the produced identity.</summary>
    public const string AuthenticationType = "Zonit";

    /// <summary>Claim type for permission tokens (<c>Permission</c> VO values).</summary>
    public const string PermissionClaimType = "zonit:permission";

    /// <summary>Builds a populated <see cref="ClaimsIdentity"/> from <paramref name="identity"/>.</summary>
    public static ClaimsIdentity Build(Identity identity)
    {
        if (!identity.HasValue)
            return new ClaimsIdentity();

        var claims = new List<Claim>(capacity: 2 + identity.Roles.Length + identity.Permissions.Length)
        {
            new(ClaimTypes.NameIdentifier, identity.Id.ToString()),
        };

        if (identity.Name.HasValue)
            claims.Add(new Claim(ClaimTypes.Name, identity.Name.Value));

        foreach (var role in identity.Roles)
            claims.Add(new Claim(ClaimTypes.Role, role.Value));

        foreach (var p in identity.Permissions)
            claims.Add(new Claim(PermissionClaimType, p.Value));

        return new ClaimsIdentity(claims, AuthenticationType);
    }

    /// <summary>Convenience: builds a <see cref="ClaimsPrincipal"/> directly.</summary>
    public static ClaimsPrincipal BuildPrincipal(Identity identity)
        => new(Build(identity));

    /// <summary>
    /// Reverse projection: rebuilds an <see cref="Identity"/> value object from the
    /// <see cref="Claim"/>s emitted by <see cref="Build(Identity)"/>. Used by
    /// <c>SessionMiddleware</c> to populate <c>IAuthenticatedRepository</c> from the
    /// already-authenticated <c>HttpContext.User</c> without going back to the
    /// database.
    /// </summary>
    /// <remarks>
    /// <para>This deliberately mirrors <see cref="Build(Identity)"/> 1:1 — same claim
    /// types, same ordering. Any tweak there must be reflected here.</para>
    ///
    /// <para>The <see cref="ClaimsPrincipal"/> may carry claims from <em>multiple</em>
    /// authentication schemes (e.g. cookie + an external OIDC scheme). We accept claims
    /// from any identity the principal owns; <c>FindFirst</c>/<c>FindAll</c> walk every
    /// inner identity. If two schemes disagree on, say, the user id, the first
    /// authenticated identity wins — matching how ASP.NET Core's <c>[Authorize]</c>
    /// resolution treats principals.</para>
    /// </remarks>
    public static Identity Read(ClaimsPrincipal? principal)
    {
        if (principal?.Identity is not { IsAuthenticated: true })
            return Identity.Empty;

        var idClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(idClaim, out var id) || id == Guid.Empty)
            return Identity.Empty;

        var nameClaim = principal.FindFirstValue(ClaimTypes.Name);
        var name = !string.IsNullOrEmpty(nameClaim) && Title.TryCreate(nameClaim, out var t)
            ? t
            : Title.Empty;

        // Roles + permissions are usually short (single-digit each). Build them up in a
        // small builder rather than LINQ to keep the hot path allocation-light. We
        // tolerate malformed values silently — a single bad claim should never fail the
        // whole request.
        var rolesBuilder = ImmutableArray.CreateBuilder<Role>();
        foreach (var c in principal.FindAll(ClaimTypes.Role))
            if (Role.TryCreate(c.Value, out var role))
                rolesBuilder.Add(role);

        var permsBuilder = ImmutableArray.CreateBuilder<Permission>();
        foreach (var c in principal.FindAll(PermissionClaimType))
            if (Permission.TryCreate(c.Value, out var perm))
                permsBuilder.Add(perm);

        return new Identity(id, name, rolesBuilder.ToImmutable(), permsBuilder.ToImmutable());
    }
}
