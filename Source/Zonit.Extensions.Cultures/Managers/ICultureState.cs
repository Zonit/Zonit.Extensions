using System.Collections.Immutable;

namespace Zonit.Extensions.Cultures;

/// <summary>
/// Read-only view of the current culture / time-zone state for the active scope (request /
/// circuit). Anyone who only needs to <i>read</i> culture (renderers, repositories, model
/// binders) should depend on this interface rather than on the wider <see cref="ICultureManager"/>.
/// </summary>
/// <remarks>
/// <para>Lifetime: <c>Scoped</c>. ASP.NET Core middleware and Blazor circuits each maintain
/// their own instance, eliminating cross-request races inherent in a singleton.</para>
///
/// <para>Decoupling read from write follows the SRP and lets consumers express intent at
/// the dependency level — handy when reviewing code or wiring tests.</para>
/// </remarks>
public interface ICultureState
{
    /// <summary>Currently active culture for this scope (BCP 47).</summary>
    Culture Current { get; }

    /// <summary>
    /// Currently active time-zone for this scope as a <see cref="Extensions.TimeZone"/>
    /// value object. Accepts named zones (IANA / Windows id) or fixed offsets; see
    /// <see cref="Extensions.TimeZone"/> for the full grammar.
    /// </summary>
    TimeZone TimeZone { get; }

    /// <summary>Supported cultures (process-wide configuration).</summary>
    ImmutableArray<LanguageModel> Supported { get; }

    /// <summary>Raised when the culture or time-zone changes within this scope.</summary>
    event Action? OnChange;
}
