using Example.Shared;
using Zonit.Extensions;
using Zonit.Extensions.Auth;

namespace Example.Auth.Stubs;

/// <summary>
/// Demo adapter that satisfies both <see cref="IAuthSource"/> (required: token → identity,
/// id → user) and the optional <see cref="IUserDirectory"/> (username → user). One class,
/// multiple contracts — a real consumer can do the same when their data layer
/// naturally covers all three lookups (e.g. a single EF <c>DbContext</c>).
/// </summary>
internal sealed class InMemoryAuthSource(DemoStore store) : IAuthSource, IUserDirectory
{
    public Task<Identity> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        if (!store.SessionsByToken.TryGetValue(token, out var userId))
            return Task.FromResult(Identity.Empty);

        if (!store.Users.TryGetValue(userId, out var user))
            return Task.FromResult(Identity.Empty);

        return Task.FromResult(store.HydrateIdentity(user));
    }

    public Task<UserModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(store.Users.TryGetValue(id, out var u) ? u : null);

    public Task<UserModel?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default)
    {
        var match = store.Users.Values
            .FirstOrDefault(u => string.Equals(u.Name, userName, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult<UserModel?>(match);
    }
}
