; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
ZONITTS0001 | Zonit.Extensions.Tenants | Warning | Two Setting&lt;T&gt; types in one namespace reduce to the same accessor name (the name with a trailing "Setting" stripped), so only one accessor is generated. Emitting both was CS0111 / CS0102 inside generated code.
ZONITTS0002 | Zonit.Extensions.Tenants | Warning | A Setting&lt;T&gt; reduces to an accessor name TenantSettings already defines, so no accessor is generated. Emitting it was CS0102 inside the package, and an unreachable extension method in a consumer assembly.
