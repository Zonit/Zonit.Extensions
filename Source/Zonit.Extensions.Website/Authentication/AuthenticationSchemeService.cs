using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;
using Zonit.Extensions.Auth;

namespace Zonit.Extensions.Website.Authentication;

/// <summary>
/// Cookie-based authentication handler that converts the <c>Session</c> cookie into a
/// <see cref="System.Security.Claims.ClaimsPrincipal"/> via <see cref="IAuthSource"/>
/// and <see cref="IdentityClaimsBuilder"/>.
/// </summary>
public sealed class AuthenticationSchemeService(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IAuthSource session
    ) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <summary>Name of the ASP.NET Core authentication scheme this handler is registered as.</summary>
    public const string SchemeName = AuthExtensions.SchemeName;

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var sessionValue = Request.Cookies[AuthExtensions.SessionCookieName];
        if (string.IsNullOrEmpty(sessionValue))
            return AuthenticateResult.NoResult();

        var identity = await session.GetByTokenAsync(sessionValue, Context.RequestAborted);
        if (!identity.HasValue)
            return AuthenticateResult.Fail("Unauthorized");

        var principal = IdentityClaimsBuilder.BuildPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return AuthenticateResult.Success(ticket);
    }
}