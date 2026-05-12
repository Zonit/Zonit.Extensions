using Microsoft.AspNetCore.Authorization;

namespace Zonit.Extensions.Auth.Authorization;

/// <summary>
/// Declarative role requirement based on <see cref="Role"/> value object.
/// Sister of <see cref="RequirePermissionAttribute"/>, useful when role membership is
/// the authorization axis instead of fine-grained permissions.
/// </summary>
/// <remarks>
/// Equivalent to the built-in <c>[Authorize(Roles = "admin")]</c> for a single role, but:
/// <list type="bullet">
///   <item>Validates the role token at attribute construction time (fails fast).</item>
///   <item>Goes through the same <see cref="IAuthorizationRequirementData"/> pipeline as
///         <see cref="RequirePermissionAttribute"/>, keeping the codebase consistent.</item>
/// </list>
/// </remarks>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Interface,
    AllowMultiple = true,
    Inherited = true)]
public sealed class RequireRoleAttribute
    : AuthorizeAttribute, IAuthorizationRequirement, IAuthorizationRequirementData
{
    public Role Role { get; }

    public string Token => Role.Value;

    public RequireRoleAttribute(string role)
    {
        Role = Role.Create(role);
        Policy = $"zonit:role:{Role.Value}";
    }

    public IEnumerable<IAuthorizationRequirement> GetRequirements()
    {
        yield return this;
    }
}
