using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Zonit.Extensions.Tenants.SourceGenerators;

/// <summary>
/// Emits strongly-typed access to every <c>Setting&lt;TModel&gt;</c> discovered at compile
/// time, so a plugin that ships its own <c>Setting&lt;T&gt;</c> gets a first-class accessor
/// the moment its assembly is rebuilt — no hand-editing of <c>TenantSettings</c> required.
/// </summary>
/// <remarks>
/// <para><b>Two shapes, because a partial class cannot span assemblies.</b> The hand-written
/// half of <c>TenantSettings</c> (the half that owns <c>protected ITenantProvider Provider</c>)
/// lives in <c>Zonit.Extensions.Tenants</c>. Emitting <c>partial class TenantSettings</c> into
/// any <i>other</i> compilation therefore does not extend that type — it declares a brand-new
/// one that has no <c>Provider</c> (CS0103) and shadows the referenced type at every use site
/// (CS0436). 10.0.0-preview.9 shipped exactly that and broke every consumer that declared a
/// <c>Setting&lt;T&gt;</c>. So:</para>
/// <list type="bullet">
///   <item><b>Inside <c>Zonit.Extensions.Tenants</c></b> (detected by the compilation itself
///         declaring <c>Zonit.Extensions.Tenants.TenantSettings</c>) the generator emits the
///         partial half — <c>settings.Site</c>, <c>settings.Theme</c>, … stay properties.</item>
///   <item><b>In every other compilation</b> it emits an extension-method façade in the
///         setting's own namespace: <c>settings.MyPlugin()</c>. Extension <i>methods</i> rather
///         than C# 14 extension <i>properties</i> so the emitted code compiles under any
///         <c>LangVersion</c> a consumer may have pinned.</item>
/// </list>
///
/// <para><b>Discovery scope.</b> The generator scans the <i>current</i> compilation's
/// <c>SyntaxTrees</c> only — settings that arrive through a referenced assembly already
/// carry that assembly's own façade.</para>
///
/// <para><b>Accessor naming.</b> A trailing <c>"Setting"</c> suffix is stripped
/// (<c>SiteSetting</c> → <c>Site</c>, <c>ThemeSetting</c> → <c>Theme</c>). When stripping would
/// leave nothing, or the type doesn't end in <c>"Setting"</c>, the full type name is used.
/// Because that transform is not injective, two settings can land on the same accessor
/// (<c>FooSetting</c> and <c>Foo</c> both → <c>Foo</c>) and an accessor can land on a name
/// <c>TenantSettings</c> already uses (<c>ToStringSetting</c> → <c>ToString</c>). The first
/// used to reach the consumer as a raw CS0111 inside generated code they cannot edit; the
/// second as an extension method that silently lost member lookup to the instance member.
/// Both are now resolved here and reported as <c>ZONITTS0001</c> / <c>ZONITTS0002</c>.</para>
/// </remarks>
[Generator]
public sealed class TenantSettingsGenerator : IIncrementalGenerator
{
    /// <summary>Metadata name of the hand-written partial half, used to decide which shape to emit.</summary>
    private const string TenantSettingsMetadataName = "Zonit.Extensions.Tenants.TenantSettings";

    private const string Category = "Zonit.Extensions.Tenants";

    /// <summary>
    /// Two settings reduce to the same accessor. Warning rather than error: one accessor is
    /// still emitted and works, so the build stays green — but the diagnostic has to name both
    /// types and say which one won, because otherwise <c>settings.Foo()</c> silently resolves
    /// to the other type's model.
    /// </summary>
    /// <remarks>The <c>ZONITTS####</c> block belongs to this generator, mirroring the
    /// <c>ZONITVM####</c> block the Website view-model generator uses. Every rule is listed in
    /// <c>AnalyzerReleases.Unshipped.md</c>, which is also the catalogue a consumer needs to
    /// <c>&lt;NoWarn&gt;</c> one by id.</remarks>
    private static readonly DiagnosticDescriptor AccessorCollision = new(
        id: "ZONITTS0001",
        title: "Two tenant settings map to the same accessor name",
        messageFormat: "Tenant settings '{1}' and '{2}' both map to the accessor '{0}'. Only '{1}' gets a generated accessor. Read the other one with settings.Get<{2}>(), or rename it so the names stop colliding.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The accessor name is the setting's type name with a trailing \"Setting\" suffix removed, so 'FooSetting' and 'Foo' collapse onto the same name. Emitting both would produce two members with identical signatures (CS0111 / CS0102) inside generated code, so only one is emitted.");

