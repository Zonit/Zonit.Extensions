using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Zonit.Extensions.Auth;
using Zonit.Extensions.Website.Authentication;

using Example.Shared;

namespace Example.Auth.Stubs;

/// <summary>
/// HTTP endpoints for the demo's auth flow. Login posts a username/password form, we
/// validate against <see cref="DemoStore.Credentials"/>, register a session token, build
/// claims via <see cref="IdentityClaimsBuilder"/> and call <c>HttpContext.SignInAsync</c>
/// using the Zonit cookie scheme. Logout clears the cookie + the in-memory session entry.
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
        if (!store.Users.TryGetValue(cred.UserId, out var user))
        {
            httpContext.Response.Redirect($"/login?error=invalid&returnUrl={Uri.EscapeDataString(returnUrl)}");
            return;
        }

        var token = Guid.NewGuid().ToString("N");
        store.SessionsByToken[token] = user.Id;

        // The Zonit auth scheme reads the Session cookie on the next request to hydrate
        // the principal. We sign in via the same scheme so the cookie itself is also issued.
        var identity = store.HydrateIdentity(user);
        var principal = IdentityClaimsBuilder.BuildPrincipal(identity);
        await httpContext.SignInAsync(AuthExtensions.SchemeName, principal);

        // Mirror the token in our session-cookie so SessionMiddleware-style flows that read
        // a separate cookie (some hosts do) also work.
        httpContext.Response.Cookies.Append(AuthExtensions.SessionCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = httpContext.Request.IsHttps,
        });

        httpContext.Response.Redirect(returnUrl);
    }

    public static async Task LogoutAsync(HttpContext httpContext)
    {
        var token = httpContext.Request.Cookies[AuthExtensions.SessionCookieName];
        if (!string.IsNullOrEmpty(token))
        {
            var store = httpContext.RequestServices.GetRequiredService<DemoStore>();
            store.SessionsByToken.TryRemove(token, out _);
            httpContext.Response.Cookies.Delete(AuthExtensions.SessionCookieName);
        }

        await httpContext.SignOutAsync(AuthExtensions.SchemeName);
        httpContext.Response.Redirect("/");
    }
}
