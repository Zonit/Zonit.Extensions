namespace Zonit.Extensions.Auth;

/// <summary>
/// Consumer-side data adapter exposing the <b>required</b> read-side of auth:
/// session-token → identity lookup, and id → user lookup.
/// </summary>
/// <remarks>
/// <para>The library auto-registers a <c>NullAuthSource</c> via
/// <see cref="DependencyInjection.AuthServiceCollectionExtensions"/>; a host that
/// does <b>not</b> register its own implementation simply sees every request as
/// anonymous (no exceptions, no startup failure).</para>
///
/// <para>One concrete class may implement multiple <c>I*Source</c> contracts at once —
/// e.g. <c>AcmeDataAdapter : IAuthSource, IUserDirectory, IOrganizationSource</c> —
/// or each can live in its own class. Pick whichever fits your data layer.</para>
/// </remarks>
public interface IAuthSource
{
    /// <summary>
    /// Resolves a session token (cookie value) to a hydrated <see cref="Identity"/>.
    /// Return <see cref="Identity.Empty"/> when the token is unknown / expired —
    /// the framework treats that as anonymous.
    /// </summary>
    Task<Identity> GetByTokenAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a user id to a full <see cref="UserModel"/> snapshot (admin panels,
    /// profile pages, audit views). Return <see langword="null"/> when missing.
    /// </summary>
    Task<UserModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
