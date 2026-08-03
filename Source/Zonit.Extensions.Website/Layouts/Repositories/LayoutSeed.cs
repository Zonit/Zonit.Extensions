using System.Diagnostics.CodeAnalysis;

namespace Zonit.Extensions.Website.Layouts.Repositories;

/// <summary>
/// DI-registration record used by <c>services.AddWebsiteLayout&lt;TLayout&gt;(key)</c>
/// to seed <see cref="ILayoutRegistry"/> without pre-building the service provider.
/// </summary>
/// <remarks>
/// Multiple <see cref="LayoutSeed"/>s are registered as singletons; the
/// <see cref="ILayoutRegistry"/> factory enumerates them once on first resolve and
/// builds the immutable runtime map. This pattern avoids the "two-container"
/// anti-pattern of calling <c>BuildServiceProvider()</c> inside DI configuration.
/// </remarks>
/// <param name="Key">Case-insensitive layout key.</param>
/// <param name="LayoutType">Concrete <c>LayoutComponentBase</c> derivative.</param>
internal sealed record LayoutSeed(
    string Key,
    // The [property:] annotation alone is NOT enough. A positional record compiles to a primary
    // constructor that stores its parameter straight into the compiler-generated backing field,
    // and the trimmer propagates the property's requirement to that field — so an unannotated
    // parameter feeding an annotated field is an unsatisfied flow. ILC reports it as
    // "IL2069: value stored in field LayoutSeed.<LayoutType>k__BackingField does not satisfy
    // 'DynamicallyAccessedMembersAttribute' requirements"; the Roslyn trim analyzer that runs in
    // this repo's build does NOT (verified: the solution built 0/0 while ILC warned on
    // preview.9's identical shape). Both targets must carry the same member set.
    [param: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors
                                    | DynamicallyAccessedMemberTypes.PublicProperties)]
    [property: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors
                                       | DynamicallyAccessedMemberTypes.PublicProperties)]
    Type LayoutType);
