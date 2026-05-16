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
    [property: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors
                                       | DynamicallyAccessedMemberTypes.PublicProperties)]
    Type LayoutType);
