namespace Zonit.Extensions.Auth.Repositories;

internal sealed class AuthenticatedRepository : IAuthenticatedRepository
{
    private Identity _current = Identity.Empty;

    public Identity Current => _current;

    /// <inheritdoc />
    public event Action? OnChange;

    /// <inheritdoc />
    public void Initialize(Identity identity)
    {
        var changed = !_current.Equals(identity);
        _current = identity;
        if (changed)
            OnChange?.Invoke();
    }
}