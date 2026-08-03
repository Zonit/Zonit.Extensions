using System.Collections.Immutable;
using System.Globalization;
using Zonit.Extensions.Text;

namespace Zonit.Extensions.Projects.Services;

/// <summary>
/// Maps the legacy <see cref="ProjectModel"/> stored in <see cref="ICatalogManager"/> to
/// the public <see cref="Extensions.Project"/> value object exposed via <see cref="ICatalogProvider"/>.
/// </summary>
/// <remarks>
/// <para><b>Snapshot, not projection.</b> Both members are read from Razor markup — <c>Visible</c>
/// is explicitly meant to be enumerated inside a <c>@foreach</c> — so they hand back a cached
/// snapshot rebuilt once per <see cref="ICatalogManager.OnChange"/>, mirroring the sibling
/// <c>WorkspaceService</c>. Computing them per read meant a compiled-regex whitespace pass plus a
/// grapheme walk per <see cref="Title"/>, and a whole fresh <see cref="ImmutableArray{T}"/> per
/// read of <see cref="Visible"/>, on the render hot path.</para>
///
/// <para><b>Mapping is total, never throwing.</b> <see cref="ProjectModel"/> is a plain
/// consumer-owned DTO with no validation, while the value objects it feeds are strict
/// (<see cref="Extensions.Project"/> rejects <see cref="Guid.Empty"/>, <see cref="Title"/> rejects
/// blank text and anything over <see cref="Title.MaxLength"/> graphemes). One malformed row must
/// not be able to take down the page that renders the list.</para>
/// </remarks>
internal sealed class CatalogService : ICatalogProvider, IDisposable
{
    private readonly ICatalogManager _manager;
    private Project _project = Project.Empty;
    private ImmutableArray<Project> _visible = ImmutableArray<Project>.Empty;

    public Project Project => _project;

    public ImmutableArray<Project> Visible => _visible;

    public event Action? OnChange;

    public CatalogService(ICatalogManager manager)
    {
        _manager = manager;
        _manager.OnChange += HandleStateChanged;
        Hydrate();
    }

    private void Hydrate()
    {
        var current = _manager.Catalog?.Project;

        // No id means the row is not addressable — SwitchProjectAsync could never reach it and the
        // VO refuses to carry it — so it reads as "no project selected" instead of throwing out of
        // a property that Razor evaluates while rendering.
        _project = current is null || current.Id == Guid.Empty
            ? Project.Empty
            : new Project(current.Id, ToTitle(current.Name));

        var src = _manager.Projects;
        if (src is null || src.Count == 0)
        {
            _visible = ImmutableArray<Project>.Empty;
            return;
        }

        var builder = ImmutableArray.CreateBuilder<Project>(src.Count);
        foreach (var p in src)
        {
            // Skip rather than throw: an un-switchable row is worth losing, the whole list is not.
            if (p is null || p.Id == Guid.Empty)
                continue;

            builder.Add(new Project(p.Id, ToTitle(p.Name)));
        }

        _visible = builder.DrainToImmutable();
    }

    /// <summary>
    /// Non-throwing <see cref="string"/> → <see cref="Title"/> projection for consumer-supplied names.
    /// </summary>
    /// <remarks>
    /// Blank collapses to <see cref="Title.Empty"/>. Over-long text is cut at the grapheme boundary
    /// <see cref="Title"/> itself enforces, because that 60-grapheme cap is a <i>display</i>
    /// constraint: a shortened label still identifies the project in a switcher, an empty one does
    /// not, and neither may cost the user their page.
    /// </remarks>
    private static Title ToTitle(string? name)
    {
        if (Title.TryCreate(name, out var title))
            return title;

        if (string.IsNullOrWhiteSpace(name))
            return Title.Empty;

        // Normalize first so the 60-grapheme budget is spent on characters that survive
        // Title's own normalization, rather than on whitespace it is about to collapse.
        var normalized = name.Trim().NormalizeWhitespace();
        var info = new StringInfo(normalized);
        if (info.LengthInTextElements > Title.MaxLength)
            normalized = info.SubstringByTextElements(0, Title.MaxLength);

        return Title.TryCreate(normalized, out title) ? title : Title.Empty;
    }

    private void HandleStateChanged()
    {
        Hydrate();
        OnChange?.Invoke();
    }

    public void Dispose() => _manager.OnChange -= HandleStateChanged;
}
