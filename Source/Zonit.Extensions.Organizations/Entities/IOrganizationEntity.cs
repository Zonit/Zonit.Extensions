namespace Zonit.Extensions.Organizations;

public interface IOrganizationEntity
{
    /// <summary>
    /// Organization ID
    /// </summary>
    public Guid? OrganizationId { get; set; }

    /// <summary>
    /// Data of the organization
    /// </summary>
    public OrganizationModel? Organization { get; init; }
}
