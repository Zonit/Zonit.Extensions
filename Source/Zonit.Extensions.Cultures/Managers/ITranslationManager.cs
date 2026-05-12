using Zonit.Extensions.Cultures.Models;

namespace Zonit.Extensions.Cultures;

/// <summary>
/// Add translation manually
/// </summary>
public interface ITranslationManager
{
    /// <summary>
    /// Add one translation
    /// </summary>
    /// <param name="item"></param>
    public void Add(Variable item);

    /// <summary>
    /// Add multiple translations
    /// </summary>
    /// <param name="items"></param>
    public void AddRange(List<Variable> items);
}