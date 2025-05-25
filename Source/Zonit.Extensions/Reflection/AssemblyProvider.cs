using System.Diagnostics;
using System.Reflection;

namespace Zonit.Extensions.Reflection;

/// <summary>
/// Provides methods for searching and filtering assemblies in the application.
/// </summary>
public static class AssemblyProvider
{
    /// <summary>
    /// Returns unique assemblies containing types that implement or inherit from type T.
    /// </summary>
    /// <typeparam name="T">Base type or interface to search for.</typeparam>
    /// <param name="includeMicrosoftAssemblies">Specifies whether to include Microsoft assemblies (default is false).</param>
    /// <returns>A collection of unique assemblies.</returns>
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
    /// Returns all types that implement or inherit from type T.
    /// </summary>
    /// <typeparam name="T">Base type or interface to search for.</typeparam>
    /// <param name="includeMicrosoftAssemblies">Specifies whether to include Microsoft assemblies (default is false).</param>
    /// <returns>A collection of types.</returns>
    public static IEnumerable<Type> GetTypes<T>(bool includeMicrosoftAssemblies = false)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => includeMicrosoftAssemblies || !IsMicrosoftAssembly(a))
            .SelectMany(s => GetLoadableTypes(s))
            .Where(p => !p.IsAbstract && typeof(T).IsAssignableFrom(p));
    }

    /// <summary>
    /// Checks whether the given assembly originates from Microsoft.
    /// </summary>
    private static bool IsMicrosoftAssembly(Assembly assembly) =>
        assembly.FullName != null &&
        (assembly.FullName.Contains("Microsoft", StringComparison.OrdinalIgnoreCase) ||
         assembly.FullName.Contains("System.", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Safely retrieves types from the given assembly, handling potential exceptions.
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
            Debug.WriteLine($"Error loading assembly {assembly.FullName}: {ex.Message}");

            // Return only the types that were successfully loaded (remove nulls)
            return ex.Types.Where(t => t != null)!;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Unexpected error while loading types from {assembly.FullName}: {ex.Message}");
            return [];
        }
    }
}