namespace Zonit.Extensions.Cultures;

/// <summary>
/// Read / write surface for the current culture state. Adds mutation operations to the
/// read-only <see cref="ICultureState"/>. Components and pages that need to <i>change</i>
/// culture (settings UI, language switcher) should depend on this interface; those that
/// only read should depend on <see cref="ICultureState"/>.
/// </summary>
/// <remarks>
/// Lifetime: <c>Scoped</c>. The middleware (or a Blazor language switcher) calls
/// <see cref="SetCulture(Culture)"/> on this scope's instance and downstream consumers
/// observe the change via <see cref="ICultureState.OnChange"/>.
/// </remarks>
public interface ICultureManager : ICultureState
{
    /// <summary>Updates the active culture for this scope. Raises <see cref="ICultureState.OnChange"/>.</summary>
    void SetCulture(Culture culture);

    /// <summary>Updates the active time-zone for this scope. Raises <see cref="ICultureState.OnChange"/>.</summary>
    void SetTimeZone(string timeZone);
}
