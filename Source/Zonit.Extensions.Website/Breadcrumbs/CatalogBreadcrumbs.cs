namespace Zonit.Extensions.Website;

public class CatalogBreadcrumbs : BreadcrumbsModel
{
    public CatalogBreadcrumbs() : base(new Title("Catalog"), new Url("Catalog", allowRelative: true))
    {
        Template = "catalog";
    }
}
