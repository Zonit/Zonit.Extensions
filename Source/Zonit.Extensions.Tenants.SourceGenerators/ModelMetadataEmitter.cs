using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Zonit.Extensions.Tenants.SourceGenerators;

/// <summary>
/// Emits <c>JsonTypeInfo&lt;TModel&gt;</c> for every setting model in the compilation, so a
/// consumer needs neither a <c>JsonSerializerContext</c> nor a <c>Hydrate</c> override to be
/// AOT-safe.
/// </summary>
/// <remarks>
/// <para><b>Why this exists rather than deferring to System.Text.Json's generator.</b> Roslyn
/// generators do not observe each other's output, so emitting
/// <c>[JsonSerializable(typeof(PricingModel))] partial class Ctx : JsonSerializerContext</c> from
/// here produces a class the System.Text.Json generator never sees and the compiler rejects with
/// <c>CS0534</c>. What that generator ultimately *emits*, however, is ordinary calls into the
/// public <c>JsonMetadataServices</c> API — and nothing stops this generator from emitting the
/// same calls directly. A <c>Setting&lt;TModel&gt;</c> declaration already names the model, so the
/// input the other generator would have needed is here.</para>
///
/// <para><b>Scope, deliberately narrow.</b> Only flat models are handled: properties whose type is
/// a scalar (covered by <c>TenantPrimitiveJsonResolver</c> in the runtime package), an enum, or a
/// nullable of either. That is what a settings model is — a POCO with <c>DataAnnotations</c> that
/// an <c>EditForm</c> binds against. A model with a nested object, a collection or a dictionary is
/// skipped entirely and reported as <c>ZONITTS0003</c>, which tells the author to supply a
/// <c>JsonSerializerContext</c> for it. Emitting half a model would be worse than emitting none:
/// the missing property would bind to its default and look like data loss.</para>
///
/// <para>Enums are emitted per assembly because
/// <c>JsonMetadataServices.GetEnumConverter&lt;TEnum&gt;</c> needs the closed generic; every other
/// scalar lives once in the runtime package.</para>
/// </remarks>
internal static class ModelMetadataEmitter
{
    /// <summary>A model that can be emitted, reduced to value-equal strings for the incremental cache.</summary>
    internal sealed record ModelInfo(string ModelFullName, string PropertyLines, string EnumTypeNames)
    {
        private const char Separator = '\n';

        public IEnumerable<string> Properties()
            => PropertyLines.Length == 0 ? System.Array.Empty<string>() : PropertyLines.Split(Separator);

        public IEnumerable<string> EnumTypes()
            => EnumTypeNames.Length == 0 ? System.Array.Empty<string>() : EnumTypeNames.Split(Separator);

        /// <summary>
        /// Inspects <paramref name="model"/> and returns metadata for it, or <see langword="null"/>
        /// when any settable property has a shape this emitter does not cover.
        /// </summary>
        public static ModelInfo? From(INamedTypeSymbol model)
        {
            // A parameterless constructor is required: Setting<T> is constrained `class, new()`
            // and "no override" is answered with new().
            if (model.IsAbstract || model.IsGenericType) return null;

            var properties = new List<string>();
            var enums = new SortedSet<string>(System.StringComparer.Ordinal);

            foreach (var member in model.GetMembers())
            {
                if (member is not IPropertySymbol property) continue;
                if (property.IsStatic || property.IsIndexer) continue;
                if (property.DeclaredAccessibility != Accessibility.Public) continue;

                // Read-only properties are not an obstacle — System.Text.Json simply never writes
                // them back — but a property with no accessible setter cannot carry an override,
                // and silently ignoring one would be indistinguishable from a typo. Skip the
                // model so the author gets ZONITTS0003 instead.
                if (property.GetMethod is null || property.GetMethod.DeclaredAccessibility != Accessibility.Public)
                    return null;
                if (property.SetMethod is null || property.SetMethod.DeclaredAccessibility != Accessibility.Public)
                    return null;

                if (!IsSupported(property.Type, enums))
                    return null;

                // FullyQualifiedFormat rather than a hand-prefixed "global::": it renders special
                // types as their keyword (`string`, not `global::System.String`, which is a
                // syntax error) and everything else with the global alias already attached.
                var propertyType = property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                properties.Add($"{property.Name}|{propertyType}");
            }

            return properties.Count == 0
                ? null
                : new ModelInfo(
                    model.ToDisplayString(),
                    string.Join(Separator.ToString(), properties),
                    string.Join(Separator.ToString(), enums));
        }

