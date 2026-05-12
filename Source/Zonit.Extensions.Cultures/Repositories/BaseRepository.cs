using System.Collections.Concurrent;
using Zonit.Extensions.Cultures.Models;

namespace Zonit.Extensions.Cultures.Repositories;

/// <summary>
/// Thread-safe in-memory store of translation <see cref="Variable"/>s keyed by
/// <see cref="Variable.Name"/>. O(1) lookup via <see cref="TryGet"/>, O(1) insertion.
/// </summary>
/// <remarks>
/// Replaces the previous <see cref="List{T}"/>-backed implementation whose <c>GetAll()</c>
/// was scanned linearly by <c>CultureService.FindTranslation</c> on every translation call —
/// a quadratic hot path. The store is concurrent so that population from multiple modules /
/// startup tasks (and <c>MissingTranslationRepository</c> writes during request handling)
/// is safe without external locking.
/// </remarks>
public abstract class BaseRepository
{
    private readonly ConcurrentDictionary<string, Variable> _items =
        new(StringComparer.Ordinal);

    /// <summary>Adds (or overwrites by name) a single variable.</summary>
    public void Add(Variable item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _items[item.Name] = item;
    }

    /// <summary>Adds a batch of variables. Last write wins per <see cref="Variable.Name"/>.</summary>
    public void AddRange(IEnumerable<Variable> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        foreach (var item in items)
            _items[item.Name] = item;
    }

    /// <summary>O(1) lookup by exact <see cref="Variable.Name"/>.</summary>
    public bool TryGet(string name, out Variable variable)
    {
        if (_items.TryGetValue(name, out var found))
        {
            variable = found;
            return true;
        }
        variable = null!;
        return false;
    }

    /// <summary>Snapshot enumeration. Cost: enumerator over the concurrent dictionary's values.</summary>
    public IReadOnlyCollection<Variable> GetAll() => _items.Values.ToArray();

    /// <summary>Number of registered variables.</summary>
    public int Count => _items.Count;

    /// <summary>Removes all variables.</summary>
    public void Clear() => _items.Clear();
}