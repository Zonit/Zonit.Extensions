using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using Zonit.Extensions.Cultures;
using Zonit.Extensions.Auth;
using Zonit.Extensions.Organizations;
using Zonit.Extensions.Projects;
using Zonit.Extensions.Tenants;

namespace Zonit.Extensions.Website;

public abstract class ExtensionsBase : Base, IDisposable
{
    private bool _disposed;
    private readonly Dictionary<Type, IDisposable> _optionsMonitorSubscriptions = new();

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

    private readonly Lazy<ITenantProvider> _tenant;
    /// <summary>
    /// Current tenant + per-domain settings (Site, Theme, Maintenance, etc.).
    /// Subscribes to <see cref="ITenantProvider.OnChange"/> so a tenant switch (e.g.
    /// admin impersonation in multi-site mode) re-runs the component's data load.
    /// </summary>
    protected ITenantProvider Tenant => _tenant.Value;

    private readonly Lazy<ILayoutContext> _layoutContext;
    /// <summary>
    /// Per-circuit dynamic layout-override channel. Exposed as <c>protected</c> so
    /// <see cref="PageBase"/> can surface a typed <c>LayoutKey</c> property without
    /// reaching back into the DI container. No <c>OnChange</c> subscription here:
    /// the layout transition is consumed by <c>ZonitRouteView</c>, not by the page
    /// itself.
    /// </summary>
    protected ILayoutContext LayoutContext => _layoutContext.Value;

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

        _tenant = new Lazy<ITenantProvider>(() =>
        {
            var service = GetService<ITenantProvider>();
            // Treat tenant changes the same as Workspace/Catalog — re-run data load,
            // since per-domain settings often parameterise what data even gets fetched.
            service.OnChange += OnRefreshChangeAsync;
            return service;
        });

        _layoutContext = new Lazy<ILayoutContext>(GetService<ILayoutContext>);
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
    /// Pobiera opcje konfiguracji typu <typeparamref name="TModel"/> z automatycznym odświeżaniem interfejsu przy zmianie.
    /// </summary>
    /// <remarks>
    /// Wymaga <c>IOptionsMonitor&lt;TModel&gt;</c>, które używa bezparametrowego konstruktora i refleksji
    /// nad publicznymi właściwościami <typeparamref name="TModel"/> do bindingu konfiguracji.
    /// W trybie trimmed/AOT, <typeparamref name="TModel"/> musi mieć zachowane publiczne konstruktory i właściwości.
    /// </remarks>
    /// <typeparam name="TModel">Typ modelu opcji (musi mieć bezparametrowy konstruktor).</typeparam>
    /// <returns>Aktualne wartości opcji z monitorowaniem.</returns>
    [RequiresUnreferencedCode("IOptionsMonitor<T> binds configuration using reflection over TModel's public properties.")]
    [RequiresDynamicCode("IOptionsMonitor<T> may require runtime code generation for TModel binding.")]
    protected TModel Options<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TModel>() where TModel : class, new()
    {
        var monitor = ServiceProvider.GetRequiredService<IOptionsMonitor<TModel>>();

        // Zarejestruj callback tylko jeśli jeszcze nie istnieje dla tego typu
        if (!_optionsMonitorSubscriptions.ContainsKey(typeof(TModel)))
        {
            var subscription = monitor.OnChange((options, name) =>
            {
                OnRefreshChangeAsync();
            });

            // Zapisuj subscription tylko jeśli nie jest null
            if (subscription != null)
                _optionsMonitorSubscriptions[typeof(TModel)] = subscription;
        }

        return monitor.CurrentValue;
    }

    // *** PROSTE ROZWIĄZANIE: T() = string, TH() = HTML MarkupString ***
    
    /// <summary>
    /// Zwraca tłumaczenie jako string (domyślna metoda dla parametrów komponentów)
    /// </summary>
    public string T(string content, params object[] args)
        => Culture.Translate(content, args).ToString();

    /// <summary>
    /// Render translation as <see cref="MarkupString"/> for raw-HTML output in Razor markup.
    /// </summary>
    public MarkupString TM(string content, params object[] args)
        => new(Culture.Translate(content, args).Value);

    /// <summary>
    /// Returns the <see cref="Translation"/> value object directly, for call sites that want
    /// to inspect <c>IsEmpty</c> or pass it through equality checks.
    /// </summary>
    public Translation Translate(string content, params object[] args)
        => Culture.Translate(content, args);

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

            if (_tenant.IsValueCreated)
                _tenant.Value.OnChange -= OnRefreshChangeAsync;

            // Dispose wszystkich subskrypcji IOptionsMonitor
            foreach (var subscription in _optionsMonitorSubscriptions.Values)
            {
                subscription?.Dispose();
            }
            _optionsMonitorSubscriptions.Clear();
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