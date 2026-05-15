using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Zonit.Extensions.Tenants.SourceGenerators;

/// <summary>
/// Emits a <c>partial class TenantSettings</c> with one strongly-typed property per
/// <c>Setting&lt;TModel&gt;</c> discovered at compile time. Plugins that ship their
/// own <c>Setting&lt;T&gt;</c> get a property the moment the consuming assembly is
/// rebuilt — no hand-editing of <c>TenantSettings</c> required.
/// </summary>
/// <remarks>
/// <para><b>Discovery scope.</b> The generator scans the <i>current</i> compilation's
/// <c>SyntaxTrees</c> only. Built-in settings ship inside <c>Zonit.Extensions.Tenants</c>
/// and are picked up when that assembly is compiled; plugin settings are picked up
/// when the plugin assembly is compiled. Each assembly therefore emits its own
/// contribution — but because <c>TenantSettings</c> is declared <c>partial</c> in the
/// core package and consumed by reference, the host application's compilation only
/// sees the core's contribution. Plugins shipping <c>Setting&lt;T&gt;</c> need to
/// expose their own facade or use <c>ITenantProvider.GetSetting&lt;TSetting&gt;()</c>.</para>
///
/// <para><b>Property naming.</b> The generator strips a trailing <c>"Setting"</c> suffix
/// (so <c>SiteSetting</c> → <c>Site</c>, <c>ThemeSetting</c> → <c>Theme</c>). When the
/// type doesn't end in <c>"Setting"</c> the full type name is used verbatim.</para>
/// </remarks>
[Generator]
public sealed class TenantSettingsGenerator : IIncrementalGenerator
{
    private const string SettingBaseFullName = "Zonit.Extensions.Tenants.Settings.Setting`1";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Pick up every `class X : Setting<...>` declaration; the heavy semantic
        // analysis happens later in `Transform`.
        var candidates = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsCandidateSettingClass(node),
                transform: static (ctx, _) => Transform(ctx))
            .Where(static info => info is not null);

        var collected = candidates.Collect();

        context.RegisterSourceOutput(collected, static (spc, infos) =>
        {
            var valid = infos.Where(i => i is not null).Cast<SettingInfo>().ToList();
            if (valid.Count == 0) return;

            // Stable order so the generated file is deterministic across builds.
            valid.Sort((a, b) => string.CompareOrdinal(a.PropertyName, b.PropertyName));

            spc.AddSource("TenantSettings.g.cs", Emit(valid));
        });
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

        return new SettingInfo(
            ClassFullName: symbol.ToDisplayString(),
            ModelFullName: modelNamed.ToDisplayString(),
            PropertyName: PropertyName(symbol.Name));
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
        => className.EndsWith("Setting", System.StringComparison.Ordinal)
            ? className.Substring(0, className.Length - "Setting".Length)
            : className;

    private static string Emit(IReadOnlyList<SettingInfo> settings)
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
            var field = $"_{char.ToLowerInvariant(s.PropertyName[0])}{s.PropertyName.Substring(1)}";
            sb.AppendLine($"    private global::{s.ModelFullName}? {field};");
        }

        sb.AppendLine();

        foreach (var s in settings)
        {
            var field = $"_{char.ToLowerInvariant(s.PropertyName[0])}{s.PropertyName.Substring(1)}";
            sb.AppendLine($"    /// <summary>Accessor for <see cref=\"global::{s.ClassFullName}\"/>.</summary>");
            sb.AppendLine(
                $"    public global::{s.ModelFullName} {s.PropertyName} => {field} ??= " +
                $"Provider.GetSetting<global::{s.ClassFullName}>().Value;");
            sb.AppendLine();
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private sealed record SettingInfo(string ClassFullName, string ModelFullName, string PropertyName);
}
