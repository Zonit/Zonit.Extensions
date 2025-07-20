using Zonit.Extensions.Website.Abstractions.Toasts.Types;

namespace Zonit.Extensions.Website;

public interface IToastProvider
{
    public void Add(string message, ToastType taskType);

    public void AddNormal(string message) => Add(message, ToastType.Normal);
    public void AddInfo(string message) => Add(message, ToastType.Info);
    public void AddSuccess(string message) => Add(message, ToastType.Success);
    public void AddWarning(string message) => Add(message, ToastType.Warning);
    public void AddError(string message) => Add(message, ToastType.Error);
}