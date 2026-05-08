using Zonit.Extensions.Website;

namespace ExampleWebsite.Components.Demo;

/// <summary>
/// Minimal consumer of <see cref="PageEditBase{TViewModel}"/> used only to verify
/// that the source generator wires up AOT-safe metadata for <see cref="DemoViewModel"/>.
/// </summary>
public class DemoEditPage : PageEditBase<DemoViewModel>
{
    protected override async Task SubmitAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(50, cancellationToken);
    }
}
