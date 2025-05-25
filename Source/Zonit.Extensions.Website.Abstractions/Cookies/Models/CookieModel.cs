namespace Zonit.Extensions.Website.Abstractions.Cookies.Models;

public class CookieModel
{
    /// <summary>
    /// The name of the cookie.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The value of the cookie.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// The domain of the cookie.
    /// </summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>
    /// The path on the server in which the cookie will be available.
    /// </summary>
    public string Path { get; set; } = "/";

    /// <summary>
    /// The expiration date and time for the cookie.
    /// </summary>
    public DateTime? Expires { get; set; }

    /// <summary>
    /// Indicates whether the cookie should only be transmitted over secure HTTPS connections.
    /// </summary>
    public bool Secure { get; set; } = true;

    /// <summary>
    /// Indicates whether the cookie is accessible only through HTTP requests and not through client-side scripts like JavaScript.
    /// </summary>
    public bool HttpOnly { get; set; } = false;

    /// <summary>
    /// Indicates whether the cookie is to be sent in cross-site requests (e.g., requests initiated by third-party websites).
    /// </summary>
    public bool SameSite { get; set; } = false;

    /// <summary>
    /// Indicates whether the cookie is a session cookie or a persistent cookie.
    /// </summary>
    public bool Session { get; set; } = false;
}