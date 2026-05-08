using System.ComponentModel.DataAnnotations;
using Zonit.Extensions.Website;

namespace ExampleWebsite.Components.Demo;

/// <summary>
/// Demo view-model used to verify the source generator: Zonit.Extensions.Website.SourceGenerators
/// must emit a <c>__ZonitVMMetadata_ExampleWebsite_Components_Demo_DemoViewModel</c> class
/// and a <c>[ModuleInitializer]</c> registering it, so that <see cref="DemoEditPage"/> works
/// in AOT/trimmed mode without reflection.
/// </summary>
public class DemoViewModel
{
    [Required, MinLength(3)]
    public string Name { get; set; } = "";

    [AutoSave(DelayMs = 500)]
    public string Email { get; set; } = "";

    public int Age { get; set; }

    public bool IsActive { get; set; } = true;
}
