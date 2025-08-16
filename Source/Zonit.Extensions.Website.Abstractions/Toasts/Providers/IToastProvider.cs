using Zonit.Extensions.Website.Abstractions.Toasts.Types;

namespace Zonit.Extensions.Website;

public interface IToastProvider
{
    public void Add(ToastType taskType, string message, params object[]? args);

    public void AddNormal(string message, params object[]? args) 
        => Add(ToastType.Normal, message, args);

    public void AddInfo(string message, params object[]? args) 
        => Add(ToastType.Info, message, args);

    public void AddSuccess(string message, params object[]? args) 
        => Add(ToastType.Success, message, args);

    public void AddWarning(string message, params object[]? args) 
        => Add(ToastType.Warning, message, args);

    public void AddError(string message, params object[]? args) 
        => Add(ToastType.Error, message, args);
}