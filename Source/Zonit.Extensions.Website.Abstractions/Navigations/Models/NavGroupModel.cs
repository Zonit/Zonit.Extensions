using Zonit.Extensions.Website.Abstractions.Navigations.Types;

namespace Zonit.Extensions.Website.Abstractions.Navigations.Models;

public class NavGroupModel
{
    public string Title { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public LinkModel? Link { get; set; }
    public string? Permission { get; set; }
    public bool Expanded { get; set; } = false;
    public List<NavLinkModel>? Children { get; set; }
    public AreaType? Area { get; set; }
    public string? Position { get; set; }
    public int Order { get; set; }
}