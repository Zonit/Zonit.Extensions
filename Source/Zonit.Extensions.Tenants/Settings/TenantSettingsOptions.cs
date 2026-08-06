using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Zonit.Extensions.Tenants.Settings;

/// <summary>
/// The JSON contract every <see cref="Setting{T}"/> is hydrated through, and the one place a host
/// registers the source-generated metadata that makes hydration AOT-safe.
/// </summary>
/// <remarks>
/// <para><b>Why this exists.</b> Until 10.0.0-preview.11 every setting carried its own
/// <see cref="JsonSerializerContext"/> plus a <c>Hydrate</c> override — roughly twenty lines of
/// ceremony per setting, repeated verbatim. The generator cannot remove it: Roslyn generators do
/// not observe each other's output, so a <c>[JsonSerializable]</c> context emitted by the Tenants
/// generator is invisible to the System.Text.Json generator and fails to compile
/// (<c>CS0534</c>). What <i>can</i> be removed is the per-setting repetition — one registration
/// per application, and <see cref="Setting{T}.Hydrate"/> becomes something you override only when
/// a setting genuinely needs different JSON rules.</para>
///
/// <para><b>The naming policy is part of the contract, and it does not travel.</b> A
/// source-generated context declares its own options through
/// <c>[JsonSourceGenerationOptions]</c>, but those options are <b>not</b> honoured when the
/// context is reached through <c>JsonTypeInfoResolver.Combine</c> — the policy on the
/// options object doing the asking wins. <see cref="Build"/> therefore pins camelCase and
/// <c>WhenWritingNull</c> explicitly, matching what <see cref="TenantsJsonContext"/> has always
/// written, so existing persisted blobs keep round-tripping. A setting whose context needs
/// genuinely different rules (a custom converter, string enums) must override
/// <see cref="Setting{T}.Hydrate"/> and reach for its own <c>JsonTypeInfo</c> directly.</para>
///
/// <para><b>Reading is case-insensitive.</b> The casing of a persisted blob no longer matters:
/// <c>{"title":…}</c>, <c>{"Title":…}</c> and <c>{"TITLE":…}</c> all bind. Writing stays
/// camelCase. This is deliberate insurance rather than convenience — under the default
/// case-sensitive matching, a blob produced by a plain <c>JsonSerializer.Serialize(model)</c>
/// matched nothing and System.Text.Json signalled that as an empty object rather than as an
/// error, so the override vanished into compile-time defaults with no exception, no log entry
/// and no <see cref="SettingHydrationFailure"/> event to notice it by.</para>
/// </remarks>
public sealed class TenantSettingsOptions
{
    private readonly List<IJsonTypeInfoResolver> _resolvers = [];

    /// <summary>
    /// Configuration section holding per-setting defaults, keyed by <see cref="ISetting.Key"/>.
    /// Defaults to <c>"Tenants"</c>.
    /// </summary>
    /// <remarks>
    /// <para>A setting with no persisted override reads its values from here, so a single-site
    /// app can configure the whole thing in <c>appsettings.json</c> and never write a line of
    /// persistence code:</para>
    /// <code>
    /// {
    ///   "Tenants": {
    ///     "site":  { "Title": "Acme", "Language": "en-US" },
    ///     "theme": { "PrimaryColor": "#0F766E", "Roundness": "Large" }
    ///   }
    /// }
    /// </code>
    /// <para>Ordinary <see cref="IConfiguration"/>, so every provider applies — environment
    /// variables (<c>Tenants__site__Title</c>), user secrets, Key Vault. Casing is free-form:
    /// section keys are matched case-insensitively, and enums accept either the member name
    /// (<c>"Large"</c>) or its number (<c>3</c>).</para>
    ///
    /// <para><b>Precedence</b> is persisted blob → configuration → compile-time default, per
    /// setting key. Configuration therefore acts as the house default for <i>every</i> tenant,
    /// and a tenant that stores its own value overrides it.</para>
    /// </remarks>
    public string ConfigurationSection { get; set; } = "Tenants";

