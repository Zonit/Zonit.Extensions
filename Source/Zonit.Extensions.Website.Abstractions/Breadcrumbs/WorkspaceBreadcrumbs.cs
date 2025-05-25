using Zonit.Extensions.Website;

namespace Zonit.Extensions.Website;

// FIXME: Kiedyś przenieś to do paczki Organizacji, tam powinno być w abstrakcji
public class WorkspaceBreadcrumbs : BreadcrumbsModel
{
    public WorkspaceBreadcrumbs() : base("Workspace", "Workspace")
    {
        Template = "workspace";
    }
}