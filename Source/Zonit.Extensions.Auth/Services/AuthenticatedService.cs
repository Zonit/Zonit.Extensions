using Zonit.Extensions.Auth.Repositories;

namespace Zonit.Extensions.Auth.Services;

/// <summary>
/// Default read-side adapter over <see cref="IAuthenticatedRepository"/>. Forwards
/// the repository's <c>OnChange</c> event so any consumer that injects only the
/// provider (e.g. Blazor's <c>AuthenticationStateProvider</c>) can react to
/// sign-in / sign-out without taking a hard dependency on the write surface.
/// </summary>
internal sealed class AuthenticatedService : IAuthenticatedProvider, IDisposable
{
    private readonly IAuthenticatedRepository _repository;

    public AuthenticatedService(IAuthenticatedRepository repository)
    {
        _repository = repository;
        _repository.OnChange += HandleRepositoryChanged;
    }

    public Identity Current => _repository.Current;

    public event Action? OnChange;

    private void HandleRepositoryChanged() => OnChange?.Invoke();

    public void Dispose()
    {
        _repository.OnChange -= HandleRepositoryChanged;
    }
}