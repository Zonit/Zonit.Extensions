namespace Zonit.Extensions.Configuration;

/// <summary>
/// Shapes what <c>AddAppData</c> loads and how. Every value has a working default; the delegate
/// exists for hosts that deviate, not for hosts that adopt.
/// </summary>
public sealed class AppDataOptions
{
    /// <summary>
    /// Settings directory, relative to the host's content root. Defaults to
    /// <c>AppData/Settings</c>.
    /// </summary>
    /// <remarks>
    /// Must stay inside the content root: the files are read through
    /// <see cref="Microsoft.Extensions.Hosting.IHostEnvironment.ContentRootFileProvider"/>, which
    /// refuses to serve anything above its own root. A path that escapes is rejected at startup
    /// with an explicit message rather than silently loading nothing.
    /// </remarks>
    public string SettingsPath { get; set; } = "AppData/Settings";

    /// <summary>
    /// Whether editing a settings file reloads configuration in the running process.
    /// Default <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// This is the switch that makes the rest of the stack's hot reload real — options bound
    /// through <c>IOptionsMonitor</c> only ever see a change if the underlying source reloads.
    /// Turn it off where file watching is unreliable or costly: network-mounted volumes, some
    /// container filesystems. In those environments <c>DOTNET_USE_POLLING_FILE_WATCHER=1</c> is
    /// usually the better answer than giving up reload entirely.
    /// </remarks>
    public bool ReloadOnChange { get; set; } = true;

    /// <summary>
    /// Whether a missing settings directory is created. Default <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// <para><b>Development only, and best-effort.</b> An empty directory changes nothing at
    /// runtime — its value is telling a developer where files go. That is not worth failing a
    /// production start over, and a hardened container with a read-only content root would do
    /// exactly that, before logging is configured, leaving a bare stack trace. So the directory
    /// is created only when the environment is Development, and any
    /// <see cref="System.IO.IOException"/> or
    /// <see cref="System.UnauthorizedAccessException"/> is swallowed.</para>
    /// </remarks>
    public bool CreateIfMissing { get; set; } = true;
}
