namespace Zonit.Extensions.Organizations;

/// <summary>
/// 
/// </summary>
public interface IOrganizationProvider
{
    /// <summary>
    /// Gets detailed information about an organization by ID.
    /// Returns null if the organization does not exist or the module is disabled.
    /// </summary>
    /// <param name="organizationId"></param>
    /// <returns></returns>
    public Task<OrganizationModel?> GetOrganizationAsync(Guid organizationId);

    /// <summary>
    /// Gets the default organization for the specified user.
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    public Task<OrganizationModel?> GetUserOrganizationAsync(Guid userId);

    /// <summary>
    /// Gets all organizations the specified user belongs to.
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    public Task<IReadOnlyCollection<OrganizationModel>?> GetUserOrganizationsAsync(Guid userId);
}
