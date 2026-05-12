namespace Zonit.Extensions.Organizations;

public class WorkspaceModel
{
    /// <summary>
    /// Current user organization
    /// </summary>
    public OrganizationModel? Organization { get; init; }

    /// <summary>
    /// List of available user permissions in the organization
    /// </summary>
    public string[] Permissions { get; init; } = [];

    /// <summary>
    /// List of available user roles in the organization
    /// </summary>
    public string[] Roles { get; init; } = [];
}
