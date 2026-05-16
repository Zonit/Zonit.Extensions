using Zonit.Extensions.Website.Toasts.Models;
using Zonit.Extensions.Website.Toasts.Types;

namespace Zonit.Extensions.Website;

/// <summary>
/// Queue-style toast notification API. Implementations are scoped per Blazor
/// circuit / per HTTP request so a single host component can render every
/// toast raised anywhere in the page.
/// </summary>
public interface IToastProvider
{
    /// <summary>
    /// Snapshot of currently queued toasts. The returned list is a read-only
    /// view backed by a thread-safe store; callers must not mutate it.
    /// </summary>
    IReadOnlyList<ToastEntry> Toasts { get; }

    /// <summary>
    /// Raised after every <see cref="Add"/> / <see cref="Remove"/> /
    /// <see cref="Clear"/> mutation. Host components should subscribe and call
    /// <c>StateHasChanged()</c> on their renderer.
    /// </summary>
    event Action? OnChange;

    /// <summary>
    /// Queues a toast. <paramref name="args"/> are applied to
    /// <paramref name="message"/> via <see cref="string.Format(IFormatProvider, string, object[])"/>
    /// using <see cref="System.Globalization.CultureInfo.CurrentCulture"/> when
    /// non-empty; otherwise <paramref name="message"/> is forwarded as-is.
    /// </summary>
    void Add(ToastType taskType, string message, params object[]? args);

    /// <summary>Removes a single toast by id. No-op when the id is unknown.</summary>
    void Remove(Guid id);

    /// <summary>Empties the queue.</summary>
    void Clear();

    public void AddNormal(string message, params object[]? args)
        => Add(ToastType.Normal, message, args);

    public void AddInfo(string message, params object[]? args)
        => Add(ToastType.Info, message, args);

    public void AddSuccess(string message, params object[]? args)
        => Add(ToastType.Success, message, args);

    public void AddWarning(string message, params object[]? args)
        => Add(ToastType.Warning, message, args);

    public void AddError(string message, params object[]? args)
        => Add(ToastType.Error, message, args);
}