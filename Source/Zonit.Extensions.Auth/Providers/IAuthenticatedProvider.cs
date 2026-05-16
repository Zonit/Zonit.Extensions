namespace Zonit.Extensions.Auth;

/// <summary>
/// Exposes the currently authenticated <see cref="Identity"/> in a scoped (per request /
/// per circuit) manner. <see cref="Identity"/> is a lightweight snapshot containing Id,
/// display name, roles and permissions — sufficient for UI / authorization decisions.
/// </summary>
/// <remarks>
/// For anonymous users <see cref="Current"/> is <see cref="Identity.Empty"/>
/// (its <see cref="Identity.HasValue"/> is <c>false</c>). Consumers should branch on that.
/// </remarks>
public interface IAuthenticatedProvider
{
    /// <summary>Currently authenticated identity, or <see cref="Identity.Empty"/> for anonymous.</summary>
    Identity Current { get; }

    /// <summary><c>true</c> when <see cref="Current"/> is a non-empty identity.</summary>
    bool IsAuthenticated => Current.HasValue;

    /// <summary>
    /// Raised when <see cref="Current"/> changes (e.g. after a sign-in or sign-out
    /// inside the current Blazor circuit). Mirrors the read-side <c>OnChange</c>
    /// pattern used by the other Zonit providers (workspace / catalog / culture).
    /// </summary>
    event Action? OnChange;
}
