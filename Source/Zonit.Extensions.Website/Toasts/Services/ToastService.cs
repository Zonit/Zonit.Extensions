using System.Collections.Immutable;
using System.Globalization;
using Zonit.Extensions.Website.Toasts.Models;
using Zonit.Extensions.Website.Toasts.Types;

namespace Zonit.Extensions.Website.Toasts.Services;

/// <summary>
/// Scoped, in-memory toast queue. Maintains an immutable snapshot of entries
/// so reads from the UI thread are allocation-free in the steady state and
/// concurrent writes do not tear.
/// </summary>
/// <remarks>
/// Lifetime is <b>scoped</b> so all components within the same Blazor circuit
/// (or the same HTTP request, for SSR) share the queue. Previously the service
/// was transient and the <c>Add</c> method had an empty body — every
/// <c>Toast.AddError(...)</c> call was a no-op.
/// </remarks>
public sealed class ToastService : IToastProvider
{
    private ImmutableList<ToastEntry> _toasts = ImmutableList<ToastEntry>.Empty;

    /// <inheritdoc />
    public IReadOnlyList<ToastEntry> Toasts => _toasts;

    /// <inheritdoc />
    public event Action? OnChange;

    /// <inheritdoc />
    public void Add(ToastType taskType, string message, params object[]? args)
    {
        ArgumentNullException.ThrowIfNull(message);

        var finalMessage = FormatMessage(message, args);

        var entry = new ToastEntry(
            Id: Guid.NewGuid(),
            Type: taskType,
            Message: finalMessage,
            CreatedAt: DateTime.UtcNow);

        ImmutableInterlocked.Update(ref _toasts, static (list, item) => list.Add(item), entry);
        OnChange?.Invoke();
    }

    /// <inheritdoc />
    public void Remove(Guid id)
    {
        // ImmutableInterlocked.Update returns true iff the transformer produced a
        // different reference than the current value — exactly the "queue mutated"
        // signal we want for OnChange.
        var changed = ImmutableInterlocked.Update(ref _toasts, static (list, id) =>
        {
            var idx = list.FindIndex(e => e.Id == id);
            return idx >= 0 ? list.RemoveAt(idx) : list;
        }, id);

        if (changed)
            OnChange?.Invoke();
    }

    /// <inheritdoc />
    public void Clear()
    {
        var prev = Interlocked.Exchange(ref _toasts, ImmutableList<ToastEntry>.Empty);
        if (prev.Count > 0)
            OnChange?.Invoke();
    }

    private static string FormatMessage(string template, object[]? args)
    {
        if (args is null || args.Length == 0)
            return template;

        try
        {
            return string.Format(CultureInfo.CurrentCulture, template, args);
        }
        catch (FormatException)
        {
            // Bad template ({0} with no matching arg, mismatched braces, …).
            // Fail open: render the raw template rather than throwing inside
            // a UI notification path.
            return template;
        }
    }
}

