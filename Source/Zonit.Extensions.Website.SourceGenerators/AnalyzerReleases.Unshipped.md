; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
ZONITVM0001 | Zonit.Extensions.Website | Warning | A view-model property has an init-only or non-public setter, so ViewModelMetadataGenerator cannot emit a setter delegate for it. Only reported when the view model is used with PageEditBase&lt;T&gt;, the one type that writes through the metadata.
ZONITVM0002 | Zonit.Extensions.Website | Warning | No metadata class could be emitted for a view model (abstract, generic, inaccessible, or no public parameterless constructor); the page falls back to reflection. Only reported for view models reached through PageEditBase&lt;T&gt;.
ZONITVM0003 | Zonit.Extensions.Website | Info | A view model has required members; the generated CreateInstance() satisfies them with an object initializer assigning default values.
ZONITVM0004 | Zonit.Extensions.Website | Warning | The consuming project pins LangVersion below C# 9, which the emitted [ModuleInitializer] needs, so nothing was generated.
ZONITSM0001 | Zonit.Sitemap | Warning | A page declares [Sitemap] but its route has a parameter. A route template is not a URL, so it cannot be written into sitemap.xml; enumerate the real URLs with an ISitemapSource.
ZONITSM0002 | Zonit.Sitemap | Warning | A page declares [Sitemap] or [Llms] but no route was found next to it. Add an @page directive or state the path with [Sitemap("/path")].
