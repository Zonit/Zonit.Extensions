namespace Zonit.Extensions.Organizations;

public class StateModel
{
    public WorkspaceModel? Workspace { get; set; }
    public IReadOnlyCollection<OrganizationModel>? Organizations { get; set; }
}