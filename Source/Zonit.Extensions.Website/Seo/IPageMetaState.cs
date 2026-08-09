namespace Zonit.Extensions.Website;

/// <summary>
/// Per-scope channel carrying the routed page's <see cref="PageMeta"/> to the document head.
/// </summary>
/// <remarks>
/// <para><b>Why a channel and not a parameter.</b> The head is rendered above the page in the
/// component tree, so it cannot receive anything from the page directly. Blazor's answer is
/// <c>HeadContent</c> / <c>HeadOutlet</c>, and this is the state that feeds it: the page
/// publishes, <c>PageHead</c> subscribes and re-renders. That also covers the case the head
/// cannot see coming — a page that only learns its title after an <c>await</c>.</para>
///
/// <para>Scoped, so an HTTP request and an interactive circuit each get their own. Pages do not
/// touch this directly; <c>PageBase</c> publishes on their behalf.</para>
/// </remarks>
public interface IPageMetaState
{
    /// <summary>Metadata published by the current page, or <see langword="null"/> before one has.</summary>
    PageMeta? Current { get; }

    /// <summary>Publishes (or re-publishes) the current page's metadata and raises <see cref="OnChange"/>.</summary>
    void Set(PageMeta meta);

    /// <summary>
    /// Clears the published metadata. Called on navigation so a page that declares no title
    /// cannot inherit the previous one.
    /// </summary>
    void Clear();

    /// <summary>
    /// Raised whenever the published metadata is replaced, cleared, or explicitly re-notified
    /// through <see cref="Touch"/>.
    /// </summary>
    event Action? OnChange;

    /// <summary>
    /// Re-raises <see cref="OnChange"/> without changing the instance.
    /// </summary>
    /// <remarks>
    /// <see cref="PageMeta"/> is a mutable object, so a page assigning <c>Meta.Title</c> after
    /// the head has already rendered changes state that nothing observed. <c>PageBase</c> calls
    /// this after each render for exactly that case. Cheap by design: the head component compares
    /// the composed output and skips the re-render when nothing actually moved.
    /// </remarks>
    void Touch();
}

internal sealed class PageMetaState : IPageMetaState
{
    public PageMeta? Current { get; private set; }

    public event Action? OnChange;

    public void Set(PageMeta meta)
    {
        ArgumentNullException.ThrowIfNull(meta);

        if (ReferenceEquals(Current, meta))
        {
            OnChange?.Invoke();
            return;
        }

        Current = meta;
        OnChange?.Invoke();
    }

    public void Clear()
    {
        if (Current is null)
            return;

        Current = null;
        OnChange?.Invoke();
    }

    public void Touch() => OnChange?.Invoke();
}
