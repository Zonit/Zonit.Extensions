namespace Zonit.Extensions.Website.Abstractions.Navigations.Models;

public class NavLinkModel
{
    public string Title { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Url { get; set; }
    public string? Permission { get; set; }
    public string? Target { get; set; }
    public int Order { get; set; }

    /// <summary>
    /// true - link musi być w całości zgodny z adresem url
    /// false - link zawiera frazę w adresie url
    /// </summary>
    public bool Match { get; set; } = true;
}