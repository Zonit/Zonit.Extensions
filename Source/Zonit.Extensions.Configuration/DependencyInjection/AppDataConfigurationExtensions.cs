using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Zonit.Extensions.Configuration;

namespace Zonit.Extensions;

/// <summary>
/// Folds the contents of <c>AppData/Settings</c> into the host's configuration — one file per
/// concern, instead of one <c>appsettings.json</c> in which the Serilog section buries everything
/// else.
/// </summary>
/// <remarks>
/// <code>
/// AppData/Settings/
///   database.json            connection string
///   kestrel.json             endpoints and certificate
///   cultures.json            default culture and the supported list
///   serilog.json             sinks and filters
///   tenants.json             site identity: name, description, brand colours
///   kestrel.dev.json         Development-only overrides (reserved ".dev.json" suffix)
///   database.local.json      machine-local secrets, gitignored
///   Staging/                 per-environment overrides (folder name = ASPNETCORE_ENVIRONMENT)
///     kestrel.json
/// </code>
///
/// <para><b>Two ways to vary by environment, on purpose.</b> <c>.dev.json</c> is a reserved
/// suffix meaning "Development only" — short, and unambiguous precisely because it is a fixed
/// word rather than a wildcard. A general <c>file.{Environment}.json</c> convention could not be
/// read reliably: <c>market-data.api.json</c> and <c>market-data.Staging.json</c> have the same
/// shape, so the loader would have to guess whether the middle segment names an environment or
/// part of the topic. Every other environment therefore uses a folder, where no guessing is
/// possible. If you need a literal "dev" in a topic name, write <c>foo-dev.json</c>.</para>
///
/// <para><b>Secrets: <c>.local.json</c>.</b> Loaded last within its directory and meant to sit in
/// <c>.gitignore</c>, so a key goes in <c>database.local.json</c> next to <c>database.json</c> and
/// never reaches the repository. Exclude the pattern from <c>CopyToOutputDirectory</c> as well —
/// <c>.gitignore</c> keeps a secret out of source control, not out of a container image.
/// Production secrets still belong in environment variables or a vault, both of which win over
/// these files.</para>
/// </remarks>
public static class AppDataConfigurationExtensions
{
    /// <summary>Reserved suffix: loaded only when the environment is Development.</summary>
    private const string DevelopmentSuffix = ".dev.json";

    /// <summary>Reserved suffix: loaded last within its directory, expected to be gitignored.</summary>
    private const string LocalSuffix = ".local.json";

    /// <summary>
    /// Idempotency marker, kept in the service collection rather than in
    /// <see cref="IHostApplicationBuilder.Properties"/> so that both entry points share it — a
    /// host may call <c>builder.AddAppData(…)</c> to override options and then
    /// <c>services.AddWebsite()</c>, and the second call has to see the first.
    /// </summary>
    private sealed class AppDataMarker;

    /// <summary>
    /// Adds every JSON file under the settings directory to <paramref name="builder"/>'s
    /// configuration.
    /// </summary>
    /// <remarks>
    /// <para><b>Call before <c>Build()</c>.</b> Kestrel and the logging providers read
    /// configuration while the host is being built, so the first line after
    /// <c>CreateBuilder(args)</c> is the right place. A missing settings directory is a valid
    /// state — the host simply runs on whatever else it has.</para>
    ///
    /// <para><b>Precedence</b>, later winning:
    /// <c>appsettings.json</c> &lt; <c>appsettings.{Environment}.json</c> &lt;
    /// <c>AppData/Settings/*</c> &lt; <c>AppData/Settings/{Environment}/*</c> &lt; user secrets
    /// &lt; environment variables &lt; command line. The files are inserted directly after the
    /// last <c>appsettings*.json</c> source rather than appended: appending would put them above
    /// the environment variables, which is how a container and a CI pipeline configure the app.
    /// The anchor still works when the host has no appsettings files at all — the sources are
    /// registered as optional regardless — and falls back to the front of the list if it finds
    /// none, leaving everything else free to override.</para>
    ///
    /// <para><b>Idempotent.</b> Repeat calls are ignored, so a host may call this explicitly even
    /// when something else already did. The first call's options are the ones that apply.</para>
    /// </remarks>
    /// <param name="builder">Host builder — web, worker or console.</param>
    /// <param name="configure">Optional overrides; see <see cref="AppDataOptions"/>.</param>
    /// <exception cref="ArgumentException">
    /// <see cref="AppDataOptions.SettingsPath"/> resolves outside the content root, which the
    /// content-root file provider cannot read. Failing here beats loading nothing quietly.
    /// </exception>
    public static TBuilder AddAppData<TBuilder>(this TBuilder builder, Action<AppDataOptions>? configure = null)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Guard before any work: AddWebsite() also calls this, and a host that calls it directly
        // must not get every file inserted twice. Duplicate sources resolve to the same values,
        // but they double the file watchers and turn the source list into noise for anyone
        // debugging why a key has the value it has.
        if (!TryMarkApplied(builder.Services))
            return builder;