    /// <summary>
    /// Whether a configuration reload invalidates hydrated settings and raises
    /// <see cref="ITenantProvider.OnChange"/>. Default <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// With the host's usual <c>reloadOnChange: true</c> JSON provider this means editing
    /// <c>appsettings.json</c> re-renders open Blazor circuits, because components already
    /// subscribe to <see cref="ITenantProvider.OnChange"/> for tenant switches. Turn it off to
    /// pin settings to the values read at startup.
    /// </remarks>
    public bool ReloadOnChange { get; set; } = true;

    /// <summary>
    /// Registers a source-generated <see cref="JsonSerializerContext"/> whose
    /// <c>[JsonSerializable]</c> entries cover the models of one or more
    /// <see cref="Setting{T}"/> types.
    /// </summary>
    /// <remarks>
    /// <para>Call it with the context an application already has:</para>
    /// <code>
    /// builder.Services.AddTenantsExtension(o => o.AddJsonContext(AppJsonContext.Default));
    /// </code>
    /// <para>Adding <c>[JsonSerializable(typeof(MyModel))]</c> to that context is the whole
    /// per-setting cost. Registering nothing is also valid — see
    /// <see cref="Setting{T}.Hydrate"/> for what happens then.</para>
    /// </remarks>
    /// <param name="context">The context to consult when resolving a setting model.</param>
    public TenantSettingsOptions AddJsonContext(JsonSerializerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _resolvers.Add(context);
        return this;
    }

    /// <summary>
    /// Registers any <see cref="IJsonTypeInfoResolver"/>, for hosts that compose metadata
    /// themselves rather than through a single generated context.
    /// </summary>
    public TenantSettingsOptions AddJsonResolver(IJsonTypeInfoResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        _resolvers.Add(resolver);
        return this;
    }

    /// <summary>
    /// Materialises the shared <see cref="JsonSerializerOptions"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="TenantsJsonContext"/> is appended last so a host can override the metadata for
    /// a built-in model by registering its own context for it, while a host that registers
    /// nothing still resolves <c>site</c> / <c>theme</c> / <c>maintenance</c> /
    /// <c>social_media</c> without reflection. The result is frozen: an options instance shared
    /// by every scope must not be mutable, and <see cref="JsonSerializerOptions.MakeReadOnly()"/>
    /// is also what lets System.Text.Json cache the resolved <c>JsonTypeInfo</c> per type instead
    /// of rebuilding it per lookup.
    /// </remarks>
    internal JsonSerializerOptions Build()
    {
        // Order is precedence, first match wins:
        //   1. what this host registered by hand — so a JsonSerializerContext supplied for a model
        //      overrides the generator's description of it,
        //   2. what the generator registered automatically through module initializers,
        //   3. the built-in settings' own context,
        //   4. the scalar types every model's properties are made of.
        IJsonTypeInfoResolver[] resolvers =
        [
            .. _resolvers,
            TenantSettingsMetadata.Live,
            TenantsJsonContext.Default,
            TenantPrimitiveJsonResolver.Default,
        ];

        var options = new JsonSerializerOptions
        {
            TypeInfoResolver = JsonTypeInfoResolver.Combine(resolvers),

            // Write side: camelCase, nulls omitted — the shape TenantsJsonContext has always
            // produced, so blobs written before this refactor still read back identically.
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

            // Read side: accept any casing. This is the single change that removes the package's
            // worst failure mode. Property matching is case-SENSITIVE by default, so a blob
            // written with plain JsonSerializer.Serialize(model) — PascalCase — matched no
            // property at all, and System.Text.Json reports "nothing matched" as success with an
            // empty object rather than as an error. The tenant's override then disappeared into
            // the compile-time defaults with no exception, no log line and no
            // OnSettingHydrationFailed event: a page quietly rendering "New website" was the only
            // symptom. Reading case-insensitively costs a case-folding lookup per property and
            // makes every reasonable way of writing the blob work.
            PropertyNameCaseInsensitive = true,
        };

        options.MakeReadOnly();
        return options;
    }
}
