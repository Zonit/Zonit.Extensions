namespace Zonit.Extensions.Tenants.Settings;

/// <summary>
/// Non-generic façade over <see cref="ISetting{T}"/> so admin UIs can iterate
/// "all known settings" without knowing the model types at compile time.
/// </summary>
public interface ISetting
{
    /// <summary>Stable storage key (e.g. <c>"site"</c>, <c>"theme"</c>). Used to look up
    /// the JSON blob in <see cref="Tenant.Variables"/> and as the database PK on the
    /// consumer side. Lower-snake-case by convention.</summary>
    string Key { get; }

    /// <summary>Human-readable name shown in admin UI.</summary>
    string Name { get; }

    /// <summary>Human-readable description shown in admin UI.</summary>
    string Description { get; }

    /// <summary>
    /// The hydrated model boxed as <see cref="object"/>. Prefer the strongly-typed
    /// <see cref="ISetting{T}.Value"/> from <see cref="ISetting{T}"/>; this property
    /// exists for reflection-free admin UIs that build forms generically.
    /// </summary>
    object Value { get; }
}

/// <summary>
/// Strongly-typed setting contract. <typeparamref name="T"/> is the POCO model whose
/// properties carry <see cref="System.ComponentModel.DataAnnotations"/> (validation,
/// <c>[Display]</c>, <c>[ColorPicker]</c>) — Blazor <c>EditForm</c> renders directly
/// against it.
/// </summary>
/// <typeparam name="T">Model type. Must be a class with a parameterless constructor so
/// that defaults can be materialised when the tenant has no override.</typeparam>
public interface ISetting<T> : ISetting where T : class, new()
{
    /// <summary>Hydrated model. Defaults are returned when the tenant has no override.</summary>
    new T Value { get; set; }

    /// <summary>
    /// Optional list of preset templates (e.g. "Light theme" / "Dark theme") that the
    /// admin UI can offer one-click. <see langword="null"/> when the setting has no
    /// presets — distinct from "empty list" for clarity in the UI.
    /// </summary>
    IReadOnlyCollection<T>? Templates { get; }

    object ISetting.Value => Value;
}
