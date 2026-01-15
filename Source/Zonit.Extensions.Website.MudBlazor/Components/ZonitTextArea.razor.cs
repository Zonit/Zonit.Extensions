namespace Zonit.Extensions.Website.MudBlazor.Components;

/// <summary>
/// Multiline text input with automatic Value Object converter support.
/// Type T is inferred from @bind-Value - no need to specify T explicitly.
/// </summary>
/// <typeparam name="T">Inferred automatically from @bind-Value expression</typeparam>
/// <remarks>
/// <para>Wrapper around <see cref="ZonitTextField{T}"/> with Lines="3" by default.</para>
/// <para>AOT and Trimming compatible - type resolution happens at compile time.</para>
/// </remarks>
/// <example>
/// <code>
/// &lt;ZonitTextArea @bind-Value="Model.Description" Label="Description" /&gt;
/// &lt;ZonitTextArea @bind-Value="Model.Content" Label="Content" Lines="10" /&gt;
/// </code>
/// </example>
public partial class ZonitTextArea<T> : ZonitTextField<T>
{
    /// <summary>
    /// Initializes the component with default Lines value for multiline input.
    /// </summary>
    protected override void OnInitialized()
    {
        // Set default Lines if not explicitly provided
        if (Lines == 1)
        {
            Lines = 3;
        }
        
        base.OnInitialized();
    }
}
