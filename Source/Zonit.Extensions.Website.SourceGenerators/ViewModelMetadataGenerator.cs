using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

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
///         delegates for every public read/write property — including properties
///         inherited from base classes — and a <c>StringProperties</c> subset for
///         <c>PageEditBase.CleanModelData</c>;</item>
///   <item>a <c>[ModuleInitializer]</c> that registers the metadata instance via
///         <c>ViewModelMetadataRegistry.Register</c> before any Blazor code runs.</item>
/// </list>
/// <para>Consumers don't have to touch anything — adding a class like
/// <c>MyPage : PageEditBase&lt;MyVM&gt;</c> to their assembly causes the generator
/// to automatically wire up AOT-safe metadata for <c>MyVM</c> at build time.</para>
///
/// <para><b>The emitted code targets C# 9.</b> It is compiled by the CONSUMER, under whatever
/// <c>LangVersion</c> that consumer pinned — so anything newer than the floor is a build break
/// in code they did not write. preview.9/.10 emitted <c>file</c>-scoped types (C# 11) and a
/// file-scoped namespace (C# 10) and therefore failed with CS8936 on any project pinning
/// <c>&lt;LangVersion&gt;10.0&lt;/LangVersion&gt;</c> or lower. The floor cannot go below C# 9
/// because the whole registration mechanism is <c>[ModuleInitializer]</c>; when the consumer
/// pins less than that, the generator emits nothing and says so (<c>ZONITVM0004</c>) instead of
/// emitting code that cannot compile. Isolation that <c>file</c> used to provide is now obtained
/// by nesting the metadata classes <c>private</c> inside the registration holder — stronger than
/// <c>file</c>, and available in every C# version. The holder's own name carries the assembly
/// name purely as insurance: two assemblies emitting the same internal holder name is only
/// diagnosable (CS0436, under <c>InternalsVisibleTo</c>) at a site that references the type BY
/// NAME, and nothing ever does, so the previous shared name was not observably broken. Verified,
/// not assumed: a hand-written collision in a referencing assembly warns only once a use site
/// names it.</para>
///
/// <para><b>Shapes the metadata contract cannot express.</b> <c>PropertyAccessor&lt;T&gt;.Set</c>
/// is an <c>Action&lt;T, object?&gt;</c> — a write to an ALREADY CONSTRUCTED instance. An
/// init-only property (the default for records) rejects exactly that, so emitting an accessor
/// for it produces CS8852 in the consumer's build. Such properties are therefore left out of
/// the generated accessor tables. That is a real, if narrow, behaviour difference from the
/// reflective fallback (<c>PropertyInfo.SetValue</c> ignores <c>init</c>), so it is reported —
/// as a warning (<c>ZONITVM0001</c>) when the view model is actually used with
/// <c>PageEditBase&lt;T&gt;</c>, which is the only type in the framework that writes through
/// this metadata, and silently otherwise, because for a <c>PageViewBase&lt;T&gt;</c>-only view
/// model nothing ever calls <c>Set</c>.</para>
///
/// <para><b>Why no <c>JsonSerializerContext</c> is emitted.</b> Up to 10.0.0-preview.9 this
/// generator also emitted a <c>[JsonSerializable]</c>-annotated partial deriving from
/// <c>JsonSerializerContext</c>, on the assumption that System.Text.Json's own generator
/// would complete it. Roslyn does not chain generators — one generator never sees another
/// generator's output — so the abstract members were never implemented and EVERY consumer
/// compilation failed with CS0534. There is also nothing on <c>PersistentComponentState</c>
/// to feed a <c>JsonTypeInfo</c> into: under a key it exposes only the reflective
/// <c>PersistAsJson&lt;T&gt;</c> / <c>TryTakeFromJson&lt;T&gt;</c> in .NET 10
/// (<c>PersistAsBytes</c> / <c>TryTakeBytes</c> exist but are <c>internal</c>), and both JSON
/// methods hard-code a <c>JsonSerializerOptions</c> instance that has no <c>TypeInfoResolver</c>
/// — verified in the IL of <c>Microsoft.AspNetCore.Components</c> 10.0.9.
/// <c>PersistentComponentStateSerializer&lt;T&gt;</c> IS public and overridable, but
/// <c>PersistAsJson</c> does not consult it: its only consumer is the declarative
/// <c>[PersistentState]</c> component-property path, which resolves the serializer through
/// <c>MakeGenericType</c> + <c>IServiceProvider</c> and then reads/writes with the internal
/// byte API. So a generated <c>JsonTypeInfo</c> would have no caller on the key-based path the
/// framework's persistence actually uses. See <c>Example/Zonit.Extensions.ConsumerGate</c>
/// — the regression gate that fails the build if this generator ever emits uncompilable code
/// again.</para>
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class ViewModelMetadataGenerator : IIncrementalGenerator
{
    private const string PageBaseNamespace = "Zonit.Extensions.Website";
    private const string AutoSaveAttributeMetadataName = "Zonit.Extensions.Website.AutoSaveAttribute";
    private const string SetsRequiredMembersAttributeName = "System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute";
    private const string DiagnosticCategory = "Zonit.Extensions.Website";

    /// <summary>
    /// The oldest C# the emitted code compiles under. Bound by <c>[ModuleInitializer]</c> (C# 9),
    /// which the auto-registration mechanism cannot do without.
    /// </summary>
    private const LanguageVersion MinimumLanguageVersion = LanguageVersion.CSharp9;

    private static readonly DiagnosticDescriptor PropertyNotWritable = new(
        id: "ZONITVM0001",
        title: "View-model property is missing from the generated metadata",
        messageFormat: "Property '{1}.{0}' cannot be written through generated metadata ({2}), so PageEditBase skips it when trimming strings, resolving [AutoSave] and dispatching OnValueChanged. Give it a public 'set' accessor, or suppress ZONITVM0001 if it is deliberately not editable.",
        category: DiagnosticCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "PropertyAccessor<T>.Set writes to an already-constructed instance, which an init-only or non-public setter rejects. The reflective fallback could write such properties, so the generated fast path is not a drop-in replacement for them.");

    private static readonly DiagnosticDescriptor NoMetadataGenerated = new(
        id: "ZONITVM0002",
        title: "No AOT-safe view-model metadata was generated",
        messageFormat: "No AOT-safe metadata was generated for view model '{0}' because {1}. PageEditBase<{0}> therefore uses reflection, which is switched off once the app is trimmed or published with Native AOT.",
        category: DiagnosticCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The generator emits a concrete ViewModelMetadata<T> only for a non-abstract, non-generic, accessible view model with a public parameterless constructor. Reported only for view models reached through PageEditBase<T>, the one type that consumes this metadata.");

    private static readonly DiagnosticDescriptor RequiredMembersDefaulted = new(
        id: "ZONITVM0003",
        title: "Required members are default-initialised by the generated CreateInstance",
        messageFormat: "View model '{0}' has required member(s) {1}; the generated CreateInstance() initialises them with 'default' because the metadata contract has no way to supply real values",
        category: DiagnosticCategory,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "ViewModelMetadata<T>.CreateInstance() is parameterless, so required members can only be satisfied with an object initializer assigning default values. A type with required members can never be used with PageEditBase<T> anyway (CS9040 against its new() constraint), so nothing in the framework calls this.");

    private static readonly DiagnosticDescriptor LanguageVersionTooLow = new(
        id: "ZONITVM0004",
        title: "LangVersion is too low for generated view-model metadata",
        messageFormat: "No view-model metadata was generated: the generated code needs C# {0} ([ModuleInitializer]) but this project pins LangVersion to {1}. PageEditBase falls back to reflection, which is unavailable once the app is trimmed or published with Native AOT.",
        category: DiagnosticCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Emitting nothing keeps the consumer's build green on the reflective path; emitting C# 9 constructs into an older compilation would fail with an error in generated code.");

    /// <inheritdoc />
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

        // Project the two ambient inputs down to tiny values before combining: the
        // ParseOptions/Compilation objects change on every keystroke, a LanguageVersion
        // and an assembly name essentially never do, so Emit stays cached.
        var languageVersion = context.ParseOptionsProvider
            .Select(static (options, _) => options is CSharpParseOptions cs ? cs.LanguageVersion : LanguageVersion.Default);

        var assemblyName = context.CompilationProvider
            .Select(static (compilation, _) => compilation.AssemblyName ?? "Unknown");

        var input = uniqueViewModels.Combine(languageVersion).Combine(assemblyName);

        context.RegisterSourceOutput(input, static (spc, data) =>
        {
            var ((candidates, langVersion), asmName) = data;
            Emit(spc, candidates, langVersion, asmName);
        });
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
            var metadataName = current.ConstructedFrom.MetadataName;
            if (metadataName is not ("PageViewBase`1" or "PageEditBase`1"))
                continue;
            if (current.ConstructedFrom.ContainingNamespace?.ToDisplayString() != PageBaseNamespace)
                continue;

            if (current.TypeArguments[0] is not INamedTypeSymbol vmSymbol)
                return null;

            // PageEditBase<T> is the only type in the framework that WRITES through the
            // metadata, so the severity of "this property is not writable" depends on it.
            var usedByEditBase = metadataName == "PageEditBase`1";

            // Anchor diagnostics on the page declaration: it is always in source, even when
            // the view model itself arrives from a referenced assembly.
            var usage = LocationInfo.From(cls.Identifier.GetLocation());

            // A view model the generator cannot express is reported, not silently dropped:
            // the consumer keeps the reflective fallback, which trimming/AOT switches off.
            var unsupported = DescribeUnsupported(vmSymbol);
            if (unsupported is not null)
            {
                return new ViewModelCandidate(
                    vmSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    vmSymbol.ToDisplayString(),
                    string.Empty,
                    ImmutableArray<PropertyModel>.Empty,
                    ImmutableArray<string>.Empty,
                    ImmutableArray<SkippedProperty>.Empty,
                    usedByEditBase,
                    usage,
                    unsupported);
            }

            return BuildCandidate(vmSymbol, usedByEditBase, usage);
        }

        return null;
    }

    /// <summary>
    /// Returns a human-readable reason when no metadata class can be emitted for
    /// <paramref name="vm"/>, or <c>null</c> when the type is supported.
    /// </summary>
    private static string? DescribeUnsupported(INamedTypeSymbol vm)
    {
        if (vm.IsAbstract)
            return "it is abstract";
        if (vm.IsUnboundGenericType || vm.IsGenericType)
            return "it is a generic type";
        if (vm.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal))
            return "it is neither public nor internal, so generated code cannot name it";
        if (!HasPublicParameterlessConstructor(vm))
            return "it has no public parameterless constructor";
        return null;
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

    /// <summary>
    /// True when <c>new T()</c> alone satisfies every required member, i.e. the parameterless
    /// constructor is annotated <c>[SetsRequiredMembers]</c>. In that case the generated
    /// <c>CreateInstance</c> must NOT add an object initializer — it would overwrite whatever
    /// the constructor assigned with <c>default</c>.
    /// </summary>
    private static bool ParameterlessCtorSetsRequiredMembers(INamedTypeSymbol type)
    {
        foreach (var ctor in type.InstanceConstructors)
        {
            if (ctor.DeclaredAccessibility != Accessibility.Public || ctor.Parameters.Length != 0)
                continue;

            foreach (var attr in ctor.GetAttributes())
            {
                if (attr.AttributeClass?.ToDisplayString() == SetsRequiredMembersAttributeName)
                    return true;
            }
        }
        return false;
    }

    private static ViewModelCandidate BuildCandidate(INamedTypeSymbol vm, bool usedByEditBase, LocationInfo? usage)
    {
        var fullName = vm.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var sanitized = SanitizeIdentifier(fullName);

        var props = ImmutableArray.CreateBuilder<PropertyModel>();
        var skipped = ImmutableArray.CreateBuilder<SkippedProperty>();
        var required = ImmutableArray.CreateBuilder<string>();

        // Most-derived declaration of a name wins, exactly like C# member lookup at the call
        // site: an override or a `new` member shadows the base one.
        var seenProperties = new HashSet<string>(StringComparer.Ordinal);
        var seenRequired = new HashSet<string>(StringComparer.Ordinal);

        // Walk the whole base chain. GetMembers() alone returns only the type's OWN members, so
        // preview.9/.10 dropped every inherited property — silently, because the emitted code
        // still compiled. The reflective fallback it replaces uses GetProperties(Public|Instance),
        // which does include inherited ones.
        for (var type = vm; type is not null && type.SpecialType != SpecialType.System_Object; type = type.BaseType)
        {
            foreach (var member in type.GetMembers())
            {
                // A required FIELD must be satisfied by the object initializer too.
                if (member is IFieldSymbol field)
                {
                    if (field.IsRequired && !field.IsStatic && seenRequired.Add(field.Name))
                        required.Add(field.Name);
                    continue;
                }

                if (member is not IPropertySymbol prop) continue;
                if (prop.IsStatic || prop.IsIndexer) continue;

                // Collected BEFORE the public-only filter: a required member only has to be as
                // visible as its containing type (CS9032), so an internal view model may declare
                // internal required members — and CreateInstance still has to satisfy every one
                // of them or the emitted object initializer is CS9035.
                if (prop.IsRequired && seenRequired.Add(prop.Name))
                    required.Add(prop.Name);

                if (prop.DeclaredAccessibility != Accessibility.Public) continue;
                if (!seenProperties.Add(prop.Name)) continue;

                // No accessible getter → nothing to read; no setter at all → a computed
                // property, which the reflective path skips as well (CanWrite is false),
                // so there is no behaviour difference worth reporting.
                if (prop.GetMethod is null || prop.GetMethod.DeclaredAccessibility != Accessibility.Public)
                    continue;
                if (prop.SetMethod is null)
                    continue;

                // These two DO differ from the reflective path — PropertyInfo.SetValue writes
                // init-only and non-public setters happily, an Action<T, object?> cannot.
                if (prop.SetMethod.IsInitOnly)
                {
                    skipped.Add(new SkippedProperty(prop.Name, "it has an init-only setter", LocationOf(prop, usage)));
                    continue;
                }
                if (prop.SetMethod.DeclaredAccessibility != Accessibility.Public)
                {
                    skipped.Add(new SkippedProperty(prop.Name, "its setter is not public", LocationOf(prop, usage)));
                    continue;
                }

                var autoSaveDelay = ExtractAutoSaveDelay(prop);
                var isString = prop.Type.SpecialType == SpecialType.System_String;
                var propTypeName = prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                props.Add(new PropertyModel(prop.Name, propTypeName, isString, autoSaveDelay));
            }
        }

        // A [SetsRequiredMembers] parameterless ctor already satisfies them; adding an
        // initializer there would clobber real values with defaults.
        if (ParameterlessCtorSetsRequiredMembers(vm))
            required.Clear();

        return new ViewModelCandidate(
            fullName,
            vm.ToDisplayString(),
            sanitized,
            props.ToImmutable(),
            required.ToImmutable(),
            skipped.ToImmutable(),
            usedByEditBase,
            usage,
            UnsupportedReason: null);
    }

    /// <summary>
    /// Prefers the property's own declaration; view models that arrive from a referenced
    /// assembly have no source location, so the page declaration is used instead.
    /// </summary>
    private static LocationInfo? LocationOf(IPropertySymbol prop, LocationInfo? fallback)
    {
        foreach (var location in prop.Locations)
        {
            if (location.IsInSource)
                return LocationInfo.From(location);
        }
        return fallback;
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

    /// <summary>
    /// Escapes a member name that happens to be a C# keyword. Such names cannot be declared in
    /// C# but can arrive from a referenced assembly written in another language.
    /// </summary>
    private static string Escape(string identifier) =>
        SyntaxFacts.GetKeywordKind(identifier) != SyntaxKind.None ? "@" + identifier : identifier;

    /// <summary>
    /// Null-safe projection to a Roslyn location. A view model that arrives from a referenced
    /// assembly has no source location at all, and a diagnostic without one is still a
    /// diagnostic — it just lands on the project instead of on a line.
    /// </summary>
    private static Location AsLocation(LocationInfo? info) => info?.ToLocation() ?? Location.None;

    private static void Emit(
        SourceProductionContext spc,
        ImmutableArray<ViewModelCandidate> candidates,
        LanguageVersion languageVersion,
        string assemblyName)
    {
        if (candidates.IsDefaultOrEmpty)
            return;

        // Dedup by full name — a VM can be reached by multiple derived pages. If ANY of those
        // pages is a PageEditBase, the write path is live for that view model.
        var unique = new Dictionary<string, ViewModelCandidate>(StringComparer.Ordinal);
        foreach (var c in candidates)
        {
            if (!unique.TryGetValue(c.FullyQualifiedName, out var existing))
            {
                unique[c.FullyQualifiedName] = c;
            }
            else if (c.UsedByEditBase && !existing.UsedByEditBase)
            {
                unique[c.FullyQualifiedName] = existing.AsUsedByEditBase();
            }
        }

        // Stable order so the generated file is deterministic across builds.
        var ordered = unique.Values
            .OrderBy(c => c.FullyQualifiedName, StringComparer.Ordinal)
            .ToList();

        // LangVersion is a whole-compilation property: if the floor is not met nothing can be
        // emitted at all, and the per-property notes below would be misleading (with no metadata
        // registered every page uses the reflective path, which handles all these shapes).
        if (languageVersion != LanguageVersion.Default && languageVersion < MinimumLanguageVersion)
        {
            spc.ReportDiagnostic(Diagnostic.Create(
                LanguageVersionTooLow,
                AsLocation(ordered[0].Usage),
                MinimumLanguageVersion.ToDisplayString(),
                languageVersion.ToDisplayString()));
            return;
        }

        foreach (var c in ordered)
            ReportCandidateDiagnostics(spc, c);

        var supported = ordered.Where(c => c.UnsupportedReason is null).ToList();
        if (supported.Count == 0)
            return;

        var holder = "__ZonitViewModelRegistrations_" + SanitizeIdentifier(assemblyName);

        var sb = new StringBuilder(4096);
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("#pragma warning disable CS0618 // obsolete members are still generated for completeness");
        sb.AppendLine();
        // Block-scoped namespace and no `file` types: this file is compiled by the CONSUMER,
        // under the LangVersion THEY pinned. See the class remarks.
        sb.AppendLine("namespace Zonit.Extensions.Website.Generated");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Auto-registration of AOT-safe ViewModel metadata emitted by");
        sb.AppendLine("    /// <c>Zonit.Extensions.Website.SourceGenerators</c>. Do not edit.");
        sb.AppendLine("    /// </summary>");
        sb.Append("    internal static class ").AppendLine(holder);
        sb.AppendLine("    {");
        sb.AppendLine("        [global::System.Runtime.CompilerServices.ModuleInitializer]");
        sb.AppendLine("        internal static void Register()");
        sb.AppendLine("        {");
        foreach (var c in supported)
        {
            sb.Append("            global::Zonit.Extensions.Website.ViewModelMetadataRegistry.Register<")
              .Append(c.FullyQualifiedName)
              .Append(">(new __ZonitVMMetadata_").Append(c.SanitizedIdentifier).AppendLine("());");
        }
        sb.AppendLine("        }");

        foreach (var c in supported)
        {
            sb.AppendLine();
            EmitMetadataClass(sb, c);
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        spc.AddSource("ZonitViewModelMetadata.g.cs", sb.ToString());
    }

    private static void ReportCandidateDiagnostics(SourceProductionContext spc, ViewModelCandidate c)
    {
        // Nothing in the framework reads this metadata for a PageViewBase-only view model —
        // PageEditBase<T> is the sole consumer (see the five ViewModelMetadata<T>.Instance call
        // sites in Components/PageEditBase.cs). So a gap that only such a view model can hit is
        // reported at Info, and a gap that changes what an edit page actually does is a Warning.
        if (c.UnsupportedReason is not null)
        {
            if (c.UsedByEditBase)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    NoMetadataGenerated, AsLocation(c.Usage), c.DisplayName, c.UnsupportedReason));
            }
            return;
        }

        if (!c.RequiredMembers.IsEmpty)
        {
            // Info, not Warning: a type with required members cannot satisfy PageEditBase's
            // new() constraint (CS9040), so this can only ever be a view-only view model and
            // no framework code path observes the defaulted instance.
            spc.ReportDiagnostic(Diagnostic.Create(
                RequiredMembersDefaulted, AsLocation(c.Usage),
                c.DisplayName, string.Join(", ", c.RequiredMembers)));
        }

        if (!c.UsedByEditBase)
            return;

        foreach (var s in c.SkippedProperties)
        {
            spc.ReportDiagnostic(Diagnostic.Create(
                PropertyNotWritable, AsLocation(s.Location), s.Name, c.DisplayName, s.Reason));
        }
    }

    private static void EmitMetadataClass(StringBuilder sb, ViewModelCandidate c)
    {
        sb.Append("        private sealed class __ZonitVMMetadata_").Append(c.SanitizedIdentifier)
          .Append(" : global::Zonit.Extensions.Website.ViewModelMetadata<")
          .Append(c.FullyQualifiedName).AppendLine(">");
        sb.AppendLine("        {");

        // StringProperties
        sb.Append("            private static readonly global::System.Collections.Generic.IReadOnlyList<")
          .Append("global::Zonit.Extensions.Website.StringPropertyAccessor<").Append(c.FullyQualifiedName).Append(">> _stringProps = new ")
          .Append("global::Zonit.Extensions.Website.StringPropertyAccessor<").Append(c.FullyQualifiedName).AppendLine(">[]");
        sb.AppendLine("            {");
        foreach (var p in c.Properties)
        {
            if (!p.IsString) continue;
            var member = Escape(p.Name);
            sb.Append("                new(\"").Append(p.Name).Append("\", static vm => vm.").Append(member)
              .Append(", static (vm, v) => vm.").Append(member).AppendLine(" = v!),");
        }
        sb.AppendLine("            };");
        sb.AppendLine();

        // Properties dictionary
        sb.Append("            private static readonly global::System.Collections.Generic.IReadOnlyDictionary<string, ")
          .Append("global::Zonit.Extensions.Website.PropertyAccessor<").Append(c.FullyQualifiedName).Append(">> _props = new ")
          .Append("global::System.Collections.Generic.Dictionary<string, global::Zonit.Extensions.Website.PropertyAccessor<")
          .Append(c.FullyQualifiedName).AppendLine(">>");
        sb.AppendLine("            {");
        foreach (var p in c.Properties)
        {
            var member = Escape(p.Name);
            sb.Append("                [\"").Append(p.Name).Append("\"] = new(");
            sb.Append("\"").Append(p.Name).Append("\", ");
            sb.Append("typeof(").Append(p.TypeFullName).Append("), ");
            sb.Append("static vm => (object?)vm.").Append(member).Append(", ");
            sb.Append("static (vm, v) => vm.").Append(member).Append(" = (").Append(p.TypeFullName).Append(")v!");
            if (p.AutoSaveDelayMs is int delay)
            {
                sb.Append(", new global::Zonit.Extensions.Website.AutoSaveAttribute(").Append(delay).Append(")");
            }
            sb.AppendLine("),");
        }
        sb.AppendLine("            };");
        sb.AppendLine();

        sb.Append("            public override global::System.Collections.Generic.IReadOnlyList<")
          .Append("global::Zonit.Extensions.Website.StringPropertyAccessor<").Append(c.FullyQualifiedName)
          .AppendLine(">> StringProperties => _stringProps;");
        sb.AppendLine();
        sb.Append("            public override global::System.Collections.Generic.IReadOnlyDictionary<string, ")
          .Append("global::Zonit.Extensions.Website.PropertyAccessor<").Append(c.FullyQualifiedName)
          .AppendLine(">> Properties => _props;");
        sb.AppendLine();

        if (!c.RequiredMembers.IsEmpty)
        {
            // Restate ZONITVM0003 where a reader of the emitted file will see it: an info-severity
            // generator diagnostic does not show up in a plain `dotnet build` console log.
            sb.AppendLine("            // Required members are initialised with 'default': CreateInstance() takes no");
            sb.AppendLine("            // arguments, so there is no value to pass. `new T()` alone would not compile (CS9035).");
        }
        sb.Append("            public override ").Append(c.FullyQualifiedName).Append(" CreateInstance() => new ")
          .Append(c.FullyQualifiedName).Append("()");
        if (!c.RequiredMembers.IsEmpty)
        {
            sb.Append(" { ");
            for (var i = 0; i < c.RequiredMembers.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(Escape(c.RequiredMembers[i])).Append(" = default!");
            }
            sb.Append(" }");
        }
        sb.AppendLine(";");

        sb.AppendLine("        }");
    }

    // ---- records ----

    /// <summary>
    /// A view model reached from a page declaration. Equality is structural over the arrays so
    /// the incremental pipeline can actually cache — <c>ImmutableArray&lt;T&gt;</c> compares by
    /// reference, which would make every keystroke a cache miss.
    /// </summary>
    private sealed record ViewModelCandidate(
        string FullyQualifiedName,
        string DisplayName,
        string SanitizedIdentifier,
        ImmutableArray<PropertyModel> Properties,
        ImmutableArray<string> RequiredMembers,
        ImmutableArray<SkippedProperty> SkippedProperties,
        bool UsedByEditBase,
        LocationInfo? Usage,
        string? UnsupportedReason)
    {
        internal ViewModelCandidate AsUsedByEditBase() => this with { UsedByEditBase = true };

        public bool Equals(ViewModelCandidate? other) =>
            other is not null
            && FullyQualifiedName == other.FullyQualifiedName
            && SanitizedIdentifier == other.SanitizedIdentifier
            && UsedByEditBase == other.UsedByEditBase
            && UnsupportedReason == other.UnsupportedReason
            && Equals(Usage, other.Usage)
            && Properties.SequenceEqual(other.Properties)
            && RequiredMembers.SequenceEqual(other.RequiredMembers)
            && SkippedProperties.SequenceEqual(other.SkippedProperties);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = FullyQualifiedName.GetHashCode();
                hash = (hash * 397) ^ UsedByEditBase.GetHashCode();
                hash = (hash * 397) ^ (UnsupportedReason?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ Properties.Length;
                hash = (hash * 397) ^ RequiredMembers.Length;
                hash = (hash * 397) ^ SkippedProperties.Length;
                return hash;
            }
        }
    }

    private sealed record PropertyModel(
        string Name,
        string TypeFullName,
        bool IsString,
        int? AutoSaveDelayMs);

    private sealed record SkippedProperty(
        string Name,
        string Reason,
        LocationInfo? Location);

    /// <summary>
    /// Value-equatable stand-in for <see cref="Location"/>. A <c>Location</c> holds onto the
    /// <c>SyntaxTree</c> it came from, which is a new object after every edit; caching it would
    /// defeat the incremental pipeline.
    /// </summary>
    private sealed record LocationInfo(string FilePath, TextSpan TextSpan, LinePositionSpan LineSpan)
    {
        internal static LocationInfo? From(Location? location)
        {
            if (location is null || !location.IsInSource || location.SourceTree is null)
                return null;

            return new LocationInfo(location.SourceTree.FilePath, location.SourceSpan, location.GetLineSpan().Span);
        }

        internal Location ToLocation() => Location.Create(FilePath, TextSpan, LineSpan);
    }
}
