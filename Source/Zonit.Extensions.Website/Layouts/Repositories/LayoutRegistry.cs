using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Zonit.Extensions.Website.Layouts.Repositories;

/// <summary>
/// Default <see cref="ILayoutRegistry"/>. Concurrent dictionary keyed by
/// case-insensitive layout names; built up at DI configuration time and read
/// concurrently from many circuits afterwards.
/// </summary>
internal sealed class LayoutRegistry : ILayoutRegistry
{
    private readonly ConcurrentDictionary<string, Type> _map =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> Keys => (IReadOnlyCollection<string>)_map.Keys;

    public void Register(string key,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors
                                  | DynamicallyAccessedMemberTypes.PublicProperties)]
        Type layoutType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(layoutType);

        // Upsert — last writer wins. This is what lets a host overwrite a built-in
        // layout (e.g. "Zonit.Minimal") with its own implementation by registering
        // the same canonical key after the framework's default registration.
        _map[key] = layoutType;
    }

    public bool TryResolve(string key, [NotNullWhen(true)] out Type? layoutType)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            layoutType = null;
            return false;
        }

        return _map.TryGetValue(key, out layoutType);
    }
}
