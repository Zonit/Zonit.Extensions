namespace Zonit.Extensions.Projects;

/// <summary>
/// Interface for entities that are associated with a project
/// </summary>
public interface IProjectEntity
{
    /// <summary>
    /// Project ID
    /// </summary>
    public Guid? ProjectId { get; set; }

    /// <summary>
    /// Project Model
    /// </summary>
    public ProjectModel? Project { get; init; }
}
