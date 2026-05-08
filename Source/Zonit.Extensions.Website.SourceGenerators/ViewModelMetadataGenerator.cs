using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Zonit.Extensions.Website.SourceGenerators;

/// <summary>
/// Incremental source generator that emits AOT-safe <c>ViewModelMetadata&lt;T&gt;</c>
/// subclasses for every view-model type used as a type parameter in
/// <c>PageViewBase&lt;T&gt;</c> or <c>PageEditBase&lt;T&gt;</c> in the consumer's assembly.
/// </summary>
/// <remarks>
/// <para>For each unique view-model <c>T</c> the generator emits:</para>
/// <list type="bullet">
///   <item>a concrete subclass of <c>ViewModelMetadata&lt;T&gt;</c> with compile-time
///         delegates for every public read/write property and a <c>StringProperties</c>
///         subset for <c>PageEditBase.CleanModelData</c>;</item>
///   <item>a <c>[ModuleInitializer]</c> that registers the metadata instance via
///         <c>ViewModelMetadataRegistry.Register</c> before any Blazor code runs.</item>
/// </list>
/// <para>Consumers don't have to touch anything — adding a class like
/// <c>MyPage : PageEditBase&lt;MyVM&gt;</c> to their assembly causes the generator
/// to automatically wire up AOT-safe metadata for <c>MyVM</c> at build time.</para>
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class ViewModelMetadataGenerator : IIncrementalGenerator
{
    private const string PageBaseNamespace = "Zonit.Extensions.Website";
    private const string AutoSaveAttributeMetadataName = "Zonit.Extensions.Website.AutoSaveAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find every class declaration that has a base type with type arguments
        // (cheap syntactic filter — deep check happens in the symbol phase).
        var candidateClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsCandidateClass(node),
                transform: static (ctx, ct) => ExtractViewModelType(ctx, ct))
            .Where(static vm => vm is not null)
            .Select(static (vm, _) => vm!);

        // Deduplicate view-model types across the whole compilation.
        var uniqueViewModels = candidateClasses.Collect();

        context.RegisterSourceOutput(uniqueViewModels, Emit);
    }

    private static bool IsCandidateClass(SyntaxNode node)
    {
        if (node is not ClassDeclarationSyntax cls || cls.BaseList is null)
            return false;

        // Must have at least one generic base type.
        foreach (var baseType in cls.BaseList.Types)
        {
            if (baseType.Type is GenericNameSyntax)
                return true;
        }
        return false;
    }

    private static ViewModelCandidate? ExtractViewModelType(GeneratorSyntaxContext ctx, System.Threading.CancellationToken ct)
    {
        var cls = (ClassDeclarationSyntax)ctx.Node;
        var semantic = ctx.SemanticModel;

        var symbol = semantic.GetDeclaredSymbol(cls, ct) as INamedTypeSymbol;
        if (symbol is null)
            return null;

        // Walk up the base-type chain looking for PageViewBase<T> / PageEditBase<T>.
        for (var current = symbol.BaseType; current is not null; current = current.BaseType)
        {
            if (!current.IsGenericType || current.TypeArguments.Length != 1)
                continue;

            // Match by metadata name + namespace; cheaper than full display-string comparison.
            if (current.ConstructedFrom.MetadataName is not ("PageViewBase`1" or "PageEditBase`1"))
                continue;
            if (current.ConstructedFrom.ContainingNamespace?.ToDisplayString() != PageBaseNamespace)
                continue;

            if (current.TypeArguments[0] is not INamedTypeSymbol vmSymbol)
                return null;

            // View-model must be accessible, have a public parameterless constructor,
            // and not be abstract / generic definition.
            if (vmSymbol.IsAbstract || vmSymbol.IsUnboundGenericType || vmSymbol.IsGenericType)
                return null;
            if (vmSymbol.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal))
                return null;
            if (!HasPublicParameterlessConstructor(vmSymbol))
                return null;

            return BuildCandidate(vmSymbol);
        }

        return null;
    }

    private static bool IsJsonContextSafe(INamedTypeSymbol vm)
    {
        // STJ source generator cannot bind property names for nested or generic types reliably,
        // so for those we skip JsonTypeInfo emission and PageViewBase will fall back to reflection.
        if (vm.ContainingType is not null) return false;
        if (vm.IsGenericType) return false;
        return true;
    }

    private static bool HasPublicParameterlessConstructor(INamedTypeSymbol type)
    {
        foreach (var ctor in type.InstanceConstructors)
        {
            if (ctor.DeclaredAccessibility == Accessibility.Public && ctor.Parameters.Length == 0)
                return true;
        }
        // Records / structs without any ctor declared still have an implicit public ctor.
        return type.InstanceConstructors.Length == 0 && !type.IsRecord;
    }

    private static ViewModelCandidate BuildCandidate(INamedTypeSymbol vm)
    {
        var fullName = vm.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var sanitized = SanitizeIdentifier(fullName);

        var props = ImmutableArray.CreateBuilder<PropertyInfo>();
        foreach (var member in vm.GetMembers())
        {
            if (member is not IPropertySymbol prop) continue;
            if (prop.IsStatic || prop.IsIndexer) continue;
            if (prop.DeclaredAccessibility != Accessibility.Public) continue;
            if (prop.GetMethod is null || prop.SetMethod is null) continue;
            if (prop.SetMethod.DeclaredAccessibility != Accessibility.Public) continue;

            var autoSaveDelay = ExtractAutoSaveDelay(prop);
            var isString = prop.Type.SpecialType == SpecialType.System_String;
            var propTypeName = prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            props.Add(new PropertyInfo(prop.Name, propTypeName, isString, autoSaveDelay));
        }

        var simpleName = vm.Name;
        var emitJson = IsJsonContextSafe(vm);
        return new ViewModelCandidate(fullName, simpleName, sanitized, props.ToImmutable(), emitJson);
    }

    private static int? ExtractAutoSaveDelay(IPropertySymbol prop)
    {
        foreach (var attr in prop.GetAttributes())
        {
            if (attr.AttributeClass?.ToDisplayString() != AutoSaveAttributeMetadataName)
                continue;

            // AutoSaveAttribute has ctor(int) or property DelayMs.
            if (attr.ConstructorArguments.Length == 1 &&
                attr.ConstructorArguments[0].Value is int ctorDelay)
            {
                return ctorDelay;
            }
            foreach (var named in attr.NamedArguments)
            {
                if (named.Key == "DelayMs" && named.Value.Value is int namedDelay)
                    return namedDelay;
            }
            return 800; // default from AutoSaveAttribute.
        }
        return null;
    }

    private static string SanitizeIdentifier(string fullyQualifiedName)
    {
        var sb = new StringBuilder(fullyQualifiedName.Length);
        foreach (var c in fullyQualifiedName)
        {
            sb.Append(char.IsLetterOrDigit(c) ? c : '_');
        }
        return sb.ToString();
    }

    private static void Emit(SourceProductionContext spc, ImmutableArray<ViewModelCandidate> candidates)
    {
        if (candidates.IsDefaultOrEmpty)
            return;

        // Dedup by full name — a VM can be reached by multiple derived pages.
        var unique = new Dictionary<string, ViewModelCandidate>();
        foreach (var c in candidates)
        {
            if (!unique.ContainsKey(c.FullyQualifiedName))
                unique[c.FullyQualifiedName] = c;
        }

        var sb = new StringBuilder(4096);
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("#pragma warning disable CS0618 // obsolete members are still generated for completeness");
        sb.AppendLine();
        sb.AppendLine("namespace Zonit.Extensions.Website.Generated;");
        sb.AppendLine();
        sb.AppendLine("using global::System;");
        sb.AppendLine("using global::System.Collections.Generic;");
        sb.AppendLine("using global::System.Runtime.CompilerServices;");
        sb.AppendLine("using global::Zonit.Extensions.Website;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Auto-registration of AOT-safe ViewModel metadata emitted by");
        sb.AppendLine("/// <c>Zonit.Extensions.Website.SourceGenerators</c>. Do not edit.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("internal static class __ZonitViewModelRegistrations");
        sb.AppendLine("{");
        sb.AppendLine("    [ModuleInitializer]");
        sb.AppendLine("    internal static void Register()");
        sb.AppendLine("    {");
        foreach (var c in unique.Values)
        {
            sb.Append("        ViewModelMetadataRegistry.Register<").Append(c.FullyQualifiedName)
              .Append(">(new __ZonitVMMetadata_").Append(c.SanitizedIdentifier).AppendLine("());");
        }
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();

        foreach (var c in unique.Values)
        {
            EmitMetadataClass(sb, c);
        }

        // NOTE: We intentionally do NOT emit a JsonSerializerContext here yet.
        // .NET 10's PersistentComponentState.PersistAsJson does not have a JsonTypeInfo overload
        // (only the reflection-based 2-arg form exists). Once .NET 11 adds it, re-enable JSON
        // emission and override ViewModelMetadata<T>.JsonTypeInfo accordingly.

        spc.AddSource("ZonitViewModelMetadata.g.cs", sb.ToString());
    }

    private static void EmitMetadataClass(StringBuilder sb, ViewModelCandidate c)
    {
        sb.Append("file sealed class __ZonitVMMetadata_").Append(c.SanitizedIdentifier)
          .Append(" : global::Zonit.Extensions.Website.ViewModelMetadata<")
          .Append(c.FullyQualifiedName).AppendLine(">");
        sb.AppendLine("{");

        // StringProperties
        sb.Append("    private static readonly global::System.Collections.Generic.IReadOnlyList<")
          .Append("global::Zonit.Extensions.Website.StringPropertyAccessor<").Append(c.FullyQualifiedName).Append(">> _stringProps = new ")
          .Append("global::Zonit.Extensions.Website.StringPropertyAccessor<").Append(c.FullyQualifiedName).AppendLine(">[]");
        sb.AppendLine("    {");
        foreach (var p in c.Properties)
        {
            if (!p.IsString) continue;
            sb.Append("        new(\"").Append(p.Name).Append("\", static vm => vm.").Append(p.Name)
              .Append(", static (vm, v) => vm.").Append(p.Name).AppendLine(" = v!),");
        }
        sb.AppendLine("    };");
        sb.AppendLine();

        // Properties dictionary
        sb.Append("    private static readonly global::System.Collections.Generic.IReadOnlyDictionary<string, ")
          .Append("global::Zonit.Extensions.Website.PropertyAccessor<").Append(c.FullyQualifiedName).Append(">> _props = new ")
          .Append("global::System.Collections.Generic.Dictionary<string, global::Zonit.Extensions.Website.PropertyAccessor<")
          .Append(c.FullyQualifiedName).AppendLine(">>");
        sb.AppendLine("    {");
        foreach (var p in c.Properties)
        {
            sb.Append("        [\"").Append(p.Name).Append("\"] = new(");
            sb.Append("\"").Append(p.Name).Append("\", ");
            sb.Append("typeof(").Append(p.TypeFullName).Append("), ");
            sb.Append("static vm => (object?)vm.").Append(p.Name).Append(", ");
            sb.Append("static (vm, v) => vm.").Append(p.Name).Append(" = (").Append(p.TypeFullName).Append(")v!");
            if (p.AutoSaveDelayMs is int delay)
            {
                sb.Append(", new global::Zonit.Extensions.Website.AutoSaveAttribute(").Append(delay).Append(")");
            }
            sb.AppendLine("),");
        }
        sb.AppendLine("    };");
        sb.AppendLine();

        sb.Append("    public override global::System.Collections.Generic.IReadOnlyList<")
          .Append("global::Zonit.Extensions.Website.StringPropertyAccessor<").Append(c.FullyQualifiedName)
          .AppendLine(">> StringProperties => _stringProps;");
        sb.AppendLine();
        sb.Append("    public override global::System.Collections.Generic.IReadOnlyDictionary<string, ")
          .Append("global::Zonit.Extensions.Website.PropertyAccessor<").Append(c.FullyQualifiedName)
          .AppendLine(">> Properties => _props;");
        sb.AppendLine();
        sb.Append("    public override ").Append(c.FullyQualifiedName).AppendLine(" CreateInstance() => new();");

        sb.AppendLine("}");
        sb.AppendLine();
    }

    // ---- records ----

    private sealed record ViewModelCandidate(
        string FullyQualifiedName,
        string SimpleName,
        string SanitizedIdentifier,
        ImmutableArray<PropertyInfo> Properties,
        bool EmitJsonContext);

    private sealed record PropertyInfo(
        string Name,
        string TypeFullName,
        bool IsString,
        int? AutoSaveDelayMs);
}
