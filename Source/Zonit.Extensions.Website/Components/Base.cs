using Microsoft.AspNetCore.Components;

namespace Zonit.Extensions.Website;

public class Base : ComponentBase, IDisposable
{
    /// <summary>
    /// Token anulowania operacji asynchronicznych
    /// </summary>
    protected CancellationTokenSource? CancellationTokenSource { get; private set; }

    /// <summary>
    /// Flaga oznaczająca, czy komponent został zniszczony
    /// </summary>
    protected bool IsDisposed { get; private set; }

    public Base()
    {
        CancellationTokenSource = new CancellationTokenSource();
    }

    /// <summary>
    /// Metoda wywoływana przy inicjalizacji komponentu z obsługą CancellationToken
    /// </summary>
    protected override Task OnInitializedAsync()
    {
        return OnInitializedAsync(CancellationTokenSource?.Token ?? CancellationToken.None);
    }

    /// <summary>
    /// Rozszerzona metoda inicjalizacji obsługująca token anulowania
    /// </summary>
    protected virtual Task OnInitializedAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Metoda wywoływana po ustawieniu parametrów z obsługą CancellationToken
    /// </summary>
    protected override Task OnParametersSetAsync()
    {
        return OnParametersSetAsync(CancellationTokenSource?.Token ?? CancellationToken.None);
    }

    /// <summary>
    /// Rozszerzona metoda ustawiania parametrów obsługująca token anulowania
    /// </summary>
    protected virtual Task OnParametersSetAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Metoda wywoływana po renderingu z obsługą CancellationToken
    /// </summary>
    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        return OnAfterRenderAsync(firstRender, CancellationTokenSource?.Token ?? CancellationToken.None);
    }

    /// <summary>
    /// Rozszerzona metoda wywoływana po renderingu obsługująca token anulowania
    /// </summary>
    protected virtual Task OnAfterRenderAsync(bool firstRender, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

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
                // Anuluj wszystkie operacje asynchroniczne
                try
                {
                    CancellationTokenSource?.Cancel();
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
    {
        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// Finalizator
    /// </summary>
    ~Base()
    {
        Dispose(false);
    }
}