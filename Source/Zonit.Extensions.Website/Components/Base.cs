using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace Zonit.Extensions.Website;

public class Base : ComponentBase, IDisposable
{
    /// <summary>
    /// Token anulowania operacji asynchronicznych
    /// </summary>
    protected CancellationTokenSource? CancellationTokenSource { get; private set; }

    /// <summary>
    /// Token życia komponentu. Po <see cref="Dispose()"/> zwraca token już anulowany,
    /// więc każda praca uruchomiona w wyścigu z niszczeniem komponentu (np. zdarzenie
    /// <c>OnChange</c> dostawcy, które dotarło tuż po odsubskrybowaniu) natychmiast
    /// widzi anulowanie zamiast startować od nowa.
    /// </summary>
    /// <remarks>
    /// Odczyt <see cref="CancellationTokenSource.Token"/> na zwolnionym źródle rzuca
    /// <see cref="ObjectDisposedException"/> — dlatego dostęp jest tu opakowany, a nie
    /// rozsiany po wszystkich miejscach wywołania.
    /// </remarks>
    protected CancellationToken ComponentToken
    {
        get
        {
            var cts = CancellationTokenSource;
            if (cts is null)
                return new CancellationToken(canceled: true);

            try
            {
                return cts.Token;
            }
            catch (ObjectDisposedException)
            {
                return new CancellationToken(canceled: true);
            }
        }
    }

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
        => OnInitializedAsync(ComponentToken);

    /// <summary>
    /// Rozszerzona metoda inicjalizacji obsługująca token anulowania
    /// </summary>
    protected virtual Task OnInitializedAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <summary>
    /// Metoda wywoływana po ustawieniu parametrów z obsługą CancellationToken
    /// </summary>
    protected override Task OnParametersSetAsync()
        => OnParametersSetAsync(ComponentToken);

    /// <summary>
    /// Rozszerzona metoda ustawiania parametrów obsługująca token anulowania
    /// </summary>
    protected virtual Task OnParametersSetAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <summary>
    /// Metoda wywoływana po renderingu z obsługą CancellationToken
    /// </summary>
    protected override Task OnAfterRenderAsync(bool firstRender)
        => OnAfterRenderAsync(firstRender, ComponentToken);

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
                // Kolejność ma znaczenie: bez Cancel() token przekazywany do
                // OnInitializedAsync/LoadAsync/SubmitAsync nigdy nie przechodzi w stan
                // anulowany, więc rozpoczęte zapytania do bazy i HTTP biegłyby dalej po
                // opuszczeniu strony, a wszystkie `if (token.IsCancellationRequested)`
                // w klasach pochodnych byłyby martwym kodem.
                try
                {
                    CancellationTokenSource?.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // Źródło zostało już zwolnione — nie ma czego anulować.
                }
                catch (AggregateException ex)
                {
                    // Cancel() propaguje wyjątki z callbacków rejestrowanych na tokenie.
                    // Zniszczenie komponentu nie może się przez nie wywrócić.
                    Logger.LogDebug(ex,
                        "Callback anulowania zgłosił wyjątek podczas niszczenia {ComponentType}.",
                        GetType().Name);
                }

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

    // Świadomie BEZ finalizatora. Klasa trzyma wyłącznie zasoby zarządzane
    // (CancellationTokenSource, subskrypcje), a finalizator na klasie bazowej każdego
    // komponentu Blazor promowałby każdą jego instancję do kolejki finalizacji —
    // realny koszt GC bez żadnej korzyści, bo Dispose(false) i tak nie ma czego zwolnić.
    // Sygnatura Dispose(bool) zostaje, żeby nie łamać klas pochodnych, które ją nadpisują.
}