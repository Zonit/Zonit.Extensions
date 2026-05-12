namespace Zonit.Extensions.Projects;

public class StateModel
{
    public CatalogModel? Catalog { get; set; }
    public IReadOnlyCollection<ProjectModel>? Projects { get; set; }
}