    /// <summary>
    /// The accessor would collide with a member <c>TenantSettings</c> already has, so it is
    /// skipped. Emitting it would be CS0102 in the core shape when <c>TenantSettings</c> itself
    /// declares the name, CS0108 ("hides inherited member") when the name comes from a base
    /// type, and — worst — silence in the façade shape, where the instance member wins member
    /// lookup and the extension method becomes unreachable code.
    /// </summary>
    private static readonly DiagnosticDescriptor ReservedAccessorName = new(
        id: "ZONITTS0002",
        title: "Tenant setting accessor collides with an existing TenantSettings member",
        messageFormat: "Tenant setting '{1}' maps to the accessor '{0}', which TenantSettings already defines. No accessor is generated. Read it with settings.Get<{1}>(), or rename the setting.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "TenantSettings declares Provider and Get<TSetting>() and inherits object's members. An accessor with one of those names is a duplicate-member or member-hiding diagnostic inside the package, and unreachable code in a consumer assembly because an instance member always beats an extension method.");

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Pick up every `class X : Setting<...>` declaration; the heavy semantic
        // analysis happens later in `Transform`.
        var candidates = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsCandidateSettingClass(node),
                transform: static (ctx, _) => Transform(ctx))
            .Where(static info => info is not null);

        // Project the compilation down to two value-equal fields before combining. The
        // compilation object changes on every keystroke; a bool and a joined name list almost
        // never do, so downstream output stays cached instead of re-running on every edit.
        var surface = context.CompilationProvider
            .Select(static (compilation, _) => TenantSettingsSurface.From(compilation));

        var collected = candidates.Collect().Combine(surface);

