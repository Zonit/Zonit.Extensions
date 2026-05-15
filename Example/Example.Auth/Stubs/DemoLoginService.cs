using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Zonit.Extensions.Auth;

using Example.Shared;

namespace Example.Auth.Stubs;

/// <summary>
/// HTTP endpoints for the demo's auth flow. Login posts a username/password form, we
/// validate against <see cref="DemoStore.Credentials"/>, register a session token and
/// issue the <c>Session</c> cookie. On the next request <c>AuthenticationSchemeService</c>
/// reads that cookie, calls <see cref="IAuthSource.GetByTokenAsync"/> and hydrates the
/// <see cref="System.Security.Claims.ClaimsPrincipal"/> — so we DON'T call
/// <c>SignInAsync</c> here (the Zonit scheme is a cookie-reading
/// <c>AuthenticationHandler</c>, not an <c>IAuthenticationSignInHandler</c>).
/// </summary>
public static class DemoLoginService
{
    public static async Task LoginAsync(HttpContext httpContext)
    {
        var form = await httpContext.Request.ReadFormAsync();
        var username = form["username"].ToString();
        var password = form["password"].ToString();
        var returnUrl = form["returnUrl"].ToString();
        if (string.IsNullOrWhiteSpace(returnUrl)) returnUrl = "/";

        var store = httpContext.RequestServices.GetRequiredService<DemoStore>();

        if (!store.Credentials.TryGetValue(username, out var cred) || cred.Password != password)
        {
            httpContext.Response.Redirect($"/login?error=invalid&returnUrl={Uri.EscapeDataString(returnUrl)}");
            return;
        }
        if (!store.Users.ContainsKey(cred.UserId))
        {
            httpContext.Response.Redirect($"/login?error=invalid&returnUrl={Uri.EscapeDataString(returnUrl)}");
            return;
        }

        var token = Guid.NewGuid().ToString("N");
        store.SessionsByToken[token] = cred.UserId;

        // Issue the Session cookie — AuthenticationSchemeService.HandleAuthenticateAsync
        // reads this on every subsequent request to hydrate ClaimsPrincipal via IAuthSource.
        httpContext.Response.Cookies.Append(AuthExtensions.SessionCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = httpContext.Request.IsHttps,
        });

        httpContext.Response.Redirect(returnUrl);
    }

    public static Task LogoutAsync(HttpContext httpContext)
    {
        var token = httpContext.Request.Cookies[AuthExtensions.SessionCookieName];
        if (!string.IsNullOrEmpty(token))
        {
            var store = httpContext.RequestServices.GetRequiredService<DemoStore>();
            store.SessionsByToken.TryRemove(token, out _);
        }

        httpContext.Response.Cookies.Delete(AuthExtensions.SessionCookieName);
        httpContext.Response.Redirect("/");
        return Task.CompletedTask;
    }
}
