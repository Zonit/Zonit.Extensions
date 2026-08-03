namespace Zonit.Extensions.Tenants.Settings;

/// <summary>
/// Reported through <see cref="ITenantProvider.OnSettingHydrationFailed"/> when a persisted
/// blob exists for a setting but could not be turned into a model.
/// </summary>
/// <remarks>
/// <para><b>Why an event and not a log call.</b> This package is deliberately
/// framework-agnostic — its only dependency is
/// <c>Microsoft.Extensions.DependencyInjection.Abstractions</c>, so it has no
/// <c>ILogger</c> to write to. Before 10.0.0-preview.10 the failure was caught and
/// discarded, which made a corrupt blob indistinguishable from "this tenant has no
/// override": the page rendered compile-time defaults and nothing anywhere recorded why.
/// Hosts should subscribe once (a scoped service or the middleware pipeline is the natural
/// place) and forward to their own logger.</para>
///
/// <para>The request is <b>not</b> aborted — the setting falls back to its compile-time
/// defaults so a single bad blob cannot take a site down.</para>
/// </remarks>
/// <param name="TenantId">Identifier of the tenant whose blob failed to hydrate.</param>
/// <param name="SettingKey">The <see cref="ISetting.Key"/> the blob was stored under.</param>
/// <param name="Exception">The failure. Typically a <see cref="System.Text.Json.JsonException"/>,
/// whose <c>Path</c> / <c>LineNumber</c> pinpoint the offending token.</param>
public sealed record SettingHydrationFailure(Guid TenantId, string SettingKey, Exception Exception);
