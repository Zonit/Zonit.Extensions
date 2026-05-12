namespace Zonit.Extensions.Cultures.Models;

public class Variable
{
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public List<Translate>? Translates { get; private set; }

    public Variable(string name)
        => Name = name;

    public Variable(string name, List<Translate> translates) : this(name)
        => Translates = translates;

    public Variable(string name, List<Translate> translates, string description) : this(name, translates)
        => Description = description;

    /// <summary>
    /// Adds a translation to the variable
    /// </summary>
    /// <param name="translate">The translation to add</param>
    public void AddTranslate(Translate translate)
    {
        Translates ??= new List<Translate>();
        Translates.Add(translate);
    }

    /// <summary>
    /// Removes a translation from the variable
    /// </summary>
    /// <param name="culture">The culture code of the translation to remove</param>
    public bool RemoveTranslate(string culture)
    {
        if (Translates == null) return false;
        
        var toRemove = Translates.FirstOrDefault(t => 
            string.Equals(t.Culture, culture, StringComparison.OrdinalIgnoreCase));
        
        return toRemove != null && Translates.Remove(toRemove);
    }

    /// <summary>
    /// Gets a translation for the specified culture
    /// </summary>
    /// <param name="culture">The culture code</param>
    /// <returns>The translation if found, null otherwise</returns>
    public Translate? GetTranslate(string culture)
    {
        return Translates?.FirstOrDefault(t => 
            string.Equals(t.Culture, culture, StringComparison.OrdinalIgnoreCase));
    }
}