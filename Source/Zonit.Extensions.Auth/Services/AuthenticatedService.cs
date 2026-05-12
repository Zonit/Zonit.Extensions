using Zonit.Extensions.Auth.Repositories;

namespace Zonit.Extensions.Auth.Services;

internal sealed class AuthenticatedService(IAuthenticatedRepository repository) : IAuthenticatedProvider
{
    public Identity Current => repository.Current;
}