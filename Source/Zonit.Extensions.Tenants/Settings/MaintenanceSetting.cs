using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Zonit.Extensions.Tenants.Settings;

/// <summary>
/// Built-in tenant setting controlling maintenance-mode banner / 503 page.
/// </summary>
public sealed class MaintenanceSetting : Setting<MaintenanceSettingsModel>
{
    public override string Key => "maintenance";
    public override string Name => "Maintenance";
    public override string Description => "Controls website availability and displays maintenance messages when the site is offline.";

    public override MaintenanceSettingsModel Hydrate(string json)
        => JsonSerializer.Deserialize(json, TenantsJsonContext.Default.MaintenanceSettingsModel) ?? new();
}

/// <summary>Model for <see cref="MaintenanceSetting"/>.</summary>
public sealed class MaintenanceSettingsModel
{
    [Display(Name = "Maintenance Active", Description = "Controls whether maintenance mode is active. When enabled, visitors will see the maintenance message.")]
    [Required]
    public bool IsActive { get; set; }

    [Display(Name = "Maintenance Message", Description = "The message shown to visitors when the website is temporarily unavailable.")]
    [StringLength(2000, MinimumLength = 10, ErrorMessage = "Maintenance message must be 10-2000 characters.")]
    public string? MaintenanceMessage { get; set; }
}
