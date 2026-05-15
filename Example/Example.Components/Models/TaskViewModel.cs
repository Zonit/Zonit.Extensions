using System.ComponentModel.DataAnnotations;
using Zonit.Extensions.Website;

namespace Example.Components.Models;

/// <summary>
/// View-model used by the <c>/components/page-view</c> and <c>/components/page-edit</c>
/// demos. Annotated with <see cref="DataAnnotations"/> for validation by
/// <c>PageEditBase&lt;T&gt;</c> + <see cref="AutoSaveAttribute"/> to demonstrate
/// per-field auto-save.
/// </summary>
/// <remarks>
/// <b>Why a public top-level type?</b> The trimmer keeps members of the type used as
/// <c>TViewModel</c> via <c>[DynamicallyAccessedMembers]</c> on <c>PageViewBase&lt;T&gt;</c>.
/// Keeping the model public also lets the source generator emit
/// <c>ViewModelMetadata&lt;TaskViewModel&gt;</c> automatically, swapping the runtime from
/// reflection to AOT-safe accessors.
/// </remarks>
public sealed class TaskViewModel
{
    [Required, StringLength(80, MinimumLength = 3)]
    public string Title { get; set; } = "Buy groceries";

    [StringLength(500)]
    [AutoSave(DelayMs = 1500)]
    public string? Notes { get; set; }

    [Range(1, 5)]
    [AutoSave]
    public int Priority { get; set; } = 3;

    public DateOnly DueDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));

    public bool Completed { get; set; }
}