        /// <summary>
        /// Whether the runtime package's scalar resolver, or a per-assembly enum entry, can supply
        /// this property type's metadata.
        /// </summary>
        private static bool IsSupported(ITypeSymbol type, SortedSet<string> enums)
        {
            // string? and int? reach here as the same symbols their non-nullable forms do for
            // reference types; for value types Nullable<T> is a distinct constructed type.
            if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
                return IsSupported(nullable.TypeArguments[0], enums);

            if (type.TypeKind == TypeKind.Enum)
            {
                enums.Add(type.ToDisplayString());
                return true;
            }

            switch (type.SpecialType)
            {
                case SpecialType.System_String:
                case SpecialType.System_Boolean:
                case SpecialType.System_Char:
                case SpecialType.System_Byte:
                case SpecialType.System_SByte:
                case SpecialType.System_Int16:
                case SpecialType.System_UInt16:
                case SpecialType.System_Int32:
                case SpecialType.System_UInt32:
                case SpecialType.System_Int64:
                case SpecialType.System_UInt64:
                case SpecialType.System_Single:
                case SpecialType.System_Double:
                case SpecialType.System_Decimal:
                case SpecialType.System_DateTime:
                    return true;
            }

            return type.ToDisplayString() switch
            {
                "System.Guid" or "System.DateTimeOffset" or "System.DateOnly"
                    or "System.TimeOnly" or "System.TimeSpan" or "System.Uri" or "System.Version" => true,
                _ => false,
            };
        }
    }

