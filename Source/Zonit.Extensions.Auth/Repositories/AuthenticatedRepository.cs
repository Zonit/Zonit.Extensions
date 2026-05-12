namespace Zonit.Extensions.Auth.Repositories;

internal sealed class AuthenticatedRepository : IAuthenticatedRepository
{
    private Identity _current = Identity.Empty;

    public Identity Current => _current;

    public void Initialize(Identity identity) => _current = identity;
}