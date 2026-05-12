namespace Zonit.Extensions.Organizations;

/// <summary>
/// Read-only API exposing the user's currently selected workspace organization as an
/// <see cref="Extensions.Organization"/> value object.
/// </summary>
/// <remarks>
/// <para>Authorization checks (permissions / roles) are NOT part of this contract — those
/// belong to the standard ASP.NET Core authorization stack registered by
/// <c>AddAuthExtension()</c> (see <c>RequirePermissionAttribute</c>, <c>IAuthorizationService</c>).
/// Mixing both led to dual-source-of-truth bugs and hardcoded "developer" bypasses in the
/// previous implementation.</para>
/// </remarks>
public interface IWorkspaceProvider
{
    /// <summary>
    /// Current organization (<see cref="Extensions.Organization"/> VO).
    /// Returns <see cref="Extensions.Organization.Empty"/> when no workspace is selected
    /// or the user has no organization membership.
    /// </summary>
    Organization Organization { get; }

    /// <summary>Raised when the active workspace organization changes.</summary>
    event Action? OnChange;
}