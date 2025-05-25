namespace Zonit.Extensions.Website;

public class WebsiteOptions
{
    /// <summary>
    /// Listening address of the site
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// A unique token for the site, for example, a one-time password
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Proxy server support? Nginx requires this
    /// </summary>
    public bool Proxy { get; set; } = false;
}