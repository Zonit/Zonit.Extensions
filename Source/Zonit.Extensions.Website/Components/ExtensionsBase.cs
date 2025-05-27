using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Zonit.Extensions.Cultures;
using Zonit.Extensions.Identity;
using Zonit.Extensions.Organizations;
using Zonit.Extensions.Projects;

namespace Zonit.Extensions.Website;

public abstract class ExtensionsBase : Base, IDisposable
{
    private bool _disposed;

    [Inject]
    private IServiceProvider ServiceProvider { get; set; } = default!;

    [Inject]
    protected NavigationManager Navigation { get; set; } = default!;

    /// <summary>
    /// Określa, czy komponent powinien zarządzać okruszkami (breadcrumbs). <br />
    /// - true: Inicjalizuje okruszki z wartością Breadcrumbs <br />
    /// - false: Nie zmienia aktualnych okruszków (używane w modalach) <br />
    /// - null: Kasuje okruszki <br />
    /// </summary>
    protected virtual bool? ShowBreadcrumbs { get; } = false;

    /// <summary>
    /// Lista okruszków (breadcrumbs) do wyświetlenia.
    /// </summary>
    protected virtual List<BreadcrumbsModel>? Breadcrumbs { get; }

    private readonly Lazy<ICultureProvider> _culture;
    protected ICultureProvider Culture => _culture.Value;

    private readonly Lazy<IWorkspaceProvider> _workspace;
    protected IWorkspaceProvider Workspace => _workspace.Value;

    private readonly Lazy<ICatalogProvider> _catalog;
    protected ICatalogProvider Catalog => _catalog.Value;

    private readonly Lazy<IAuthenticatedProvider> _authenticated;
    protected IAuthenticatedProvider Authenticated => _authenticated.Value;

    private readonly Lazy<IBreadcrumbsProvider> _breadcrumbs;
    protected IBreadcrumbsProvider BreadcrumbsProvider => _breadcrumbs.Value;

    protected ExtensionsBase()
    {
        _culture = new Lazy<ICultureProvider>(() =>
        {
            var service = GetService<ICultureProvider>();
            service.OnChange += OnRefreshChangeAsync;
            return service;
        });

        _workspace = new Lazy<IWorkspaceProvider>(() =>
        {
            var service = GetService<IWorkspaceProvider>();
            service.OnChange += OnRefreshChangeAsync;
            return service;
        });

        _catalog = new Lazy<ICatalogProvider>(() =>
        {
            var service = GetService<ICatalogProvider>();
            service.OnChange += OnRefreshChangeAsync;
            return service;
        });

        _authenticated = new Lazy<IAuthenticatedProvider>(() =>
        {
            return GetService<IAuthenticatedProvider>();
        });

        _breadcrumbs = new Lazy<IBreadcrumbsProvider>(() =>
        {
            var service = GetService<IBreadcrumbsProvider>();
            service.OnChange += OnRefreshChangeAsync;
            return service;
        });
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();

        if (ShowBreadcrumbs == true)
            BreadcrumbsProvider.Initialize(this.Breadcrumbs);
        else if (ShowBreadcrumbs == null)
            BreadcrumbsProvider.Initialize(null);
    }

    private T GetService<T>() where T : class
        => ServiceProvider.GetRequiredService<T>();

    public MarkupString T(string key, params object[] args)
        => new (Culture.Translate(key, args));

    public MarkupString Translate(string key, params object[] args)
        => new (Culture.Translate(key, args));

    protected override void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            if (_culture.IsValueCreated)
                _culture.Value.OnChange -= OnRefreshChangeAsync;

            if (_workspace.IsValueCreated)
                _workspace.Value.OnChange -= OnRefreshChangeAsync;

            if (_catalog.IsValueCreated)
                _catalog.Value.OnChange -= OnRefreshChangeAsync;

            if (_breadcrumbs.IsValueCreated)
                _breadcrumbs.Value.OnChange -= OnRefreshChangeAsync;
        }

        _disposed = true;
        base.Dispose(disposing);
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
            var token = CancellationTokenSource?.Token ?? CancellationToken.None;
            await OnInitializedAsync(token);

            if (token.IsCancellationRequested)
            {
                return;
            }

            await InvokeAsync(StateHasChanged);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Błąd podczas odświeżania komponentu {ComponentType}: {Message}",
                GetType().Name, ex.Message);
        }
    }
}