    /// <summary>
    /// Emits the per-assembly resolver: one <c>JsonTypeInfo</c> factory per model, plus one per
    /// enum the models use.
    /// </summary>
    public static string Emit(string hintNamespace, IReadOnlyList<ModelInfo> models, IReadOnlyList<string> contexts)
    {
        var enums = new SortedSet<string>(System.StringComparer.Ordinal);
        foreach (var model in models)
            foreach (var e in model.EnumTypes())
                enums.Add(e);

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine($"namespace {hintNamespace};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Source-generated System.Text.Json metadata for the tenant setting models declared in");
        sb.AppendLine("/// this assembly. Registered automatically — see <c>TenantSettingsExtensions.AddJsonContexts</c>.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("/// <remarks>");
        sb.AppendLine("/// Built on <c>JsonMetadataServices</c>, the same public API the System.Text.Json generator");
        sb.AppendLine("/// emits calls to, so hydration needs no reflection and survives trimming and Native AOT.");
        sb.AppendLine("/// </remarks>");
        sb.AppendLine("internal sealed class TenantSettingsJsonMetadata : global::System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver");
        sb.AppendLine("{");
        sb.AppendLine("    internal static readonly TenantSettingsJsonMetadata Default = new();");
        sb.AppendLine();
        sb.AppendLine("    public global::System.Text.Json.Serialization.Metadata.JsonTypeInfo? GetTypeInfo(");
        sb.AppendLine("        global::System.Type type, global::System.Text.Json.JsonSerializerOptions options)");
        sb.AppendLine("    {");

        foreach (var model in models)
            sb.AppendLine($"        if (type == typeof(global::{model.ModelFullName})) return {FactoryName(model.ModelFullName)}(options);");

        foreach (var e in enums)
        {
            sb.AppendLine($"        if (type == typeof(global::{e})) return global::System.Text.Json.Serialization.Metadata.JsonMetadataServices.CreateValueInfo<global::{e}>(");
            sb.AppendLine($"            options, global::System.Text.Json.Serialization.Metadata.JsonMetadataServices.GetEnumConverter<global::{e}>(options));");
            sb.AppendLine($"        if (type == typeof(global::{e}?)) return global::System.Text.Json.Serialization.Metadata.JsonMetadataServices.CreateValueInfo<global::{e}?>(");
            sb.AppendLine($"            options, global::System.Text.Json.Serialization.Metadata.JsonMetadataServices.GetNullableConverter(");
            sb.AppendLine($"                global::System.Text.Json.Serialization.Metadata.JsonMetadataServices.CreateValueInfo<global::{e}>(");
            sb.AppendLine($"                    options, global::System.Text.Json.Serialization.Metadata.JsonMetadataServices.GetEnumConverter<global::{e}>(options))));");
        }

        sb.AppendLine("        return null;");
        sb.AppendLine("    }");

        foreach (var model in models)
            EmitModelFactory(sb, model);

        sb.AppendLine("}");

        EmitModuleInitializer(sb, models.Count > 0, contexts);
        return sb.ToString();
    }

    /// <summary>
    /// Emits the module initializer that hands this assembly's metadata to the runtime registry,
    /// so nothing has to be registered by hand.
    /// </summary>
    /// <remarks>
    /// <para><b>Why a module initializer.</b> The metadata lives here, in the consumer's assembly;
    /// <c>AddTenantsExtension()</c> is compiled into <c>Zonit.Extensions.Tenants</c> and cannot
    /// name a type that does not exist at its compile time. A module initializer is the only thing
    /// that runs without anyone calling it, and it runs before any other code in this assembly —
    /// so by the time a <c>Setting&lt;T&gt;</c> declared here can possibly be read, the metadata
    /// for it is registered.</para>
    ///
    /// <para>Hand-written <c>JsonSerializerContext</c>s covering setting models are registered the
    /// same way, which is what makes <c>AddJsonContexts()</c> optional rather than required.</para>
    /// </remarks>
    private static void EmitModuleInitializer(StringBuilder sb, bool hasGenerated, IReadOnlyList<string> contexts)
    {
        if (!hasGenerated && contexts.Count == 0) return;

        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Registers this assembly's tenant setting metadata as it loads, so no wiring is needed");
        sb.AppendLine("/// in <c>Program.cs</c>.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("internal static class TenantSettingsJsonRegistration");
        sb.AppendLine("{");
        sb.AppendLine("    [global::System.Runtime.CompilerServices.ModuleInitializer]");
        sb.AppendLine("    internal static void Register()");
        sb.AppendLine("    {");

        foreach (var context in contexts)
            sb.AppendLine($"        global::Zonit.Extensions.Tenants.Settings.TenantSettingsMetadata.Register(global::{context}.Default);");

        if (hasGenerated)
            sb.AppendLine("        global::Zonit.Extensions.Tenants.Settings.TenantSettingsMetadata.Register(TenantSettingsJsonMetadata.Default);");

        sb.AppendLine("    }");
        sb.AppendLine("}");
    }

    private static void EmitModelFactory(StringBuilder sb, ModelInfo model)
    {
        var type = $"global::{model.ModelFullName}";

        sb.AppendLine();
        sb.AppendLine($"    private static global::System.Text.Json.Serialization.Metadata.JsonTypeInfo<{type}> {FactoryName(model.ModelFullName)}(");
        sb.AppendLine("        global::System.Text.Json.JsonSerializerOptions options)");
        sb.AppendLine($"        => global::System.Text.Json.Serialization.Metadata.JsonMetadataServices.CreateObjectInfo<{type}>(");
        sb.AppendLine("            options,");
        sb.AppendLine($"            new global::System.Text.Json.Serialization.Metadata.JsonObjectInfoValues<{type}>");
        sb.AppendLine("            {");
        sb.AppendLine($"                ObjectCreator = static () => new {type}(),");
        // The initializer's parameter is the JsonSerializerContext, which is null for a plain
        // resolver — the options are captured from the enclosing call instead.
        sb.AppendLine("                PropertyMetadataInitializer = _ =>");
        sb.AppendLine("                [");

        foreach (var line in model.Properties())
        {
            var parts = line.Split('|');
            var name = parts[0];
            var propertyType = parts[1];

            sb.AppendLine($"                    global::System.Text.Json.Serialization.Metadata.JsonMetadataServices.CreatePropertyInfo<{propertyType}>(");
            sb.AppendLine($"                        options, new global::System.Text.Json.Serialization.Metadata.JsonPropertyInfoValues<{propertyType}>");
            sb.AppendLine("                        {");
            sb.AppendLine("                            IsProperty = true,");
            sb.AppendLine("                            IsPublic = true,");
            sb.AppendLine($"                            DeclaringType = typeof({type}),");
            sb.AppendLine($"                            PropertyName = \"{name}\",");
            sb.AppendLine($"                            Getter = static obj => (({type})obj).{name},");
            sb.AppendLine($"                            Setter = static (obj, value) => (({type})obj).{name} = value!,");
            sb.AppendLine("                        }),");
        }

        sb.AppendLine("                ],");
        sb.AppendLine("            });");
    }

    /// <summary>A collision-free method name for a model's factory.</summary>
    private static string FactoryName(string modelFullName)
        => "Create_" + modelFullName.Replace('.', '_').Replace('+', '_');
}
