namespace Zonit.Extensions.Organizations;

/// <summary>
/// Managing the state of the organization
/// </summary>
public interface IOrganizationManager
{
    /// <summary>
    /// Information about the organization
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public Task<OrganizationModel?> GetAsync(Guid id);
}
