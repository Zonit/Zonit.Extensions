using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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

    private readonly Lazy<IToastProvider> _toast;
    protected IToastProvider Toast => _toast.Value;

    private readonly Lazy<ICookieProvider> _cookie;
    protected ICookieProvider Cookie => _cookie.Value;

    protected ExtensionsBase()
    {
        _culture = new Lazy<ICultureProvider>(() =>
        {
            var service = GetService<ICultureProvider>();
            service.OnChange += OnUIRefreshChangeAsync;
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
            service.OnChange += OnUIRefreshChangeAsync;
            return service;
        });

        _toast = new Lazy<IToastProvider>(() =>
        {
            return GetService<IToastProvider>();
        });

        _cookie = new Lazy<ICookieProvider>(() =>
        {
            return GetService<ICookieProvider>();
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

    /// <summary>
    /// Pobiera opcje konfiguracji typu TModel
    /// </summary>
    /// <typeparam name="TModel">Typ modelu opcji</typeparam>
    /// <returns>Wartości opcji</returns>
    protected TModel Options<TModel>() where TModel : class
        => ServiceProvider.GetRequiredService<IOptions<TModel>>().Value;

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
                _culture.Value.OnChange -= OnUIRefreshChangeAsync;

            if (_workspace.IsValueCreated)
                _workspace.Value.OnChange -= OnRefreshChangeAsync;

            if (_catalog.IsValueCreated)
                _catalog.Value.OnChange -= OnRefreshChangeAsync;

            if (_breadcrumbs.IsValueCreated)
                _breadcrumbs.Value.OnChange -= OnUIRefreshChangeAsync;
        }

        _disposed = true;
        base.Dispose(disposing);
    }

    ~ExtensionsBase()
        => Dispose(false);

    /// <summary>
    /// Metoda do odświeżania tylko interfejsu użytkownika bez przeładowania danych
    /// </summary>
    protected virtual async void OnUIRefreshChangeAsync()
    {
        try
        {
            var token = CancellationTokenSource?.Token ?? CancellationToken.None;

            if (token.IsCancellationRequested)
                return;

            await InvokeAsync(StateHasChanged);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Błąd podczas odświeżania interfejsu komponentu {ComponentType}: {Message}",
                GetType().Name, ex.Message);
        }
    }

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
                return;

            await InvokeAsync(StateHasChanged);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Błąd podczas odświeżania komponentu {ComponentType}: {Message}",
                GetType().Name, ex.Message);
        }
    }
}