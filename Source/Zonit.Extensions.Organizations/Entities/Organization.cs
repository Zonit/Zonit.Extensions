namespace Zonit.Extensions.Organizations.Abstractions.Entities;

public class Organization
{
    public Guid Id { get; set; }

    /// <summary>
    /// Organization name
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Date of creation
    /// </summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}