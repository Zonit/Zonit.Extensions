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

    /// <summary>
    /// Raised after <see cref="Initialize"/> when the new identity differs from the
    /// previously stored one (compared by <see cref="Identity.Equals(Identity)"/>).
    /// Used by Blazor's <c>AuthenticationStateProvider</c> implementation to
    /// notify <c>&lt;AuthorizeView&gt;</c> and the cascading <c>Task&lt;AuthenticationState&gt;</c>
    /// after a sign-in / sign-out within a long-lived circuit.
    /// </summary>
    event Action? OnChange;
}