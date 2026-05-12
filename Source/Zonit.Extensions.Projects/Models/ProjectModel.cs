namespace Zonit.Extensions.Projects;

/// <summary>
/// Represents a project
/// </summary>
public class ProjectModel
{
    /// <summary>
    /// Project ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Project name
    /// </summary>
    public string Name { get; set; } = string.Empty;
}