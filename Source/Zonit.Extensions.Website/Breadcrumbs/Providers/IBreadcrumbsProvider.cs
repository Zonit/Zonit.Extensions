namespace Zonit.Extensions.Website;

public interface IBreadcrumbsProvider
{
    /// <summary>
    /// Adds a new breadcrumb.
    /// </summary>
    /// <param name="model">The breadcrumb to add.</param>
    public void Initialize(IList<BreadcrumbsModel>? model);
    /// <summary>
    /// Gets the breadcrumbs for the current page.
    /// </summary>
    /// <returns>The breadcrumbs for the current page.</returns>
    public IReadOnlyList<BreadcrumbsModel>? Get();

    public event Action? OnChange;
}
