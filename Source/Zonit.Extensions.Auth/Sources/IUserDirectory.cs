namespace Zonit.Extensions.Auth;

/// <summary>
/// <b>Optional</b> add-on to <see cref="IAuthSource"/>: lookup users by their
/// human-typed username (login flows, admin search).
/// </summary>
/// <remarks>
/// Kept separate from <see cref="IAuthSource"/> because not every consumer needs it
/// (e.g. systems that only authenticate via external OIDC by user-id). When unset,
/// the library registers a no-op <c>NullUserDirectory</c> that returns
/// <see langword="null"/> for every query.
/// </remarks>
public interface IUserDirectory
{
    /// <summary>
    /// Resolves a username (case-insensitive by convention) to a
    /// <see cref="UserModel"/>. Return <see langword="null"/> when no match.
    /// </summary>
    Task<UserModel?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default);
}
