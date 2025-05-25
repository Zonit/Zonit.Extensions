namespace Zonit.Extensions.Website.Abstractions.Navigations.Models;

public class LinkModel(string title, string? url = null, string? target = null)
{
    public string Title { get; set; } = title;
    public string? Url { get; set; } = url;
    public string? Target { get; set; } = target;
}