using Zonit.Extensions.Website.Toasts.Types;

namespace Zonit.Extensions.Website.Toasts.Models;

/// <summary>
/// A queued toast notification. Created internally by <c>ToastService</c>;
/// consumers receive them through <see cref="IToastProvider.Toasts"/>.
/// </summary>
/// <param name="Id">Stable identifier; pass to <see cref="IToastProvider.Remove"/> to dismiss.</param>
/// <param name="Type">Visual category (info / success / warning / error / normal).</param>
/// <param name="Message">Final, already-formatted message ready for rendering.</param>
/// <param name="CreatedAt">UTC timestamp of when the toast was queued.</param>
public sealed record ToastEntry(
    Guid Id,
    ToastType Type,
    string Message,
    DateTime CreatedAt);
