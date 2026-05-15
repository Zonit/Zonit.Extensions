namespace Zonit.Extensions.Auth;

/// <summary>
/// Safe-default <see cref="IAuthSource"/>: every lookup yields the anonymous identity
/// / null user. The library registers this via <c>TryAdd*</c> so that a host which
/// never wires its own implementation still boots without exceptions.
/// </summary>
internal sealed class NullAuthSource : IAuthSource, IUserDirectory
{
    public Task<Identity> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
        => Task.FromResult(Identity.Empty);

    public Task<UserModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult<UserModel?>(null);

    public Task<UserModel?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default)
        => Task.FromResult<UserModel?>(null);
}
