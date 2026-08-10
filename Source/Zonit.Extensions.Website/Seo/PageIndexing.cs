using Microsoft.AspNetCore.Authorization;
using System.Collections.Concurrent;
using System.Reflection;

namespace Zonit.Extensions.Website;

/// <summary>
/// Whether a routed page should carry <c>noindex</c> purely because reaching it needs an
/// authenticated principal.
/// </summary>
/// <remarks>
/// <para>Not a <c>Disallow</c> in <c>robots.txt</c>, deliberately. That file is written for
/// anyone to read, so listing every gated path publishes a map of where the interesting parts
/// are — and it would not even do the job: a disallowed URL can still be indexed bare, because
/// the crawler is forbidden from fetching the page that would have told it not to.</para>
///
/// <para>An anonymous crawler is redirected to the login page and never renders this one, so the
/// tag is belt-and-braces. The belt is free, it keeps <c>robots.txt</c> to real crawl policy, and
/// it catches the stray bot that arrives holding a session.</para>
/// </remarks>
public static class PageIndexing
{
    private static readonly ConcurrentDictionary<Type, bool> Cache = new();

    /// <summary>
    /// <see langword="true"/> when the page carries an authorization requirement. Cached:
    /// <c>GetCustomAttribute</c> walks metadata on every call and this runs on every navigation.
    /// </summary>
    /// <remarks>
    /// <c>[RequirePermission]</c> and <c>[RequireRole]</c> derive from
    /// <see cref="AuthorizeAttribute"/> — they are synthetic policy names, not a separate
    /// mechanism — so one check covers all three. <c>[AllowAnonymous]</c> wins, matching how
    /// ASP.NET Core resolves the same pair.
    /// </remarks>
    public static bool RequiresAuthorization(Type pageType)
    {
        ArgumentNullException.ThrowIfNull(pageType);
        return Cache.GetOrAdd(pageType, static t
            => t.GetCustomAttribute<AllowAnonymousAttribute>() is null
            && t.GetCustomAttribute<AuthorizeAttribute>() is not null);
    }
}
