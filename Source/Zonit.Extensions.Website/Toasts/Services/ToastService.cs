using Zonit.Extensions.Website.Abstractions.Toasts.Types;

namespace Zonit.Extensions.Website.Toasts.Services;

public class ToastService : IToastProvider
{
    public void Add(ToastType taskType, string message, params object[]? args)
    {
        
    }
}
