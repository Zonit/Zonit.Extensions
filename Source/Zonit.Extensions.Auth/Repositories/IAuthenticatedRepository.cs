namespace Zonit.Extensions.Auth.Repositories;

/// <summary>
/// Per-request / per-circuit storage of the currently authenticated <see cref="Identity"/>.
/// Lifecycle: <c>Scoped</c>. <c>SessionMiddleware</c> populates it once per request.
/// </summary>
public interface IAuthenticatedRepository
{
    /// <summary>Currently set identity, or <see cref="Identity.Empty"/> if none.</summary>
    Identity Current { get; }

    /// <summary>Stores the identity snapshot for the lifetime of the current scope.</summary>
    void Initialize(Identity identity);
}