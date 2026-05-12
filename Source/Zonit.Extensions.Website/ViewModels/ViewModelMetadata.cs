using System.Collections.Concurrent;
using System.Text.Json.Serialization.Metadata;

namespace Zonit.Extensions.Website;

/// <summary>
/// AOT-safe metadata about a view-model type (<typeparamref name="TViewModel"/>),
/// supplied by the <c>Zonit.Extensions.Website.SourceGenerators</c> source generator.
/// </summary>
/// <remarks>
/// <para>The source generator scans every consumer assembly for classes that inherit from
/// <c>PageViewBase&lt;T&gt;</c> or <c>PageEditBase&lt;T&gt;</c>, collects each unique <c>T</c>, and
/// emits a concrete <see cref="ViewModelMetadata{TViewModel}"/> subclass plus a
/// <see cref="System.Runtime.CompilerServices.ModuleInitializerAttribute"/> that registers it
/// via <see cref="Register"/>. No user action is required.</para>
/// <para>When metadata is registered, <c>PageEditBase</c>/<c>PageViewBase</c> switch from
/// <see cref="System.Reflection"/> to the generated accessors, making them trim- and AOT-safe.
/// When metadata is <em>not</em> registered (e.g. generator missing or dynamic type),
/// the base classes fall back to reflection (backward-compatible).</para>
/// </remarks>
/// <typeparam name="TViewModel">The concrete view-model type.</typeparam>
public abstract class ViewModelMetadata<TViewModel> where TViewModel : class
{
    /// <summary>
    /// Singleton instance registered by the source-generator's module initializer.
    /// <c>null</c> when no generator has emitted metadata for <typeparamref name="TViewModel"/>.
    /// </summary>
    public static ViewModelMetadata<TViewModel>? Instance { get; private set; }

    /// <summary>
    /// Registers the metadata singleton. Called automatically by generated <c>[ModuleInitializer]</c>.
    /// </summary>
    /// <remarks>Safe to call multiple times — last registration wins.</remarks>
    public static void Register(ViewModelMetadata<TViewModel> metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        Instance = metadata;
    }

    /// <summary>
    /// All <see cref="string"/> properties on <typeparamref name="TViewModel"/> that are both
    /// readable and writable. Used by <c>PageEditBase.CleanModelData()</c>.
    /// </summary>
    public abstract IReadOnlyList<StringPropertyAccessor<TViewModel>> StringProperties { get; }

    /// <summary>
    /// All public read/write properties on <typeparamref name="TViewModel"/>, keyed by CLR name.
    /// Used by <c>PageEditBase.GetFieldValue</c>, <c>OnValueChanged</c>, and <c>AutoSave</c> lookup.
    /// </summary>
    public abstract IReadOnlyDictionary<string, PropertyAccessor<TViewModel>> Properties { get; }

    /// <summary>
    /// Optional source-generated <see cref="JsonTypeInfo{T}"/> for <typeparamref name="TViewModel"/>.
    /// When non-null, <c>PageViewBase</c> uses the AOT-safe <see cref="JsonTypeInfo{T}"/> overload
    /// of <c>PersistentComponentState.TryTakeFromJson</c> instead of the reflective one.
    /// </summary>
    public virtual JsonTypeInfo<TViewModel>? JsonTypeInfo => null;

    /// <summary>
    /// Creates a new instance of <typeparamref name="TViewModel"/>. Source-generated implementations
    /// use <c>new TViewModel()</c> directly (no <see cref="Activator.CreateInstance(Type)"/>).
    /// </summary>
    public abstract TViewModel CreateInstance();
}

/// <summary>
/// Type-indexed registry for untyped lookups (rarely used — prefer
/// <see cref="ViewModelMetadata{TViewModel}.Instance"/>).
/// </summary>
public static class ViewModelMetadataRegistry
{
    private static readonly ConcurrentDictionary<Type, object> _map = new();

    /// <summary>
    /// Called by generated module initializers to register a metadata instance under its CLR type.
    /// </summary>
    public static void Register<TViewModel>(ViewModelMetadata<TViewModel> metadata)
        where TViewModel : class
    {
        ArgumentNullException.ThrowIfNull(metadata);
        _map[typeof(TViewModel)] = metadata;
        ViewModelMetadata<TViewModel>.Register(metadata);
    }

    /// <summary>
    /// Looks up metadata by runtime <see cref="Type"/> (for non-generic reflection-style callers).
    /// </summary>
    public static object? TryGet(Type viewModelType) =>
        _map.TryGetValue(viewModelType, out var m) ? m : null;
}