        var options = new AppDataOptions();
        configure?.Invoke(options);

        Apply(builder.Configuration, builder.Environment, options);
        return builder;
    }

    /// <summary>
    /// Adds the settings files from a service collection, for callers that never see the host
    /// builder — <c>services.AddWebsite()</c> being the one that matters.
    /// </summary>
    /// <remarks>
    /// <para><b>How a service collection reaches configuration.</b> The host registers its
    /// <c>ConfigurationManager</c> as the <see cref="IConfiguration"/> service and registers
    /// <see cref="IHostEnvironment"/> as a plain instance, and <c>ConfigurationManager</c>
    /// implements <see cref="IConfigurationBuilder"/> as well as <see cref="IConfiguration"/>. So
    /// both halves of what this needs are already in the collection, and adding a source to the
    /// resolved instance is immediately visible through <c>builder.Configuration</c> — the object
    /// is the same one.</para>
    ///
    /// <para><b>Still call it before <c>Build()</c>.</b> Reaching the manager is not the same as
    /// beating the readers: Kestrel and the logging providers read configuration during host
    /// construction, so a source added afterwards is simply late.</para>
    ///
    /// <para>The two service descriptors are the one assumption here. If a host registers them
    /// differently this throws with instructions rather than silently loading nothing — a missing
    /// settings file is not a failure mode anyone would notice until production behaves oddly.</para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The host's configuration or environment could not be reached from the service collection.
    /// </exception>
    public static IServiceCollection AddAppData(
        this IServiceCollection services,
        Action<AppDataOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (!TryMarkApplied(services))
            return services;

        var environment = ResolveRegistered<IHostEnvironment>(services)
            ?? throw new InvalidOperationException(
                "AddAppData could not resolve IHostEnvironment from the service collection. Call " +
                "builder.AddAppData(…) on the host builder instead, or turn the loader off where " +
                "this was invoked from.");

        if (ResolveRegistered<IConfiguration>(services) is not IConfigurationBuilder configuration)
        {
            throw new InvalidOperationException(
                "AddAppData reached the host's IConfiguration but it is not an IConfigurationBuilder, " +
                "so no source can be added to it. Call builder.AddAppData(…) on the host builder " +
                "instead, or turn the loader off where this was invoked from.");
        }

        var options = new AppDataOptions();
        configure?.Invoke(options);

        Apply(configuration, environment, options);
        return services;
    }

    /// <summary>
    /// Adds a marker service on first call and reports whether the caller should proceed.
    /// </summary>
    private static bool TryMarkApplied(IServiceCollection services)
    {
        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType == typeof(AppDataMarker))
                return false;
        }

        services.Add(ServiceDescriptor.Singleton(new AppDataMarker()));
        return true;
    }

    /// <summary>
    /// Pulls an already-constructed service out of the collection without building a provider.
    /// </summary>
    /// <remarks>
    /// Hosts register these two either as an instance or through a factory that ignores its
    /// <see cref="IServiceProvider"/> and closes over the object the builder already holds.
    /// Instance first; the factory is invoked with a null provider only as a fallback, and a
    /// factory that does touch the provider fails with <see cref="NullReferenceException"/>,
    /// which is caught here so the caller gets the explicit message above instead.
    /// </remarks>
    private static T? ResolveRegistered<T>(IServiceCollection services) where T : class
    {
        for (var i = services.Count - 1; i >= 0; i--)
        {
            var descriptor = services[i];

            if (descriptor.ServiceType != typeof(T))
                continue;

            if (descriptor.ImplementationInstance is T instance)
                return instance;

            if (descriptor.ImplementationFactory is { } factory)
            {
                try
                {
                    if (factory(null!) is T produced)
                        return produced;
                }
                catch (NullReferenceException)
                {
                    // Factory needs a real provider — nothing to salvage, keep looking.
                }
            }
        }

        return null;
    }

    /// <summary>
    /// The shared body: resolve the settings directory, collect files in load order and splice the
    /// sources in at the right precedence.
    /// </summary>
    private static void Apply(
        IConfigurationBuilder configuration,
        IHostEnvironment environment,
        AppDataOptions options)
    {
        var contentRoot = environment.ContentRootPath;
        var root = Path.GetFullPath(Path.Combine(contentRoot, options.SettingsPath));
        var relativeRoot = Path.GetRelativePath(contentRoot, root);

        if (relativeRoot.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relativeRoot))
        {
            throw new ArgumentException(
                $"SettingsPath '{options.SettingsPath}' resolves to '{root}', which is outside the " +
                $"content root '{contentRoot}'. Files are read through ContentRootFileProvider and it " +
                "cannot serve paths above its own root.",
                nameof(options));
        }

        if (!Directory.Exists(root))
        {
            TryScaffold(root, environment, options);

            // Still absent — either scaffolding is off, we are not in Development, or the file
            // system refused. Nothing to add; the host runs on its remaining sources.
            if (!Directory.Exists(root))
                return;
        }

        var files = new List<string>();
        files.AddRange(SettingsFilesIn(root, environment));
        files.AddRange(SettingsFilesIn(Path.Combine(root, environment.EnvironmentName), environment));

        if (files.Count == 0)
            return;

        var sources = configuration.Sources;
        var insertAt = LastAppSettingsIndex(sources) + 1;

        foreach (var file in files)
        {
            sources.Insert(insertAt++, new JsonConfigurationSource
            {
                Path = Path.GetRelativePath(contentRoot, file),
                Optional = true,
                ReloadOnChange = options.ReloadOnChange,
                FileProvider = environment.ContentRootFileProvider,
            });
        }
    }

    /// <summary>
    /// The <c>*.json</c> files of one directory, in load order: plain topic files first
    /// (alphabetically), then <c>.dev.json</c> when the environment is Development, then
    /// <c>.local.json</c>. Not recursive — a subdirectory is an environment and gets its own pass.
    /// </summary>
    /// <remarks>
    /// Ordering is the whole point: a secret has to override its base file, never the reverse,
    /// and a Development tweak has to override the shared value it is tweaking. Alphabetical
    /// ordering inside each group only decides between peers, which is why files should be scoped
    /// to one topic — two files claiming the same section makes the winner depend on their names.
    /// </remarks>
    private static List<string> SettingsFilesIn(string directory, IHostEnvironment environment)
    {
        if (!Directory.Exists(directory))
            return [];

        var all = Directory
            .EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(static x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var ordered = new List<string>(all.Count);
        ordered.AddRange(all.Where(static x => !HasSuffix(x, DevelopmentSuffix) && !HasSuffix(x, LocalSuffix)));

        if (environment.IsDevelopment())
            ordered.AddRange(all.Where(static x => HasSuffix(x, DevelopmentSuffix)));

        // Local overrides apply in every environment: the file is about this machine, not about
        // this stage. It stays last so it beats both the base file and the Development tweak.
        ordered.AddRange(all.Where(static x => HasSuffix(x, LocalSuffix)));

        return ordered;
    }

    private static bool HasSuffix(string path, string suffix)
        => Path.GetFileName(path).EndsWith(suffix, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Index of the last <c>appsettings*.json</c> source, or <c>-1</c> when the host registered
    /// none — in which case our files go to the front and everything else overrides them.
    /// </summary>
    private static int LastAppSettingsIndex(IList<IConfigurationSource> sources)
    {
        for (var i = sources.Count - 1; i >= 0; i--)
        {
            if (sources[i] is JsonConfigurationSource { Path: { } path } &&
                Path.GetFileName(path).StartsWith("appsettings", StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Creates the settings directory in Development, best-effort. See
    /// <see cref="AppDataOptions.CreateIfMissing"/> for why this is neither unconditional nor
    /// allowed to throw.
    /// </summary>
    private static void TryScaffold(string root, IHostEnvironment environment, AppDataOptions options)
    {
        if (!options.CreateIfMissing || !environment.IsDevelopment())
            return;

        try
        {
            Directory.CreateDirectory(root);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A convenience directory is not worth failing a start over, and at this point in the
            // pipeline there is no logger to report it to anyway.
        }
    }
}
