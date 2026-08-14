namespace Zonit.Extensions.Website.Layouts.Services;

/// <summary>
/// Default <see cref="ILayoutContext"/>. Per-circuit state holder; emits
/// <see cref="OnChange"/> on every effective state transition so
/// <c>ZonitRouteView</c> can re-render with the new layout choice.
/// </summary>
internal sealed class LayoutContext : ILayoutContext
{
    public bool HasOverride { get; private set; }
    public string? Key { get; private set; }
    public bool IsNoLayout { get; private set; }

    public PageWidth Width { get; private set; } = PageWidth.Content;

    public event Action? OnChange;

    public void SetWidth(PageWidth width)
    {
        if (Width == width)
            return;

        Width = width;
        OnChange?.Invoke();
    }

    public void SetKey(string? key)
    {
        // null  → NoLayout dynamic override (raw render, like [NoLayout])
        // ""    → Site default (used to "undo" a [LayoutKey] static attribute at runtime)
        // other → string-keyed layout, resolved via ILayoutRegistry

        var newHas = true;
        var newIsNoLayout = key is null;
        var newKey = key;

        if (HasOverride == newHas && IsNoLayout == newIsNoLayout && Key == newKey)
            return;

        HasOverride = newHas;
        IsNoLayout = newIsNoLayout;
        Key = newKey;
        OnChange?.Invoke();
    }

    public void ClearOverride()
    {
        // Width resets with the rest: it belongs to the page being left, and a stale value would
        // silently widen the next one. The route view sets it again from the incoming page's
        // attribute before anything renders.
        if (!HasOverride && Key is null && !IsNoLayout && Width == PageWidth.Content)
            return;

        HasOverride = false;
        IsNoLayout = false;
        Key = null;
        Width = PageWidth.Content;
        OnChange?.Invoke();
    }
}
