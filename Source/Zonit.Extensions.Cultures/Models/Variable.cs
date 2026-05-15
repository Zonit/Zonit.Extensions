using System.Collections.Immutable;

namespace Zonit.Extensions.Cultures.Models;

/// <summary>
/// One translation key (<see cref="Name"/>) plus the per-culture renditions
/// (<see cref="Translates"/>). Lives in singleton repositories shared across the
/// whole process — therefore must be thread-safe under concurrent reads from request
/// threads and concurrent writes from <c>MissingTranslationRepository.RecordMissing</c>
/// and the application's bootstrap loaders.
/// </summary>
/// <remarks>
/// <para><b>Thread safety.</b> The field <see cref="_translates"/> is an
/// <see cref="ImmutableArray{T}"/>. Mutations replace the whole array using
/// <see cref="System.Threading.Interlocked.CompareExchange{T}(ref T, T, T)"/> in a retry
/// loop, so concurrent <c>AddTranslate</c> / <c>RemoveTranslate</c> never lose updates
/// and concurrent <c>GetTranslate</c> reads see a consistent snapshot. Replaces the
/// previous <see cref="List{T}"/>-based implementation that crashed under load with
/// "Collection was modified during enumeration" — exactly the symptom the user
/// reported as "wywala extensions when starting too fast".</para>
///
/// <para><b>API stability.</b> Public surface unchanged — same constructors,
/// <see cref="Translates"/> type widened from nullable <c>List&lt;Translate&gt;?</c> to
/// <see cref="IReadOnlyList{T}"/> so existing call sites keep working.</para>
/// </remarks>
public sealed class Variable
{
    private ImmutableArray<Translate> _translates = ImmutableArray<Translate>.Empty;

    public string Name { get; }
    public string? Description { get; }

    /// <summary>
    /// Snapshot of the translations registered for this key. Always non-null. The
    /// returned reference is safe to enumerate even while a writer is mutating the
    /// variable — <see cref="ImmutableArray{T}"/> is by definition not modified in place.
    /// </summary>
    public IReadOnlyList<Translate> Translates => _translates;

    public Variable(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
    }

    public Variable(string name, IEnumerable<Translate> translates) : this(name)
    {
        ArgumentNullException.ThrowIfNull(translates);
        _translates = [.. translates];
    }

    public Variable(string name, IEnumerable<Translate> translates, string description)
        : this(name, translates)
    {
        Description = description;
    }

    /// <summary>
    /// Appends a translation. If a translation for the same culture already exists,
    /// it is replaced (last write wins) — matches the semantics callers expected from
    /// the legacy <c>List.Add</c> implementation, but makes the replacement explicit.
    /// </summary>
    public void AddTranslate(Translate translate)
    {
        ArgumentNullException.ThrowIfNull(translate);

        // CAS retry loop: on contention rebuild from the latest snapshot.
        while (true)
        {
            var current = _translates;
            var next = ReplaceOrAppend(current, translate);
            if (ImmutableInterlocked.InterlockedCompareExchange(ref _translates, next, current) == current)
                return;
        }
    }

    /// <summary>Removes the entry for <paramref name="culture"/>, returning whether anything was actually removed.</summary>
    public bool RemoveTranslate(string culture)
    {
        if (string.IsNullOrEmpty(culture))
            return false;

        while (true)
        {
            var current = _translates;
            var idx = IndexOfCulture(current, culture);
            if (idx < 0)
                return false;

            var next = current.RemoveAt(idx);
            if (ImmutableInterlocked.InterlockedCompareExchange(ref _translates, next, current) == current)
                return true;
        }
    }

    /// <summary>Returns the translation for <paramref name="culture"/>, or <see langword="null"/> when missing.</summary>
    public Translate? GetTranslate(string culture)
    {
        if (string.IsNullOrEmpty(culture))
            return null;

        var snapshot = _translates;
        var idx = IndexOfCulture(snapshot, culture);
        return idx < 0 ? null : snapshot[idx];
    }

    private static ImmutableArray<Translate> ReplaceOrAppend(ImmutableArray<Translate> current, Translate translate)
    {
        var idx = IndexOfCulture(current, translate.Culture);
        return idx < 0 ? current.Add(translate) : current.SetItem(idx, translate);
    }

    private static int IndexOfCulture(ImmutableArray<Translate> items, string culture)
    {
        for (int i = 0; i < items.Length; i++)
        {
            if (string.Equals(items[i].Culture, culture, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }
}