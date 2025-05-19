using System.Diagnostics;
using System.Reflection;

namespace Zonit.Extensions.Reflection;

/// <summary>
/// Dostarcza metody do wyszukiwania i filtrowania assembly w aplikacji.
/// </summary>
public static class AssemblyProvider
{
    /// <summary>
    /// Zwraca unikalne assembly zawieraj�ce typy implementuj�ce lub dziedzicz�ce po typie T.
    /// </summary>
    /// <typeparam name="T">Typ bazowy lub interfejs do wyszukania.</typeparam>
    /// <param name="includeMicrosoftAssemblies">Okre�la czy uwzgl�dnia� assembly Microsoft (domy�lnie false).</param>
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
    /// Zwraca wszystkie typy implementuj�ce lub dziedzicz�ce po typie T.
    /// </summary>
    /// <typeparam name="T">Typ bazowy lub interfejs do wyszukania.</typeparam>
    /// <param name="includeMicrosoftAssemblies">Okre�la czy uwzgl�dnia� assembly Microsoft (domy�lnie false).</param>
    /// <returns>Kolekcja typ�w.</returns>
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
    /// Bezpiecznie pobiera typy z danego assembly, obs�uguj�c potencjalne wyj�tki.
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
            Debug.WriteLine($"B��d �adowania assembly {assembly.FullName}: {ex.Message}");
            
            // Zwracamy tylko te typy, kt�re uda�o si� za�adowa� (usuwamy nulle)
            return ex.Types.Where(t => t != null)!;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Nieoczekiwany b��d podczas �adowania typ�w z {assembly.FullName}: {ex.Message}");
            return [];
        }
    }
}