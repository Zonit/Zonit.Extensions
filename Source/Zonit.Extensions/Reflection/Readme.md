# AssemblyProvider

A simple utility class for searching, filtering, and retrieving assemblies and types in an application. It helps you discover unique assemblies or types that implement or inherit from a specified base class or interface. By default, it filters out Microsoft-generated assemblies. You can optionally include them by setting a parameter.

## Features

1. **Find Assemblies by Type**  
   Returns a unique collection of assemblies that contain types implementing or inheriting from a given class or interface.

2. **Find Types**  
   Returns a collection of types (non-abstract) implementing or inheriting from a given class or interface.

3. **Exclude or Include Microsoft Assemblies**  
   Easily skip Microsoft assemblies to reduce noise when scanning for your custom types. Set a flag to `true` when you need to include them.

4. **Graceful Error Handling**  
   Reflection-related exceptions (e.g., ReflectionTypeLoadException) are caught and logged, returning only successfully loaded types.

## Usage

### 1. Get Assemblies
Use `GetAssemblies<T>` to retrieve assemblies which contain types implementing or inheriting from `T`.  
By default, assemblies from Microsoft or System namespaces are skipped.

Example:
```csharp
using System;
using System.Collections.Generic;
using System.Reflection;
using Zonit.Extensions.Reflection;

public interface IMyService
{
    void Execute();
}

public class MyServiceImplementation : IMyService
{
    public void Execute()
    {
        Console.WriteLine("Service Executed!");
    }
}

class Program
{
    static void Main()
    {
        // Get assemblies containing implementations of IMyService
        IEnumerable<Assembly> assemblies = AssemblyProvider.GetAssemblies<IMyService>();

        // Print assembly names
        foreach (var assembly in assemblies)
        {
            Console.WriteLine(assembly.FullName);
        }
    }
}
```

### 2. Get Types
Use `GetTypes<T>` to retrieve all non-abstract types that implement or inherit from `T`.

Example:
```csharp
using System;
using System.Collections.Generic;
using Zonit.Extensions.Reflection;

public interface IMyService
{
    void Execute();
}

class Program
{
    static void Main()
    {
        // Get types that implement IMyService
        IEnumerable<Type> serviceTypes = AssemblyProvider.GetTypes<IMyService>();

        // Print type names
        foreach (Type type in serviceTypes)
        {
            Console.WriteLine(type.FullName);
        }
    }
}
```

### 3. Including Microsoft Assemblies
If you also want to scan Microsoft assemblies, simply pass `true` as a parameter:

```csharp
using Zonit.Extensions.Reflection;

var allAssemblies = AssemblyProvider.GetAssemblies<IMyService>(includeMicrosoftAssemblies: true);
var allTypes = AssemblyProvider.GetTypes<IMyService>(includeMicrosoftAssemblies: true);
```

## Implementation Details

- The core logic uses:
  - `AppDomain.CurrentDomain.GetAssemblies()` to fetch all loaded assemblies in the current application domain.
  - A filter to exclude (or include) assemblies with names that contain "Microsoft" or "System.".
  - Reflection to get and navigate through all loadable types even if some fail to load.
  - Safe handling of `ReflectionTypeLoadException` by logging the error and returning only the types that successfully loaded.