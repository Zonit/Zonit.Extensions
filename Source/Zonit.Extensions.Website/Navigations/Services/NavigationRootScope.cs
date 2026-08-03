namespace Zonit.Extensions.Website.Navigations.Services;

/// <summary>
/// Captures the container's root <see cref="IServiceProvider"/> so a service that is resolved
/// from <em>either</em> the root or a scope can tell the two apart.
/// </summary>
/// <remarks>
/// <para><b>Why this works.</b> The type is registered as a <b>singleton</b>, and Microsoft DI
/// creates every singleton in the root scope. The <see cref="IServiceProvider"/> handed to this
/// constructor is therefore the root provider by construction — never a request or circuit
/// scope — no matter which scope first triggered the resolution.</para>
///
/// <para><b>Why anybody needs it.</b> <see cref="NavigationService"/> is registered
/// <em>transient</em> precisely so a singleton (a menu-seeding <c>IHostedService</c>) can take
/// <see cref="INavigationProvider"/> in its constructor. That means the provider it receives —
/// the <see cref="IServiceProvider"/> it was resolved from — is sometimes the root. Asking the
/// root for the scoped <see cref="ICurrentSite"/> throws
/// <c>InvalidOperationException: Cannot resolve scoped service … from root provider</c> as soon
/// as the host turns on <c>ServiceProviderOptions.ValidateScopes</c>, which the generic host
/// does in Development. A reference comparison against this instance answers "am I in a scope?"
/// deterministically and for the cost of one field read; catching the exception instead would
/// also swallow genuine wiring failures, which is exactly the kind of silence we do not want.</para>
/// </remarks>
internal sealed class NavigationRootScope
{
    /// <param name="root">
    /// Injected by the container. Because this type is a singleton, the value is the root
    /// provider — see the remarks on the class.
    /// </param>
    public NavigationRootScope(IServiceProvider root) => Provider = root;

    /// <summary>The root provider: the one with no request / circuit scope attached.</summary>
    public IServiceProvider Provider { get; }
}
