using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Zonit.Extensions.Cultures;
using Zonit.Extensions.Identity;
using Zonit.Extensions.Organizations;
using Zonit.Extensions.Projects;

namespace Zonit.Extensions.Website;

public abstract class ExtensionsBase : ComponentBase, IDisposable
{
    private bool _disposed;

    [Inject]
    private IServiceProvider ServiceProvider { get; set; } = default!;

    // Leniwe właściwości, które pobierają zależności tylko gdy są używane
    private ICultureProvider? _culture;
    protected ICultureProvider Culture => _culture ??= GetService<ICultureProvider>();

    private IWorkspaceProvider? _workspace;
    protected IWorkspaceProvider Workspace => _workspace ??= GetService<IWorkspaceProvider>();

    private ICatalogProvider? _catalog;
    protected ICatalogProvider Catalog => _catalog ??= GetService<ICatalogProvider>();

    private IAuthenticatedProvider? _authenticated;
    protected IAuthenticatedProvider Authenticated => _authenticated ??= GetService<IAuthenticatedProvider>();

    private bool _cultureInitialized;
    private bool _workspaceInitialized;
    private bool _catalogInitialized;

    private T GetService<T>() where T : class
    {
        var service = ServiceProvider.GetRequiredService<T>();

        if (service is ICultureProvider cultureProvider && !_cultureInitialized)
        {
            cultureProvider.OnChange += OnRefreshChangeAsync;
            _cultureInitialized = true;
        }
        else if (service is IWorkspaceProvider workspaceProvider && !_workspaceInitialized)
        {
            workspaceProvider.OnChange += OnRefreshChangeAsync;
            _workspaceInitialized = true;
        }
        else if (service is ICatalogProvider catalogProvider && !_catalogInitialized)
        {
            catalogProvider.OnChange += OnRefreshChangeAsync;
            _catalogInitialized = true;
        }

        return service;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public string T(string key, params object[] args)
        => Culture.Translate(key, args);

    public string Translate(string key, params object[] args)
        => Culture.Translate(key, args);

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            if (_culture is not null && _cultureInitialized)
                _culture.OnChange -= OnRefreshChangeAsync;

            if (_workspace is not null && _workspaceInitialized)
                _workspace.OnChange -= OnRefreshChangeAsync;

            if (_catalog is not null && _catalogInitialized)
                _catalog.OnChange -= OnRefreshChangeAsync;
        }

        _disposed = true;
    }

    ~ExtensionsBase()
        => Dispose(false);

    /// <summary>
    /// Klasa do ponownego przeładowania treści na stronie, danych np z bazy danych
    /// </summary>
    protected virtual async void OnRefreshChangeAsync()
    {
        try
        {
            await OnInitializedAsync();
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error in OnRefreshChange: {ex.Message}");
        }
    }
}