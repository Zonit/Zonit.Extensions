using System.Collections.Immutable;

namespace Zonit.Extensions.Projects.Services;

/// <summary>
/// Maps the legacy <see cref="ProjectModel"/> stored in <see cref="ICatalogManager"/> to
/// the public <see cref="Project"/> value object exposed via <see cref="ICatalogProvider"/>.
/// </summary>
internal sealed class CatalogService : ICatalogProvider, IDisposable
{
    private readonly ICatalogManager _manager;

    public Project Project
    {
        get
        {
            var model = _manager.Catalog?.Project;
            return model is null
                ? Project.Empty
                : new Project(model.Id, new Title(model.Name));
        }
    }

    public ImmutableArray<Project> Visible
    {
        get
        {
            var src = _manager.Projects;
            if (src is null || src.Count == 0)
                return ImmutableArray<Project>.Empty;

            var builder = ImmutableArray.CreateBuilder<Project>(src.Count);
            foreach (var p in src)
                builder.Add(new Project(p.Id, new Title(p.Name)));
            return builder.ToImmutable();
        }
    }

    public event Action? OnChange;

    public CatalogService(ICatalogManager manager)
    {
        _manager = manager;
        _manager.OnChange += HandleStateChanged;
    }

    private void HandleStateChanged() => OnChange?.Invoke();

    public void Dispose() => _manager.OnChange -= HandleStateChanged;
}
