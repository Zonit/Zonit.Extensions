using Zonit.Extensions.Website;

namespace Zonit.Extensions.Website;

public class CatalogBreadcrumbs : BreadcrumbsModel
{
    public CatalogBreadcrumbs() : base("Catalog", "Catalog")
    {
        Template = "catalog";
    }
}