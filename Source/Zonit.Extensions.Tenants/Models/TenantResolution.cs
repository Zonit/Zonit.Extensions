namespace Zonit.Extensions.Tenants;

/// <summary>
/// How the current scope arrived at <see cref="ITenantProvider.Current"/>.
/// </summary>
/// <remarks>
/// <para><b>Why this exists.</b> <see cref="ITenantProvider.Current"/> is
/// <see langword="null"/> whenever no tenant was resolved, and that single value collapses three
/// very different situations. Two of them a <b>multi-domain</b> host must tell apart: "this app
/// serves one site, defaults are the answer" and "somebody pointed a hostname at us that we have
/// no tenant for". The second is a misconfiguration — a DNS record added without the matching row,
/// a typo'd alias, a staging host leaking into production — and silently serving default branding
/// is the worst possible response to it.</para>
///
/// <para>Note that settings do not depend on any of this: they fall back to configuration and then
/// to their compile-time defaults regardless, so a page renders correctly without ever consulting
/// either <c>Current</c> or this enum.</para>
///
/// <para>Reading this is how a multi-site host reacts:</para>
/// <code>
/// if (Tenants.Resolution is TenantResolution.Unknown)
///     return Results.NotFound();          // or redirect to the marketing site, or 421
/// </code>
///
/// <para><see cref="Unknown"/> is also logged at <c>Warning</c> by the repository, with the host
/// that failed, so it shows up in production without any code being written for it.</para>
/// </remarks>
public enum TenantResolution
{
    /// <summary>
    /// Nothing has tried to resolve a tenant in this scope: a console app, a worker, a test, or a
    /// Blazor circuit before the state bridge restores. <see cref="ITenantProvider.Current"/> is
    /// <see langword="null"/> because nobody asked, not because anybody decided.
    /// </summary>
    None = 0,

    /// <summary>
    /// A resolution was attempted and no <see cref="ITenantSource"/> is registered — the host
    /// serves a single site. A <see langword="null"/> <c>Current</c> is the intended answer here.
    /// </summary>
    SingleSite = 1,

    /// <summary>
    /// An <see cref="ITenantSource"/> returned a tenant for this host.
    /// <see cref="ITenantProvider.Current"/> is that tenant.
    /// </summary>
    Resolved = 2,

    /// <summary>
    /// An <see cref="ITenantSource"/> is registered but did not recognise this host, so the scope
    /// left <see cref="ITenantProvider.Current"/> <see langword="null"/>. In a multi-domain host this is a
    /// misconfiguration worth surfacing rather than rendering through.
    /// </summary>
    Unknown = 3,
}
