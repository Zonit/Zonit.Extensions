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

    private readonly int _capacity;

    /// <summary>Creates an unbounded repository — the shape a real translation registry needs.</summary>
    protected BaseRepository() : this(int.MaxValue) { }

    /// <summary>
    /// Creates a repository that stops accepting NEW keys once <paramref name="capacity"/>
    /// distinct names are stored.
    /// </summary>
    /// <param name="capacity">Maximum number of distinct <see cref="Variable.Name"/>s. Must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="capacity"/> is zero or negative.
    /// </exception>
    /// <remarks>
    /// Exists for <see cref="MissingTranslationRepository"/>, which is written from request
    /// threads with keys derived from arbitrary <c>Translate(content, …)</c> input. Without a
    /// ceiling any dynamic string routed through the translation pipeline (a user-supplied
    /// name, an exception message, a per-row label) becomes a permanent allocation in a
    /// process-wide singleton — a data-driven memory leak.
    /// </remarks>
    protected BaseRepository(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _capacity = capacity;
    }

    /// <summary>
    /// Maximum number of distinct keys this repository will store.
    /// <see cref="int.MaxValue"/> means effectively unbounded.
    /// </summary>
    public int Capacity => _capacity;

    /// <summary>
    /// <see langword="true"/> once <see cref="Capacity"/> is reached and new keys are being
    /// dropped. Updates to keys already present continue to be applied.
    /// </summary>
    public bool IsFull => _items.Count >= _capacity;

    /// <summary>
    /// Adds (or overwrites by name) a single variable. When the repository is
    /// <see cref="IsFull"/>, a variable whose name is not already present is dropped silently.
    /// </summary>
    public void Add(Variable item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!HasRoomFor(item.Name))
            return;
        _items[item.Name] = item;
    }

    /// <summary>Adds a batch of variables. Last write wins per <see cref="Variable.Name"/>.</summary>
    public void AddRange(IEnumerable<Variable> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        foreach (var item in items)
            Add(item);
    }

    /// <summary>
    /// Capacity gate. Deliberately a check-then-act pair rather than a lock: the only bounded
    /// repository is a diagnostic buffer, so a handful of extra entries under concurrent
    /// first-misses is irrelevant, whereas serialising every write would sit on the hottest
    /// path in the rendering stack. The ceiling is therefore a soft one — N concurrent writers
    /// can overshoot it by at most N - 1 entries — and it exists to bound memory, not to be exact.
    /// The unbounded default short-circuits before touching <c>Count</c>, which on
    /// <see cref="ConcurrentDictionary{TKey,TValue}"/> sums the per-lock counters rather than
    /// reading a single field, so it is not free on the translation hot path.
    /// </summary>
    private bool HasRoomFor(string name)
        => _capacity == int.MaxValue || _items.Count < _capacity || _items.ContainsKey(name);

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