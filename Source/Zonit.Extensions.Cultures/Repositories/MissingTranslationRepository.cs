namespace Zonit.Extensions.Cultures.Repositories;

/// <summary>
/// Records translation requests that did not find a matching <see cref="Models.Variable"/> —
/// useful for surfacing untranslated keys during development. DI singleton; thread-safe.
/// </summary>
/// <remarks>
/// <para><b>Bounded on purpose.</b> Keys here come from arbitrary <c>Translate(content, …)</c>
/// arguments, so anything dynamic routed through the translation pipeline — a user-supplied
/// product name, an exception message, a per-row label — would otherwise become a permanent
/// entry in a process-wide singleton. The store therefore stops accepting new keys at
/// <see cref="BaseRepository.Capacity"/> (<see cref="DefaultCapacity"/> unless configured via
/// <c>CultureOption.MaxTrackedMissingTranslations</c>); <see cref="BaseRepository.IsFull"/>
/// tells a report whether it is looking at a truncated sample. Recording is additionally
/// opt-in — see <c>CultureOption.TrackMissingTranslations</c>.</para>
///
/// <para>Stored only in memory; no persistence. Consumers wanting durable telemetry should add a
/// custom <c>IHostedService</c> that flushes <see cref="BaseRepository.GetAll"/> periodically —
/// and call <see cref="BaseRepository.Clear"/> after each flush, otherwise the ceiling is hit
/// once and every later miss is dropped.</para>
/// </remarks>
public sealed class MissingTranslationRepository : BaseRepository
{
    /// <summary>
    /// Ceiling applied when the host does not configure one: enough distinct keys to be a
    /// useful "what is not translated yet" sample for a real application, small enough that a
    /// full buffer is a rounding error in memory terms.
    /// </summary>
    public const int DefaultCapacity = 1000;

    /// <summary>Creates the repository with <see cref="DefaultCapacity"/>.</summary>
    public MissingTranslationRepository() : base(DefaultCapacity) { }

    /// <summary>Creates the repository with an explicit ceiling.</summary>
    /// <param name="capacity">Maximum number of distinct missing keys to retain. Must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="capacity"/> is zero or negative.
    /// </exception>
    public MissingTranslationRepository(int capacity) : base(capacity) { }
}
