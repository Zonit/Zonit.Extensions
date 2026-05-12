using System.Security.Claims;

namespace Zonit.Extensions.Auth.Authorization;

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
}
