using System.Diagnostics;
using System.Reflection;

namespace Zonit.Extensions.Reflection;

/// <summary>
/// Dostarcza metody do wyszukiwania i filtrowania assembly w aplikacji.
/// </summary>
public static class AssemblyProvider
{
    /// <summary>
    /// Zwraca unikalne assembly zawieraj¹ce typy implementuj¹ce lub dziedzicz¹ce po typie T.
    /// </summary>
    /// <typeparam name="T">Typ bazowy lub interfejs do wyszukania.</typeparam>
    /// <param name="includeMicrosoftAssemblies">Okreœla czy uwzglêdniaæ assembly Microsoft (domyœlnie false).</param>
    /// <returns>Kolekcja unikalnych assembly.</returns>
    public static IEnumerable<Assembly> GetAssemblies<T>(bool includeMicrosoftAssemblies = false)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => includeMicrosoftAssemblies || !IsMicrosoftAssembly(a))
            .SelectMany(s => GetLoadableTypes(s))
            .Where(p => !p.IsAbstract && typeof(T).IsAssignableFrom(p))
            .Select(x => x.Assembly)
            .Distinct();
    }

    /// <summary>
    /// Zwraca wszystkie typy implementuj¹ce lub dziedzicz¹ce po typie T.
    /// </summary>
    /// <typeparam name="T">Typ bazowy lub interfejs do wyszukania.</typeparam>
    /// <param name="includeMicrosoftAssemblies">Okreœla czy uwzglêdniaæ assembly Microsoft (domyœlnie false).</param>
    /// <returns>Kolekcja typów.</returns>
    public static IEnumerable<Type> GetTypes<T>(bool includeMicrosoftAssemblies = false)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => includeMicrosoftAssemblies || !IsMicrosoftAssembly(a))
            .SelectMany(s => GetLoadableTypes(s))
            .Where(p => !p.IsAbstract && typeof(T).IsAssignableFrom(p));
    }

    /// <summary>
    /// Sprawdza czy podane assembly pochodzi od Microsoft.
    /// </summary>
    private static bool IsMicrosoftAssembly(Assembly assembly) =>
        assembly.FullName != null && 
        (assembly.FullName.Contains("Microsoft", StringComparison.OrdinalIgnoreCase) ||
         assembly.FullName.Contains("System.", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Bezpiecznie pobiera typy z danego assembly, obs³uguj¹c potencjalne wyj¹tki.
    /// </summary>
    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        if (assembly == null)
            return [];

        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            Debug.WriteLine($"B³¹d ³adowania assembly {assembly.FullName}: {ex.Message}");
            
            // Zwracamy tylko te typy, które uda³o siê za³adowaæ (usuwamy nulle)
            return ex.Types.Where(t => t != null)!;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Nieoczekiwany b³¹d podczas ³adowania typów z {assembly.FullName}: {ex.Message}");
            return [];
        }
    }
}