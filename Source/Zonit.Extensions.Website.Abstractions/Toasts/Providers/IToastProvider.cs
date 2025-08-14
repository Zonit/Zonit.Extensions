using Zonit.Extensions.Website.Abstractions.Toasts.Types;

namespace Zonit.Extensions.Website;

public interface IToastProvider
{
    public void Add(string message, ToastType taskType, params object[]? args);

    public void AddNormal(string message, params object[]? args) 
        => Add(message, ToastType.Normal, args);

    public void AddInfo(string message, params object[]? args) 
        => Add(message, ToastType.Info, args);

    public void AddSuccess(string message, params object[]? args) 
        => Add(message, ToastType.Success, args);

    public void AddWarning(string message, params object[]? args) 
        => Add(message, ToastType.Warning, args);

    public void AddError(string message, params object[]? args) 
        => Add(message, ToastType.Error, args);
}