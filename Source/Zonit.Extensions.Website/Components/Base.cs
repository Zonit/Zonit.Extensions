using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace Zonit.Extensions.Website;

public class Base : ComponentBase, IDisposable
{
    /// <summary>
    /// Token anulowania operacji asynchronicznych
    /// </summary>
    protected CancellationTokenSource? CancellationTokenSource { get; set; }

    /// <summary>
    /// Flaga oznaczająca, czy komponent został zniszczony
    /// </summary>
    protected bool IsDisposed { get; private set; }

    /// <summary>
    /// Logger dla komponentu - automatycznie wykorzystuje nazwę typu wywołującego
    /// </summary>
    [Inject]
    protected ILoggerFactory? LoggerFactory { get; set; }

    /// <summary>
    /// Właściwość zwracająca logger dla aktualnego typu
    /// </summary>
    protected ILogger Logger => _lazyLogger?.Value ?? NullLogger.Instance;

    private Lazy<ILogger>? _lazyLogger;

    public Base()
    {
        CancellationTokenSource = new CancellationTokenSource();
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _lazyLogger = new Lazy<ILogger>(() =>
        {
            var actualType = this.GetType();
            return LoggerFactory?.CreateLogger(actualType.FullName ?? actualType.Name)
                ?? NullLogger.Instance;
        });
    }

    /// <summary>
    /// Metoda wywoływana przy inicjalizacji komponentu z obsługą CancellationToken
    /// </summary>
    protected override Task OnInitializedAsync()
        => OnInitializedAsync(CancellationTokenSource?.Token ?? CancellationToken.None);

    /// <summary>
    /// Rozszerzona metoda inicjalizacji obsługująca token anulowania
    /// </summary>
    protected virtual Task OnInitializedAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <summary>
    /// Metoda wywoływana po ustawieniu parametrów z obsługą CancellationToken
    /// </summary>
    protected override Task OnParametersSetAsync()
        => OnParametersSetAsync(CancellationTokenSource?.Token ?? CancellationToken.None);

    /// <summary>
    /// Rozszerzona metoda ustawiania parametrów obsługująca token anulowania
    /// </summary>
    protected virtual Task OnParametersSetAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <summary>
    /// Metoda wywoływana po renderingu z obsługą CancellationToken
    /// </summary>
    protected override Task OnAfterRenderAsync(bool firstRender)
        => OnAfterRenderAsync(firstRender, CancellationTokenSource?.Token ?? CancellationToken.None);

    /// <summary>
    /// Rozszerzona metoda wywoływana po renderingu obsługująca token anulowania
    /// </summary>
    protected virtual Task OnAfterRenderAsync(bool firstRender, CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <summary>
    /// Implementacja IDisposable.Dispose()
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Metoda do zwalniania zasobów, która może być nadpisana w klasach pochodnych
    /// </summary>
    /// <param name="disposing">True jeśli metoda została wywołana bezpośrednio, false jeśli poprzez finalizator</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            if (disposing)
            {
                try
                {
                    CancellationTokenSource?.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // Ignoruj błąd, jeśli CancellationTokenSource został już zniszczony
                }
                finally
                {
                    CancellationTokenSource = null;
                }
            }

            IsDisposed = true;
        }
    }

    /// <summary>
    /// Metoda pomocnicza do sprawdzania czy token anulowania został aktywowany
    /// </summary>
    protected static void ThrowIfCancellationRequested(CancellationToken cancellationToken)
        => cancellationToken.ThrowIfCancellationRequested();

    /// <summary>
    /// Klasa implementująca pusty logger, gdy nie jest dostępny ILoggerFactory
    /// </summary>
    private class NullLogger : ILogger
    {
        public static readonly ILogger Instance = new NullLogger();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }
    }

    /// <summary>
    /// Finalizator
    /// </summary>
    ~Base()
    {
        Dispose(false);
    }
}