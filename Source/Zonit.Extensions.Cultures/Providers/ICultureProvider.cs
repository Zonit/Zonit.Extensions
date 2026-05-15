using Zonit.Extensions.Cultures.Models;

namespace Zonit.Extensions.Cultures;

/// <summary>
/// Public read-only API for translation and culture-aware presentation. Designed to be
/// consumed by views / components / repositories that need to render localised content
/// without participating in culture mutation.
/// </summary>
public interface ICultureProvider
{
    /// <summary>Currently active culture (<see cref="Culture"/> value object).</summary>
    Culture Current { get; }

    /// <summary>Date / time format for the current culture.</summary>
    DateTimeFormatModel DateTimeFormat { get; }

    /// <summary>
    /// Looks up <paramref name="content"/> in the translation registry for the current culture
    /// (with default-culture fallback) and applies <see cref="string.Format(string, object[])"/>
    /// over <paramref name="args"/> if any. Returns the input <paramref name="content"/>
    /// unchanged when no entry is found.
    /// </summary>
    /// <returns>
    /// A <see cref="Translation"/> value object. Implicitly convertible to <see cref="string"/>
    /// for legacy call sites.
    /// </returns>
    Translation Translate(string content, params object?[] args);

    /// <summary>Converts a UTC <see cref="DateTime"/> to the current culture's time zone.</summary>
    DateTime ClientTimeZone(DateTime utcDateTime);

    /// <summary>Raised when the active culture or time-zone changes.</summary>
    event Action? OnChange;
}