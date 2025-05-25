namespace Zonit.Extensions.Website.Breadcrumbs.Services;

internal class BreadcrumbsService : IBreadcrumbsProvider
{
    IList<BreadcrumbsModel>? _breadcrumbsModel = [];

    public event Action? OnChange;

    public IReadOnlyList<BreadcrumbsModel>? Get()
        => _breadcrumbsModel?.ToList();

    public void Initialize(IList<BreadcrumbsModel>? breadcrumbs)
    {
        _breadcrumbsModel = breadcrumbs;
        StateChanged();
    }

    public void StateChanged() => OnChange?.Invoke();
}