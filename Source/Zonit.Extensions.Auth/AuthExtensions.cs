namespace Zonit.Extensions.Auth;

/// <summary>
/// Shared constants for the Zonit.Extensions.Auth package.
/// </summary>
public static class AuthExtensions
{
    /// <summary>Name of the ASP.NET Core authentication scheme registered by <c>AddAuthExtension</c>.</summary>
    public const string SchemeName = "Zonit";

    /// <summary>Cookie name used to carry the opaque session token.</summary>
    public const string SessionCookieName = "Session";
}
