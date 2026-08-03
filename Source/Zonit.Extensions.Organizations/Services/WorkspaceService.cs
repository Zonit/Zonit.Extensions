using System.Globalization;
using Zonit.Extensions.Text;

namespace Zonit.Extensions.Organizations.Services;

/// <summary>
/// Maps the legacy <see cref="OrganizationModel"/> stored in <see cref="IWorkspaceManager"/>
/// to the public <see cref="Extensions.Organization"/> value object exposed via <see cref="IWorkspaceProvider"/>.
/// </summary>
/// <remarks>
/// <para><b>Mapping is total, never throwing.</b> <see cref="OrganizationModel"/> is a plain
/// consumer-owned DTO with no validation, while the value objects it feeds are strict
/// (<see cref="Extensions.Organization"/> rejects <see cref="Guid.Empty"/>, <see cref="Title"/>
/// rejects blank text and anything over <see cref="Title.MaxLength"/> graphemes). Hydration runs
/// during DI construction of <see cref="IWorkspaceProvider"/> and again inside the
/// <see cref="IWorkspaceManager.OnChange"/> fan-out raised from <c>WorkspaceMiddleware</c>, so a
/// throw here surfaces as an unhandled 500 on every request of the affected user — for data that
/// the source, not the caller, got wrong. A legal entity name longer than 60 characters is
/// ordinary, so that case must degrade, not fail.</para>
/// </remarks>
internal sealed class WorkspaceService : IWorkspaceProvider, IDisposable
{
    private readonly IWorkspaceManager _manager;
    private Organization _organization = Organization.Empty;

    public Organization Organization => _organization;
    public event Action? OnChange;

    public WorkspaceService(IWorkspaceManager manager)
    {
        _manager = manager;
        _manager.OnChange += HandleStateChanged;
        Hydrate();
    }

    private void Hydrate()
    {
        var model = _manager.Workspace?.Organization;

        // No id means the row is not addressable — SwitchOrganizationAsync could never reach it
        // and the VO refuses to carry it — so it reads as "no workspace selected" rather than as
        // an exception thrown out of a property that Razor evaluates on the render path.
        _organization = model is null || model.Id == Guid.Empty
            ? Organization.Empty
            : new Organization(model.Id, ToTitle(model.Name));
    }

    /// <summary>
    /// Non-throwing <see cref="string"/> → <see cref="Title"/> projection for consumer-supplied names.
    /// </summary>
    /// <remarks>
    /// Blank collapses to <see cref="Title.Empty"/>. Over-long text is cut at the grapheme boundary
    /// <see cref="Title"/> itself enforces, because that 60-grapheme cap is a <i>display</i>
    /// constraint: a shortened label still identifies the organization in a switcher, an empty one
    /// does not, and neither may cost the user their page.
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
