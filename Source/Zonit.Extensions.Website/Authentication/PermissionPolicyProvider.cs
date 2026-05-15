using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Zonit.Extensions.Website.Authentication;

/// <summary>
/// Dynamic <see cref="IAuthorizationPolicyProvider"/> that synthesizes a permission-based
/// policy when the requested name parses as a <see cref="Permission"/> token (e.g.
/// <c>"settings.write"</c> or <c>"orders.*"</c>) — or matches the synthetic prefix
/// emitted by <see cref="RequirePermissionAttribute"/> (<c>"zonit:permission:&lt;token&gt;"</c>).
/// Falls back to <see cref="DefaultAuthorizationPolicyProvider"/> for everything else.
/// </summary>
/// <remarks>
/// <para>Why: <c>[Authorize&lt;RequirePermission&gt;("orders.read")]</c> works without
/// registering a policy because <see cref="RequirePermissionAttribute"/> is its own
/// <see cref="IAuthorizationRequirementData"/>. But the Blazor <c>&lt;AuthorizeView
/// Policy="orders.read"&gt;</c> path goes through <see cref="IAuthorizationPolicyProvider.
/// GetPolicyAsync"/> by name and the default provider doesn't know our tokens, so it
/// throws <c>InvalidOperationException("AuthorizationPolicy named X was not found")</c>.
/// This provider closes that gap.</para>
///
/// <para>Behaviour:</para>
/// <list type="bullet">
///   <item>Name == <c>"zonit:permission:&lt;token&gt;"</c> → policy with
///         <see cref="RequirePermissionAttribute"/> requirement for the embedded token.</item>
///   <item>Name parses as <see cref="Permission"/> via <see cref="Permission.TryCreate"/> →
///         policy with <see cref="RequirePermissionAttribute"/> requirement for the token.</item>
///   <item>Otherwise → defer to the default provider (so <c>[Authorize(Policy = "MyCustom")]</c>
///         policies registered with <c>AddAuthorization(o =&gt; o.AddPolicy(...))</c> still work).</item>
/// </list>
///
/// <para>The provider is registered as a singleton by <c>AddWebsite</c>. Synthesized
/// policies are <b>not</b> cached because the default provider already caches by name
/// at the authorization-evaluator level.</para>
/// </remarks>
public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private const string SyntheticPrefix = "zonit:permission:";

    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallback = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()      => _fallback.GetDefaultPolicyAsync();
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()    => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (string.IsNullOrEmpty(policyName))
            return _fallback.GetPolicyAsync(policyName);

        // 1) Exact synthetic-name match emitted by [RequirePermission].
        if (policyName.StartsWith(SyntheticPrefix, StringComparison.Ordinal))
        {
            var token = policyName.AsSpan(SyntheticPrefix.Length).ToString();
            if (Permission.TryCreate(token, out _))
                return Task.FromResult<AuthorizationPolicy?>(BuildPermissionPolicy(token));
        }

        // 2) Plain permission token used directly as a policy name.
        if (Permission.TryCreate(policyName, out _))
            return Task.FromResult<AuthorizationPolicy?>(BuildPermissionPolicy(policyName));

        // 3) Anything else — let the default provider answer (registered AddPolicy(...)).
        return _fallback.GetPolicyAsync(policyName);
    }

    private static AuthorizationPolicy BuildPermissionPolicy(string token)
    {
        // RequirePermissionAttribute is itself an IAuthorizationRequirement, and the
        // PermissionAuthorizationHandler is registered globally — so building a policy
        // from a single requirement is enough; no per-policy handler wiring needed.
        var requirement = new RequirePermissionAttribute(token);
        return new AuthorizationPolicyBuilder()
            .AddRequirements(requirement)
            .Build();
    }
}
