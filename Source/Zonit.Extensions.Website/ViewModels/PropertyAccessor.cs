namespace Zonit.Extensions.Website;

/// <summary>
/// Strongly-typed, AOT-safe accessor for any property on a view-model.
/// Emitted by the <c>Zonit.Extensions.Website.SourceGenerators</c> source generator.
/// </summary>
/// <typeparam name="TViewModel">View-model type that owns the property.</typeparam>
/// <param name="Name">CLR property name.</param>
/// <param name="PropertyType">Declared CLR type of the property (for runtime type-matching without reflection).</param>
/// <param name="Get">Boxed-getter — returns value as <see cref="object"/> for generic code paths.</param>
/// <param name="Set">Boxed-setter — accepts value as <see cref="object"/> and assigns it.</param>
/// <param name="AutoSave">Optional <see cref="AutoSaveAttribute"/> metadata if the property is annotated.</param>
public sealed record PropertyAccessor<TViewModel>(
    string Name,
    Type PropertyType,
    Func<TViewModel, object?> Get,
    Action<TViewModel, object?> Set,
    AutoSaveAttribute? AutoSave = null)
    where TViewModel : class;
