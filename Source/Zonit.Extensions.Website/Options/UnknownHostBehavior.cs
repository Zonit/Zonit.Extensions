namespace Zonit.Extensions.Website;

/// <summary>
/// How <c>TenantMiddleware</c> answers a request whose host no registered
/// <c>ITenantSource</c> recognises.
/// </summary>
/// <seealso cref="WebsiteOptions.UnknownHost"/>
public enum UnknownHostBehavior
{
    /// <summary>
    /// Refuse the request with <c>404 Not Found</c> and do not run the rest of the pipeline.
    /// The default.
    /// </summary>
    /// <remarks>
    /// Chosen as the default because the alternative fails silently: an unrecognised host served
    /// with compile-time default branding is indistinguishable, to a visitor and to most
    /// monitoring, from a working site. The unknown host is also logged at <c>Warning</c> by the
    /// repository regardless of this setting.
    /// </remarks>
    NotFound = 0,

    /// <summary>
    /// Carry on with <c>Tenant.Default</c>, leaving the decision to the application.
    /// </summary>
    /// <remarks>
    /// The right choice when unknown hosts are expected — a catch-all landing page, a health
    /// probe hitting the container's internal name, a marketing site that answers on any domain.
    /// Pages can still branch on <c>ITenantProvider.Resolution</c>.
    /// </remarks>
    Continue = 1,
}
