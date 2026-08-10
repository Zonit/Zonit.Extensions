using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Zonit.Extensions.Website.SourceGenerators;

/// <summary>
/// Collects every page declaring <c>[WebsiteSitemap]</c> or <c>[WebsiteLlms]</c> and emits a module initializer
/// that hands them to <c>StaticPageRegistry</c>.
/// </summary>
/// <remarks>
/// <para><b>Why build time.</b> The set of static pages in an assembly is fixed the moment it
/// compiles. Rediscovering it at start-up means scanning types and reading attributes through
/// reflection — work that produces the same answer every run, costs start-up time, and is the kind
/// of thing trimming cannot see through. Emitting an array literal instead makes the sitemap free
/// at run time and honest under AOT.</para>
///
/// <para><b>Why the <c>.razor</c> text and not the routed type.</b> A source generator cannot see
/// another generator's output, so the <c>[Route]</c> attribute the Razor compiler emits is
/// invisible here — there is no ordering that fixes it, it is how Roslyn is designed. What is
/// visible is the <c>.razor</c> file itself: the Razor SDK adds every one to
/// <c>AdditionalFiles</c>. Both directives being in that one file is what makes this work without
/// the route ever being stated twice.</para>
///
/// <para>C# files are read through the normal syntax provider, for components that declare
/// <c>[Route]</c> in code rather than <c>@page</c> in a template.</para>
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class SitemapPageGenerator : IIncrementalGenerator
{
    private const string SitemapAttribute = "WebsiteSitemap";
    private const string LlmsAttribute = "WebsiteLlms";

    // A Razor directive occupies a whole line: `@page "/ebook"`. Anchored to line start so a
    // literal `@page` inside a code block or a string cannot be mistaken for a route declaration.
    private static readonly Regex PageDirective = new(
        @"^\s*@page\s+""(?<route>[^""]+)""\s*$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    // `@attribute [WebsiteSitemap(...)]` — the argument list is captured whole and parsed separately,
    // because it is ordinary C# and nesting parentheses inside a string would defeat a single
    // regex. Balanced enough for the shapes an attribute can legally take here.
    private static readonly Regex AttributeDirective = new(
        @"^\s*@attribute\s*\[\s*(?<name>WebsiteSitemap|WebsiteLlms)(Attribute)?\s*(?<args>\((?:[^()""]|""[^""]*"")*\))?\s*\]\s*$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly DiagnosticDescriptor ParameterisedRoute = new(
        id: "ZONITSM0001",
        title: "Page with a parameterised route cannot be listed in the sitemap",
        messageFormat: "'{0}' declares [WebsiteSitemap] but its route '{1}' has a parameter. A template is not a URL, so it cannot go into the XML. Enumerate the real URLs with an ISitemapSource, or remove [WebsiteSitemap].",
        category: "Zonit.Sitemap",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MissingRoute = new(
        id: "ZONITSM0002",
        title: "Page declares a sitemap attribute but no route",
        messageFormat: "'{0}' declares [{1}] but no route was found next to it. Add an @page directive, or state the path explicitly with [WebsiteSitemap(\"/path\")].",
        category: "Zonit.Sitemap",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var fromRazor = context.AdditionalTextsProvider
            .Where(static text => text.Path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
            .Select(static (text, token) => ReadRazor(text, token))
            .Where(static result => result is not null)
            .Select(static (result, _) => result!.Value);

        // The second, and in practice the more common, shape: a code-behind partial carrying
        // [Route(Route)] next to [WebsiteSitemap]. Worth handling separately rather than folding into the
        // text parser, because here the route can be a `const string` reference — Roslyn has
        // already evaluated it, so the generator reads the value instead of the expression.
        var fromCSharp = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Zonit.Extensions.Website.WebsiteSitemapAttribute",
                static (node, _) => true,
                static (ctx, _) => ReadSymbol(ctx.TargetSymbol, ctx.Attributes))
            .Where(static result => result is not null)
            .Select(static (result, _) => result!.Value);

        var llmsOnlyFromCSharp = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Zonit.Extensions.Website.WebsiteLlmsAttribute",
                static (node, _) => true,
                static (ctx, _) => ReadSymbol(ctx.TargetSymbol, ctx.Attributes))
            .Where(static result => result is not null)
            .Select(static (result, _) => result!.Value);

        var collected = fromRazor.Collect()
            .Combine(fromCSharp.Collect())
            .Combine(llmsOnlyFromCSharp.Collect())
            .Select(static (triple, _) =>
            {
                var ((razor, csharp), llms) = triple;
                return Merge(razor, csharp, llms);
            });
        var assemblyName = context.CompilationProvider.Select(static (c, _) => c.AssemblyName ?? "Assembly");

        context.RegisterSourceOutput(collected.Combine(assemblyName), static (spc, pair) =>
        {
            var (pages, name) = pair;

            foreach (var page in pages)
            {
                foreach (var diagnostic in page.Diagnostics)
                    spc.ReportDiagnostic(diagnostic);
            }

            var usable = pages.Where(static p => p.Path is not null).ToArray();
            if (usable.Length == 0)
                return;

            spc.AddSource("ZonitStaticPages.g.cs", SourceText.From(Emit(usable, name), Encoding.UTF8));
        });
    }

    /// <summary>
    /// Merges the three inputs, keyed by path. A page declaring <c>[WebsiteSitemap]</c> in code-behind
    /// and <c>[WebsiteLlms]</c> in the template is one page, not two entries.
    /// </summary>
    private static ImmutableArray<PageDeclaration> Merge(
        ImmutableArray<PageDeclaration> razor,
        ImmutableArray<PageDeclaration> csharp,
        ImmutableArray<PageDeclaration> llms)
    {
        var byPath = new Dictionary<string, PageDeclaration>(StringComparer.Ordinal);
        var pathless = new List<PageDeclaration>();

        foreach (var page in razor.Concat(csharp).Concat(llms))
        {
            if (page.Path is null)
            {
                pathless.Add(page);
                continue;
            }

            byPath[page.Path] = byPath.TryGetValue(page.Path, out var existing)
                ? existing.MergeWith(page)
                : page;
        }

        return byPath.Values.Concat(pathless).ToImmutableArray();
    }

    /// <summary>
    /// Reads a code-behind declaration: <c>[Route(...)]</c> for the path, plus whichever of
    /// <c>[WebsiteSitemap]</c> / <c>[WebsiteLlms]</c> are present.
    /// </summary>
    private static PageDeclaration? ReadSymbol(ISymbol symbol, ImmutableArray<AttributeData> _)
    {
        if (symbol is not INamedTypeSymbol type)
            return null;

        var attributes = type.GetAttributes();
        var sitemap = Find(attributes, "WebsiteSitemapAttribute");
        var llms = Find(attributes, "WebsiteLlmsAttribute");

        if (sitemap is null && llms is null)
            return null;

        var diagnostics = new List<Diagnostic>();
        var name = type.ToDisplayString();

        // [WebsiteSitemap("/explicit")] first, then whatever [Route] says. Roslyn hands over the
        // evaluated constant, so `[Route(Route)]` against a `const string` resolves here where a
        // text parser would only ever see the identifier.
        var route = Positional(sitemap) ?? Routes(attributes).FirstOrDefault();

        if (route is null)
        {
            diagnostics.Add(Diagnostic.Create(
                MissingRoute, Locate(type), name, sitemap is not null ? SitemapAttribute : LlmsAttribute));
            return new PageDeclaration(null, false, "Unset", null, null, null, null, diagnostics);
        }

        if (route.IndexOf('{') >= 0)
        {
            diagnostics.Add(Diagnostic.Create(ParameterisedRoute, Locate(type), name, route));
            return new PageDeclaration(null, false, "Unset", null, null, null, null, diagnostics);
        }

        return new PageDeclaration(
            route,
            sitemap is not null,
            Named(sitemap, "Change") is { } change ? change : "Unset",
            Named(sitemap, "Priority"),
            Positional(llms),
            Named(llms, "Title"),
            Named(llms, "Section"),
            diagnostics);
    }

    private static AttributeData? Find(ImmutableArray<AttributeData> attributes, string name)
        => attributes.FirstOrDefault(a => a.AttributeClass?.Name == name);

    private static IEnumerable<string> Routes(ImmutableArray<AttributeData> attributes)
        => attributes
            .Where(static a => a.AttributeClass?.Name == "RouteAttribute")
            .Select(static a => a.ConstructorArguments.FirstOrDefault().Value as string)
            .Where(static v => !string.IsNullOrEmpty(v))
            .Select(static v => v!);

    private static string? Positional(AttributeData? attribute)
        => attribute?.ConstructorArguments.FirstOrDefault().Value as string;

    private static string? Named(AttributeData? attribute, string name)
    {
        if (attribute is null)
            return null;

        foreach (var pair in attribute.NamedArguments)
        {
            if (pair.Key != name || pair.Value.Value is null)
                continue;

            // Change is an enum: the constant is the underlying int, and the member name is what
            // the emitted code has to say.
            if (name == "Change" && pair.Value.Type is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType)
            {
                var value = Convert.ToInt64(pair.Value.Value, CultureInfo.InvariantCulture);
                foreach (var member in enumType.GetMembers().OfType<IFieldSymbol>())
                {
                    if (member.HasConstantValue &&
                        Convert.ToInt64(member.ConstantValue, CultureInfo.InvariantCulture) == value)
                        return member.Name;
                }

                return "Unset";
            }

            return Convert.ToString(pair.Value.Value, CultureInfo.InvariantCulture);
        }

        return null;
    }

    private static Location Locate(ISymbol symbol)
        => symbol.Locations.FirstOrDefault(static l => l.IsInSource) ?? Location.None;

    private static PageDeclaration? ReadRazor(AdditionalText text, System.Threading.CancellationToken token)
    {
        var source = text.GetText(token)?.ToString();
        if (string.IsNullOrEmpty(source))
            return null;

        // Cheapest possible rejection first: the overwhelming majority of .razor files declare
        // neither attribute, and this generator runs on every keystroke in the IDE.
        if (source!.IndexOf("@attribute", StringComparison.Ordinal) < 0)
            return null;

        var attributes = AttributeDirective.Matches(source);
        if (attributes.Count == 0)
            return null;

        string? sitemapArgs = null;
        string? llmsArgs = null;
        var hasSitemap = false;
        var hasLlms = false;

        foreach (Match match in attributes)
        {
            var args = match.Groups["args"].Success ? match.Groups["args"].Value : "()";
            if (match.Groups["name"].Value == SitemapAttribute)
            {
                hasSitemap = true;
                sitemapArgs = args;
            }
            else
            {
                hasLlms = true;
                llmsArgs = args;
            }
        }

        if (!hasSitemap && !hasLlms)
            return null;

        var file = System.IO.Path.GetFileName(text.Path);
        var diagnostics = new List<Diagnostic>();

        // An explicit path in [WebsiteSitemap("...")] wins — it exists precisely for the cases the
        // directive cannot cover.
        var explicitPath = FirstStringArgument(sitemapArgs);
        var route = explicitPath ?? FirstRoute(source);

        if (route is null)
        {
            diagnostics.Add(Diagnostic.Create(
                MissingRoute, Location.None, file, hasSitemap ? SitemapAttribute : LlmsAttribute));
            return new PageDeclaration(null, false, "Unset", null, null, null, null, diagnostics);
        }

        if (route.IndexOf('{') >= 0)
        {
            // The whole reason opt-in beats opt-out: a template is not a URL, and here that is a
            // named, actionable build warning instead of a template silently reaching the XML.
            diagnostics.Add(Diagnostic.Create(ParameterisedRoute, Location.None, file, route));
            return new PageDeclaration(null, false, "Unset", null, null, null, null, diagnostics);
        }

        return new PageDeclaration(
            route,
            hasSitemap,
            NamedArgument(sitemapArgs, "Change") is { } change ? StripEnum(change) : "Unset",
            NamedArgument(sitemapArgs, "Priority"),
            hasLlms ? FirstStringArgument(llmsArgs) : null,
            NamedString(llmsArgs, "Title"),
            NamedString(llmsArgs, "Section"),
            diagnostics);
    }

    private static string? FirstRoute(string source)
    {
        var match = PageDirective.Match(source);
        return match.Success ? match.Groups["route"].Value : null;
    }

    /// <summary>First positional string literal in an argument list, if any.</summary>
    private static string? FirstStringArgument(string? args)
    {
        if (string.IsNullOrEmpty(args))
            return null;

        var match = Regex.Match(args!, @"\(\s*""(?<value>(?:[^""\\]|\\.)*)""");
        return match.Success ? Unescape(match.Groups["value"].Value) : null;
    }

    private static string? NamedArgument(string? args, string name)
    {
        if (string.IsNullOrEmpty(args))
            return null;

        var match = Regex.Match(args!, name + @"\s*=\s*(?<value>[^,)]+)");
        return match.Success ? match.Groups["value"].Value.Trim() : null;
    }

    private static string? NamedString(string? args, string name)
    {
        if (string.IsNullOrEmpty(args))
            return null;

        var match = Regex.Match(args!, name + @"\s*=\s*""(?<value>(?:[^""\\]|\\.)*)""");
        return match.Success ? Unescape(match.Groups["value"].Value) : null;
    }

    private static string Unescape(string value) => value.Replace("\\\"", "\"").Replace("\\\\", "\\");

    /// <summary>`ChangeFrequency.Monthly` / `Monthly` → `Monthly`.</summary>
    private static string StripEnum(string value)
    {
        var dot = value.LastIndexOf('.');
        return dot >= 0 ? value.Substring(dot + 1).Trim() : value.Trim();
    }

    private static string Emit(IReadOnlyList<PageDeclaration> pages, string assemblyName)
    {
        var holder = "__ZonitStaticPages_" + Sanitize(assemblyName);
        var text = new StringBuilder(1024);

        // C# 9 is the floor: the whole mechanism is [ModuleInitializer]. Nothing newer is used,
        // because this compiles inside the CONSUMER's project under whatever LangVersion they
        // pinned, and anything newer is a build break in code they did not write.
        text.Append("// <auto-generated/>\n")
            .Append("#nullable enable\n")
            .Append("namespace Zonit.Extensions.Website.Generated\n{\n")
            .Append("    internal static class ").Append(holder).Append("\n    {\n")
            .Append("        [global::System.Runtime.CompilerServices.ModuleInitializer]\n")
            .Append("        internal static void Register()\n        {\n")
            .Append("            global::Zonit.Extensions.Website.Sitemaps.StaticPageRegistry.Register(\n")
            .Append("                typeof(").Append(holder).Append(").Assembly,\n")
            .Append("                new global::Zonit.Extensions.Website.Sitemaps.StaticPage[]\n                {\n");

        foreach (var page in pages)
        {
            text.Append("                    new global::Zonit.Extensions.Website.Sitemaps.StaticPage(")
                .Append(Literal(page.Path)).Append(", ")
                .Append(page.InSitemap ? "true" : "false").Append(", ")
                .Append("global::Zonit.Extensions.Website.Sitemaps.ChangeFrequency.").Append(page.Change).Append(", ")
                .Append(Priority(page.Priority)).Append(", ")
                .Append(Literal(page.LlmsDescription)).Append(", ")
                .Append(Literal(page.LlmsTitle)).Append(", ")
                .Append(Literal(page.LlmsSection)).Append("),\n");
        }

        text.Append("                });\n        }\n    }\n}\n");
        return text.ToString();
    }

    private static string Priority(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "null";

        return double.TryParse(raw!.TrimEnd('d', 'D', 'f', 'F', 'm', 'M'),
            NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value.ToString("R", CultureInfo.InvariantCulture) + "d"
            : "null";
    }

    private static string Literal(string? value)
        => value is null ? "null" : "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    private static string Sanitize(string value)
    {
        var buffer = new StringBuilder(value.Length);
        foreach (var c in value)
            buffer.Append(char.IsLetterOrDigit(c) ? c : '_');
        return buffer.ToString();
    }

    private readonly record struct PageDeclaration(
        string? Path,
        bool InSitemap,
        string Change,
        string? Priority,
        string? LlmsDescription,
        string? LlmsTitle,
        string? LlmsSection,
        List<Diagnostic> Diagnostics)
    {
        /// <summary>
        /// Folds a second declaration for the same path into this one. Whoever said something
        /// wins over whoever said nothing; the template and the code-behind are two halves of one
        /// page, not two pages.
        /// </summary>
        internal PageDeclaration MergeWith(PageDeclaration other) => new(
            Path,
            InSitemap || other.InSitemap,
            Change != "Unset" ? Change : other.Change,
            Priority ?? other.Priority,
            LlmsDescription ?? other.LlmsDescription,
            LlmsTitle ?? other.LlmsTitle,
            LlmsSection ?? other.LlmsSection,
            Diagnostics.Concat(other.Diagnostics).ToList());
    }
}
