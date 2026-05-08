namespace Zonit.Extensions.Website;

/// <summary>
/// Strongly-typed, AOT-safe accessor for a <see cref="string"/> property on a view-model.
/// Emitted by the <c>Zonit.Extensions.Website.SourceGenerators</c> source generator
/// so that <c>PageEditBase&lt;TViewModel&gt;</c> can clean/trim strings without reflection.
/// </summary>
/// <typeparam name="TViewModel">View-model type that owns the property.</typeparam>
/// <param name="Name">CLR property name (same as <c>PropertyInfo.Name</c>).</param>
/// <param name="Get">Getter delegate — returns the current string value (may be <c>null</c>).</param>
/// <param name="Set">Setter delegate — assigns a new string value.</param>
public sealed record StringPropertyAccessor<TViewModel>(
    string Name,
    Func<TViewModel, string?> Get,
    Action<TViewModel, string?> Set)
    where TViewModel : class;
