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

    public event Action? OnChange;

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
        if (!HasOverride && Key is null && !IsNoLayout)
            return;

        HasOverride = false;
        IsNoLayout = false;
        Key = null;
        OnChange?.Invoke();
    }
}