        context.RegisterSourceOutput(collected, static (spc, input) =>
        {
            var (infos, surface) = input;

            var valid = infos.Where(i => i is not null).Cast<SettingInfo>().ToList();
            if (valid.Count == 0) return;

            // Stable order so the generated file is deterministic across builds. The tie-break
            // on ClassFullName matters: List<T>.Sort is not stable, so comparing PropertyName
            // alone left the winner of a collision up to the sort's internal pivot choices.
            valid.Sort(static (a, b) =>
            {
                var byName = string.CompareOrdinal(a.PropertyName, b.PropertyName);
                return byName != 0 ? byName : string.CompareOrdinal(a.ClassFullName, b.ClassFullName);
            });

            var reserved = surface.ReservedNameSet();

            if (surface.OwnsPartial)
            {
                // One partial class holds every accessor, so uniqueness is compilation-wide.
                var settings = Resolve(valid, reserved, spc);
                if (settings.Count > 0)
                    spc.AddSource("TenantSettings.g.cs", EmitCorePartial(settings));
                return;
            }

            // One façade per namespace: a `using MyPlugin;` — which the consumer already has
            // wherever the setting type is visible — is then enough to bring the accessors
            // into scope, and two plugins in different namespaces never collide. Uniqueness is
            // therefore per namespace, because each namespace gets its own static class.
            foreach (var group in valid.GroupBy(s => s.Namespace).OrderBy(g => g.Key, System.StringComparer.Ordinal))
            {
                var settings = Resolve(group.ToList(), reserved, spc);
                if (settings.Count == 0) continue;

                var hint = group.Key.Length == 0
                    ? "TenantSettingsExtensions.g.cs"
                    : "TenantSettingsExtensions." + group.Key + ".g.cs";

                spc.AddSource(hint, EmitConsumerFacade(group.Key, settings));
            }
        });
    }

    /// <summary>
    /// Reduces one emission scope (the whole compilation for the core partial, one namespace
    /// for a façade) to a set of settings whose accessor names are unique and legal, reporting
    /// everything it drops.
    /// </summary>
    /// <remarks>
    /// <para>Three distinct inputs land here:</para>
    /// <list type="number">
    ///   <item><b>Repeated declarations of the same type.</b> A <c>partial class</c> whose parts
    ///         both carry a base list produces one <see cref="SettingInfo"/> per part. Same
    ///         symbol, same accessor, same model — collapsing them loses nothing, so it is done
    ///         silently.</item>
    ///   <item><b>Two different types on one accessor.</b> Reported as
    ///         <see cref="AccessorCollision"/>; the winner is the type that needed no suffix
    ///         stripping (its name <i>is</i> the accessor), else the ordinally-first type name.
    ///         Deterministic, and it favours the type the consumer most likely meant.</item>
    ///   <item><b>An accessor that <c>TenantSettings</c> already defines.</b> Reported as
    ///         <see cref="ReservedAccessorName"/> and dropped.</item>
    /// </list>
    /// </remarks>
    private static List<SettingInfo> Resolve(
        List<SettingInfo> scope,
        HashSet<string> reserved,
        SourceProductionContext spc)
    {
        // Accessor name → index of the setting currently holding it in `result`, so a later
        // winner can replace an earlier one in place (keeping the emission order stable)
        // without an O(n) search over value-equal records.
        var byAccessor = new Dictionary<string, int>(System.StringComparer.Ordinal);
        var seenTypes = new HashSet<string>(System.StringComparer.Ordinal);
        var result = new List<SettingInfo>(scope.Count);

        foreach (var candidate in scope)
        {
            // (1) Same type seen twice (multi-part partial declaration).
            if (!seenTypes.Add(candidate.ClassFullName))
                continue;

            // (3) Reserved name — checked before the collision map so the diagnostic points at
            // the real cause instead of blaming whichever colliding type happened to be first.
            if (reserved.Contains(candidate.PropertyName))
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    ReservedAccessorName,
                    candidate.Location.ToLocation(),
                    candidate.PropertyName,
                    candidate.ClassFullName));
                continue;
            }

            // (2) Two different types, one accessor.
            if (byAccessor.TryGetValue(candidate.PropertyName, out var index))
            {
                var incumbent = result[index];
                var (winner, loser) = Prefer(incumbent, candidate);

                spc.ReportDiagnostic(Diagnostic.Create(
                    AccessorCollision,
                    loser.Location.ToLocation(),
                    new[] { winner.Location.ToLocation() },
                    loser.PropertyName,
                    winner.ClassFullName,
                    loser.ClassFullName));

                result[index] = winner;
                continue;
            }

            byAccessor.Add(candidate.PropertyName, result.Count);
            result.Add(candidate);
        }

        return result;
    }

    /// <summary>
    /// Picks the surviving setting of a collision: a type whose name needs no suffix stripping
    /// owns the accessor outright, otherwise the ordinally-first full name wins so the choice
    /// does not depend on file order.
    /// </summary>
    private static (SettingInfo Winner, SettingInfo Loser) Prefer(SettingInfo a, SettingInfo b)
    {
        var aExact = a.TypeName == a.PropertyName;
        var bExact = b.TypeName == b.PropertyName;

        if (aExact != bExact)
            return aExact ? (a, b) : (b, a);

        return string.CompareOrdinal(a.ClassFullName, b.ClassFullName) <= 0 ? (a, b) : (b, a);
    }

    private static bool IsCandidateSettingClass(SyntaxNode node)
        => node is ClassDeclarationSyntax c
           && c.BaseList is { Types.Count: > 0 }
           && !c.Modifiers.Any(m => m.Text == "abstract");

    private static SettingInfo? Transform(GeneratorSyntaxContext ctx)
    {
        if (ctx.Node is not ClassDeclarationSyntax classDecl)
            return null;

        if (ctx.SemanticModel.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol symbol)
            return null;

        // Walk the inheritance chain so plugins can derive through intermediate abstract
        // bases (e.g. `MyPluginSetting : MyBaseSetting<T>` where `MyBaseSetting<T> : Setting<T>`).
        var settingBase = WalkToSettingBase(symbol);
        if (settingBase is null) return null;

        var modelType = settingBase.TypeArguments[0];
        if (modelType is not INamedTypeSymbol modelNamed) return null;

        // A façade can only ever call a `new()`-able, publicly reachable setting. Emitting an
        // accessor for a private/protected nested type would produce CS0122 in the consumer.
        if (symbol.DeclaredAccessibility != Accessibility.Public) return null;

        return new SettingInfo(
            ClassFullName: symbol.ToDisplayString(),
            ModelFullName: modelNamed.ToDisplayString(),
            TypeName: symbol.Name,
            PropertyName: PropertyName(symbol.Name),
            Namespace: symbol.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : symbol.ContainingNamespace.ToDisplayString(),
            Location: LocationInfo.From(classDecl.Identifier.GetLocation()));
    }

    private static INamedTypeSymbol? WalkToSettingBase(INamedTypeSymbol symbol)
    {
        var current = symbol.BaseType;
        while (current is not null)
        {
            if (current.IsGenericType
                && current.OriginalDefinition.ToDisplayString() == "Zonit.Extensions.Tenants.Settings.Setting<T>")
            {
                return current;
            }

            // Belt-and-braces: also accept the unbound display when Roslyn formats the
            // open generic differently across versions.
            if (current.OriginalDefinition.MetadataName == "Setting`1"
                && current.OriginalDefinition.ContainingNamespace?.ToDisplayString()
                   == "Zonit.Extensions.Tenants.Settings")
            {
                return current;
            }

            current = current.BaseType;
        }
        return null;
    }

    private static string PropertyName(string className)
    {
        if (!className.EndsWith("Setting", System.StringComparison.Ordinal))
            return className;

        var stripped = className.Substring(0, className.Length - "Setting".Length);

        // A type literally named `Setting` would otherwise strip down to the empty string
        // and produce an unnamed member.
        return stripped.Length == 0 ? className : stripped;
    }

    /// <summary>
    /// Renders an accessor name as a C# identifier. A setting named <c>ClassSetting</c> or
    /// <c>NewSetting</c> reduces to a reserved keyword, which is a syntax error in the emitted
    /// member — <c>@</c>-escaping keeps it compilable and callable (<c>settings.@class</c>).
    /// Contextual keywords (<c>value</c>, <c>record</c>, …) are legal identifiers and are left
    /// alone.
    /// </summary>
    private static string Identifier(string name)
        => SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None ? name : "@" + name;

    /// <summary>
    /// The in-package shape: the other half of the hand-written <c>partial class TenantSettings</c>.
    /// Only ever emitted into the compilation that declares that half.
    /// </summary>
    private static string EmitCorePartial(IReadOnlyList<SettingInfo> settings)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("namespace Zonit.Extensions.Tenants;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>Auto-generated strongly-typed access to discovered Setting&lt;T&gt; instances.</summary>");
        sb.AppendLine("public partial class TenantSettings");
        sb.AppendLine("{");

        foreach (var s in settings)
        {
            var field = FieldName(s.PropertyName);
            sb.AppendLine($"    private global::{s.ModelFullName}? {field};");
        }

        sb.AppendLine();

        foreach (var s in settings)
        {
            var field = FieldName(s.PropertyName);
            sb.AppendLine($"    /// <summary>Accessor for <see cref=\"global::{s.ClassFullName}\"/>.</summary>");
            sb.AppendLine(
                $"    public global::{s.ModelFullName} {Identifier(s.PropertyName)} => {field} ??= " +
                $"Provider.GetSetting<global::{s.ClassFullName}>().Value;");
            sb.AppendLine();
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// The out-of-package shape: extension methods over the referenced <c>TenantSettings</c>.
    /// No state is cached here — <c>ITenantProvider.GetSetting&lt;T&gt;()</c> already caches
    /// per scope and invalidates on tenant change, so a façade-level field would only add a
    /// second cache that can go stale.
    /// </summary>
    private static string EmitConsumerFacade(string ns, IReadOnlyList<SettingInfo> settings)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        if (ns.Length > 0)
        {
            sb.AppendLine($"namespace {ns};");
            sb.AppendLine();
        }

        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Auto-generated accessors for the <c>Setting&lt;T&gt;</c> types declared in this assembly.");
        sb.AppendLine("/// Extension methods rather than <c>TenantSettings</c> members because a partial class");
        sb.AppendLine("/// cannot span assemblies — the type itself lives in Zonit.Extensions.Tenants.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class TenantSettingsExtensions");
        sb.AppendLine("{");

        foreach (var s in settings)
        {
            sb.AppendLine($"    /// <summary>Accessor for <see cref=\"global::{s.ClassFullName}\"/>.</summary>");
            sb.AppendLine(
                $"    public static global::{s.ModelFullName} {Identifier(s.PropertyName)}(" +
                "this global::Zonit.Extensions.Tenants.TenantSettings settings)");
            sb.AppendLine($"        => settings.Get<global::{s.ClassFullName}>().Value;");
            sb.AppendLine();
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string FieldName(string propertyName)
        => $"_{char.ToLowerInvariant(propertyName[0])}{propertyName.Substring(1)}";

    private sealed record SettingInfo(
        string ClassFullName,
        string ModelFullName,
        string TypeName,
        string PropertyName,
        string Namespace,
        LocationInfo Location);

    /// <summary>
    /// Value-equal stand-in for <see cref="Location"/>. A real <see cref="Location"/> holds the
    /// <see cref="SyntaxTree"/> it came from, and that instance is replaced on every keystroke,
    /// so caching a model that contains one defeats the incremental pipeline. Path + spans
    /// compare by value and rebuild the same location for the diagnostic.
    /// </summary>
    private sealed record LocationInfo(string FilePath, TextSpan TextSpan, LinePositionSpan LineSpan)
    {
        public static LocationInfo From(Location location)
        {
            var span = location.GetLineSpan();
            return new LocationInfo(span.Path, location.SourceSpan, span.Span);
        }

        public Location ToLocation() => Location.Create(FilePath, TextSpan, LineSpan);
    }

    /// <summary>
    /// The facts about <c>TenantSettings</c> that emission depends on, projected out of the
    /// <see cref="Compilation"/> into value-equal fields.
    /// </summary>
    /// <param name="OwnsPartial">
    /// True only for the compilation that owns the hand-written <c>TenantSettings</c> half.
    /// Deliberately a symbol check rather than <c>AssemblyName == "Zonit.Extensions.Tenants"</c>:
    /// it stays correct across renames/forks, and it is <see langword="false"/> for the
    /// ambiguous case (type declared in source <i>and</i> pulled in from a reference), which is
    /// precisely the situation the emitted partial used to create.
    /// </param>
    /// <param name="ReservedNames">
    /// Newline-joined member names an accessor must not take. A joined string rather than a
    /// collection because <c>ImmutableArray&lt;string&gt;</c> compares by reference in the
    /// incremental cache, which would re-run every downstream step on every keystroke.
    /// </param>
    private sealed record TenantSettingsSurface(bool OwnsPartial, string ReservedNames)
    {
        private const char Separator = '\n';

        public static TenantSettingsSurface From(Compilation compilation)
        {
            var symbol = compilation.GetTypeByMetadataName(TenantSettingsMetadataName);
            if (symbol is null)
                return new TenantSettingsSurface(false, string.Empty);

            var ownsPartial = SymbolEqualityComparer.Default.Equals(symbol.ContainingAssembly, compilation.Assembly);
            return new TenantSettingsSurface(ownsPartial, CollectReservedNames(symbol, ownsPartial));
        }

        public HashSet<string> ReservedNameSet()
            => new(
                ReservedNames.Length == 0
                    ? System.Array.Empty<string>()
                    : ReservedNames.Split(Separator),
                System.StringComparer.Ordinal);

        /// <summary>
        /// Names that would break, or silently swallow, a generated accessor. The two emission
        /// shapes fail differently, so they reserve different sets.
        /// </summary>
        private static string CollectReservedNames(INamedTypeSymbol symbol, bool ownsPartial)
        {
            var names = new SortedSet<string>(System.StringComparer.Ordinal);

            for (var type = symbol; type is not null; type = type.BaseType)
            {
                var isSelf = SymbolEqualityComparer.Default.Equals(type, symbol);

                foreach (var member in type.GetMembers())
                {
                    if (ownsPartial)
                    {
                        // Emitting a PROPERTY into the same class: a name the class already
                        // declares is CS0102, and an accessible inherited name is CS0108
                        // ("hides inherited member") — a warning inside generated code that
                        // nobody can add `new` to.
                        if (!isSelf && member.DeclaredAccessibility == Accessibility.Private)
                            continue;

                        names.Add(member.Name);
                        continue;
                    }

                    // Emitting an EXTENSION METHOD: it is only ever reached when member lookup
                    // on TenantSettings finds nothing applicable. A public property/field/event
                    // makes `settings.Foo()` a CS1955 ("non-invocable member used like a
                    // method"), and a public parameterless, non-generic method simply wins —
                    // the accessor becomes unreachable code with no error at all.
                    if (member.DeclaredAccessibility != Accessibility.Public)
                        continue;

                    switch (member)
                    {
                        case IPropertySymbol:
                        case IFieldSymbol:
                        case IEventSymbol:
                            names.Add(member.Name);
                            break;

                        case IMethodSymbol { Parameters.Length: 0, TypeParameters.Length: 0, MethodKind: MethodKind.Ordinary }:
                            names.Add(member.Name);
                            break;
                    }
                }
            }

            return string.Join(Separator.ToString(), names);
        }
    }
}
