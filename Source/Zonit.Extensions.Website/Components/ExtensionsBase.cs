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
    /// Określa, czy komponent powinien zarządzać okruszkami (breadcrumbs).
    /// - true: Inicjalizuje okruszki z wartością Breadcrumbs
    /// - false: Nie zmienia aktualnych okruszków (używane w modalach)
    /// - null: Kasuje okruszki
    /// </summary>
    protected virtual bool? ShowBreadcrumbs { get; } = true;

    /// <summary>
    /// Lista okruszków (breadcrumbs) do wyświetlenia.
    /// </summary>
    protected virtual List<BreadcrumbsModel>? Breadcrumbs { get; }

    // Leniwe właściwości, które pobierają zależności tylko gdy są używane
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
            Logger.LogDebug("Inicjalizacja ICultureProvider dla {ComponentType}", GetType().Name);
            service.OnChange += OnRefreshChangeAsync;
            return service;
        });

        _workspace = new Lazy<IWorkspaceProvider>(() =>
        {
            var service = GetService<IWorkspaceProvider>();
            Logger.LogDebug("Inicjalizacja IWorkspaceProvider dla {ComponentType}", GetType().Name);
            service.OnChange += OnRefreshChangeAsync;
            return service;
        });

        _catalog = new Lazy<ICatalogProvider>(() =>
        {
            var service = GetService<ICatalogProvider>();
            Logger.LogDebug("Inicjalizacja ICatalogProvider dla {ComponentType}", GetType().Name);
            service.OnChange += OnRefreshChangeAsync;
            return service;
        });

        _authenticated = new Lazy<IAuthenticatedProvider>(() =>
        {
            var service = GetService<IAuthenticatedProvider>();
            Logger.LogDebug("Inicjalizacja IAuthenticatedProvider dla {ComponentType}", GetType().Name);
            return service;
        });

        _breadcrumbs = new Lazy<IBreadcrumbsProvider>(() =>
        {
            var service = GetService<IBreadcrumbsProvider>();
            Logger.LogDebug("Inicjalizacja IBreadcrumbsProvider dla {ComponentType}", GetType().Name);
            service.OnChange += OnRefreshChangeAsync;
            return service;
        });
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();

        Logger.LogDebug("Inicjalizacja ExtensionsBase dla {ComponentType}", GetType().Name);

        // Zarządzanie breadcrumbs w zależności od wartości ShowBreadcrumbs:
        // - true: inicjalizuje breadcrumbs z wartością Breadcrumbs
        // - false: nic nie robi (nie modyfikuje aktualnych breadcrumbs)
        // - null: usuwa breadcrumbs
        if (ShowBreadcrumbs == true)
        {
            Logger.LogDebug("Inicjalizacja breadcrumbs dla {ComponentType}", GetType().Name);
            BreadcrumbsProvider.Initialize(this.Breadcrumbs);
        }
        else if (ShowBreadcrumbs == null)
        {
            Logger.LogDebug("Usuwanie breadcrumbs dla {ComponentType}", GetType().Name);
            BreadcrumbsProvider.Initialize(null);
        }
        // gdy ShowBreadcrumbs == false, nie robimy nic
        else
        {
            Logger.LogDebug("Zachowanie istniejących breadcrumbs dla {ComponentType}", GetType().Name);
        }
    }

    private T GetService<T>() where T : class
    {
        Logger.LogTrace("Pobieranie serwisu {ServiceType} dla {ComponentType}", typeof(T).Name, GetType().Name);
        return ServiceProvider.GetRequiredService<T>();
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

    protected override void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            Logger.LogDebug("Zwalnianie zasobów ExtensionsBase dla {ComponentType}", GetType().Name);

            if (_culture.IsValueCreated)
            {
                Logger.LogTrace("Odsubskrypcja zdarzeń ICultureProvider dla {ComponentType}", GetType().Name);
                _culture.Value.OnChange -= OnRefreshChangeAsync;
            }

            if (_workspace.IsValueCreated)
            {
                Logger.LogTrace("Odsubskrypcja zdarzeń IWorkspaceProvider dla {ComponentType}", GetType().Name);
                _workspace.Value.OnChange -= OnRefreshChangeAsync;
            }

            if (_catalog.IsValueCreated)
            {
                Logger.LogTrace("Odsubskrypcja zdarzeń ICatalogProvider dla {ComponentType}", GetType().Name);
                _catalog.Value.OnChange -= OnRefreshChangeAsync;
            }

            if (_breadcrumbs.IsValueCreated)
            {
                Logger.LogTrace("Odsubskrypcja zdarzeń IBreadcrumbsProvider dla {ComponentType}", GetType().Name);
                _breadcrumbs.Value.OnChange -= OnRefreshChangeAsync;
            }
        }

        _disposed = true;

        // Wywołanie Dispose z klasy bazowej po własnych operacjach
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
            Logger.LogDebug("Odświeżanie komponentu {ComponentType}", GetType().Name);

            await OnInitializedAsync(token);

            if (token.IsCancellationRequested)
            {
                Logger.LogDebug("Odświeżanie komponentu {ComponentType} anulowane", GetType().Name);
                return;
            }

            await InvokeAsync(StateHasChanged);
            Logger.LogDebug("Odświeżanie komponentu {ComponentType} zakończone", GetType().Name);
        }
        catch (OperationCanceledException)
        {
            Logger.LogDebug("Odświeżanie komponentu {ComponentType} anulowane przez CancellationToken", GetType().Name);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Błąd podczas odświeżania komponentu {ComponentType}: {Message}",
                GetType().Name, ex.Message);
        }
    }
}