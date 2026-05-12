namespace Zonit.Extensions.Cultures.Repositories;

/// <summary>
/// Records translation requests that did not find a matching <see cref="Models.Variable"/> —
/// useful for surfacing untranslated keys during development. DI singleton; thread-safe.
/// </summary>
/// <remarks>
/// Stored only in memory; no persistence. Consumers wanting durable telemetry should add a
/// custom <c>IHostedService</c> that flushes <see cref="BaseRepository.GetAll"/> periodically.
/// </remarks>
public sealed class MissingTranslationRepository : BaseRepository;
