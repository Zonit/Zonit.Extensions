using Microsoft.AspNetCore.Components;
using Zonit.Extensions.Website;

namespace Zonit.Extensions.Website;

public abstract class PageBase : ExtensionsBase
{
    [Inject]
    protected IBreadcrumbsProvider BreadcrumbsProvider { get; set; } = default!;

    [Inject]
    protected NavigationManager Navigation { get; set; } = default!;

    protected virtual List<BreadcrumbsModel>? Breadcrumbs { get; }

    protected override void OnInitialized()
    {
        base.OnInitialized();

        BreadcrumbsProvider.Initialize(Breadcrumbs);
    }
}