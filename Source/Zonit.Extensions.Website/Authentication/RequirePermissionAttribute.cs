using Microsoft.AspNetCore.Authorization;

namespace Zonit.Extensions.Website.Authentication;

/// <summary>
/// Declarative permission requirement based on <see cref="Permission"/> value object.
/// Works with the standard ASP.NET Core / Blazor authorization pipeline:
/// <c>[Authorize]</c> attribute discovery, <c>AuthorizeView</c>,
/// minimal-API <c>.RequireAuthorization(...)</c>, controllers and Razor pages.
/// </summary>
/// <remarks>
/// <para>Uses .NET 8+ <see cref="IAuthorizationRequirementData"/> contract — the attribute
/// IS its own requirement, no manual policy registration in <c>AddAuthorization</c>.</para>
///
/// <para>Wildcards: the requirement is checked by <see cref="PermissionAuthorizationHandler"/>
/// using <see cref="Permission.Implies"/>, so a user granted <c>orders.*</c> satisfies
/// <c>[RequirePermission("orders.read")]</c>.</para>
///
/// <example>
/// <code>
/// // Blazor / Razor pages:
/// [RequirePermission("orders.read")]
/// public partial class OrdersPage { }
///
/// // Minimal API:
/// app.MapGet("/orders", () => ...).RequireAuthorization(new RequirePermissionAttribute("orders.read"));
///
/// // Controllers:
/// [RequirePermission("orders.write")]
/// public IActionResult Update(...) => ...;
/// </code>
/// </example>
/// </remarks>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Interface,
    AllowMultiple = true,
    Inherited = true)]
public sealed class RequirePermissionAttribute
    : AuthorizeAttribute, IAuthorizationRequirement, IAuthorizationRequirementData
{
    /// <summary>The permission token required (may be wildcard, e.g. <c>orders.*</c>).</summary>
    public Permission Permission { get; }

    /// <summary>Underlying string token, exposed for diagnostics / policy naming.</summary>
    public string Token => Permission.Value;

    /// <param name="permission">Permission token (dot-separated, supports <c>*</c> wildcard).</param>
    /// <exception cref="FormatException">If <paramref name="permission"/> is not a valid permission.</exception>
    public RequirePermissionAttribute(string permission)
    {
        Permission = Permission.Create(permission);
        // Use a synthetic policy name so each (token) gets a distinct fallback policy entry.
        Policy = $"zonit:permission:{Permission.Value}";
    }

    /// <inheritdoc />
    public IEnumerable<IAuthorizationRequirement> GetRequirements()
    {
        yield return this;
    }
}
