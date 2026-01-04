namespace Zonit.Extensions;

/// <summary>
/// Reprezentuje prawid³owy adres URL.
/// </summary>
public readonly struct Url : IEquatable<Url>
{
    /// <summary>
    /// Wartoœæ URL.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Pobiera obiekt Uri reprezentuj¹cy ten URL.
    /// </summary>
    public Uri Uri => new(Value);

    /// <summary>
    /// Pobiera schemat URL (http, https, ftp, etc.).
    /// </summary>
    public string Scheme => Uri.Scheme;

    /// <summary>
    /// Pobiera host URL.
    /// </summary>
    public string Host => Uri.Host;

    /// <summary>
    /// Pobiera port URL.
    /// </summary>
    public int Port => Uri.Port;

    /// <summary>
    /// Pobiera œcie¿kê URL.
    /// </summary>
    public string Path => Uri.AbsolutePath;

    /// <summary>
    /// Pobiera query string URL.
    /// </summary>
    public string Query => Uri.Query;

    /// <summary>
    /// Sprawdza czy URL u¿ywa HTTPS.
    /// </summary>
    public bool IsHttps => Uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Sprawdza czy URL jest bezwzglêdny (absolute).
    /// </summary>
    public bool IsAbsolute => Uri.IsAbsoluteUri;

    /// <summary>
    /// Tworzy nowy URL na podstawie podanego adresu.
    /// </summary>
    /// <param name="value">Adres URL.</param>
    /// <exception cref="ArgumentNullException">Rzucany gdy <paramref name="value"/> jest null.</exception>
    /// <exception cref="UriFormatException">Rzucany gdy <paramref name="value"/> nie jest prawid³owym URL.</exception>
    public Url(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(value));

        var trimmedValue = value.Trim();

        if (!Uri.TryCreate(trimmedValue, UriKind.Absolute, out var uri))
        {
            throw new UriFormatException($"'{trimmedValue}' is not a valid absolute URL.");
        }

        Value = uri.AbsoluteUri;
    }

    /// <summary>
    /// Tworzy nowy URL z opcj¹ akceptacji wzglêdnych adresów.
    /// </summary>
    /// <param name="value">Adres URL.</param>
    /// <param name="allowRelative">Czy akceptowaæ wzglêdne URL.</param>
    /// <exception cref="ArgumentNullException">Rzucany gdy <paramref name="value"/> jest null.</exception>
    /// <exception cref="UriFormatException">Rzucany gdy <paramref name="value"/> nie jest prawid³owym URL.</exception>
    public Url(string value, bool allowRelative)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(value));

        var trimmedValue = value.Trim();
        var uriKind = allowRelative ? UriKind.RelativeOrAbsolute : UriKind.Absolute;

        if (!Uri.TryCreate(trimmedValue, uriKind, out var uri))
        {
            throw new UriFormatException($"'{trimmedValue}' is not a valid URL.");
        }

        Value = uri.IsAbsoluteUri ? uri.AbsoluteUri : uri.OriginalString;
    }

    /// <summary>
    /// Tworzy URL z obiektu Uri.
    /// </summary>
    /// <param name="uri">Obiekt Uri.</param>
    /// <exception cref="ArgumentNullException">Rzucany gdy <paramref name="uri"/> jest null.</exception>
    public Url(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri, nameof(uri));
        Value = uri.IsAbsoluteUri ? uri.AbsoluteUri : uri.OriginalString;
    }

    /// <summary>
    /// £¹czy aktualny URL z relatywn¹ œcie¿k¹.
    /// </summary>
    /// <param name="relativePath">Relatywna œcie¿ka do po³¹czenia.</param>
    /// <returns>Nowy URL bêd¹cy po³¹czeniem.</returns>
    public Url Combine(string relativePath)
    {
        var combinedUri = new Uri(Uri, relativePath);
        return new Url(combinedUri);
    }

    /// <summary>
    /// Dodaje lub aktualizuje parametr query string.
    /// </summary>
    /// <param name="key">Klucz parametru.</param>
    /// <param name="value">Wartoœæ parametru.</param>
    /// <returns>Nowy URL z dodanym parametrem.</returns>
    public Url WithQueryParameter(string key, string value)
    {
        var builder = new UriBuilder(Uri);
        var query = System.Web.HttpUtility.ParseQueryString(builder.Query);
        query[key] = value;
        builder.Query = query.ToString();
        return new Url(builder.Uri);
    }

    /// <summary>
    /// Usuwa parametr z query string.
    /// </summary>
    /// <param name="key">Klucz parametru do usuniêcia.</param>
    /// <returns>Nowy URL bez parametru.</returns>
    public Url WithoutQueryParameter(string key)
    {
        var builder = new UriBuilder(Uri);
        var query = System.Web.HttpUtility.ParseQueryString(builder.Query);
        query.Remove(key);
        builder.Query = query.ToString();
        return new Url(builder.Uri);
    }

    /// <summary>
    /// Konwertuje string na obiekt Url.
    /// </summary>
    public static explicit operator Url(string value) => new(value);

    /// <summary>
    /// Konwertuje Url na string.
    /// </summary>
    public static implicit operator string(Url url) => url.Value ?? string.Empty;

    /// <summary>
    /// Konwertuje Uri na Url.
    /// </summary>
    public static implicit operator Url(Uri uri) => new(uri);

    /// <summary>
    /// Konwertuje Url na Uri.
    /// </summary>
    public static implicit operator Uri(Url url) => url.Uri;

    /// <inheritdoc />
    public bool Equals(Url other)
    {
        return string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Url other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value ?? string.Empty);

    /// <summary>
    /// Porównuje dwa URL.
    /// </summary>
    public static bool operator ==(Url left, Url right) => left.Equals(right);

    /// <summary>
    /// Porównuje dwa URL.
    /// </summary>
    public static bool operator !=(Url left, Url right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>
    /// Tworzy URL z podanego adresu.
    /// </summary>
    public static Url Create(string value) => new(value);

    /// <summary>
    /// Tworzy URL z podanego adresu z opcj¹ akceptacji wzglêdnych adresów.
    /// </summary>
    public static Url Create(string value, bool allowRelative) => new(value, allowRelative);

    /// <summary>
    /// Próbuje utworzyæ URL z podanego adresu.
    /// </summary>
    /// <param name="value">Adres URL.</param>
    /// <param name="url">Utworzony URL lub default jeœli wartoœæ jest nieprawid³owa.</param>
    /// <returns>True jeœli URL zosta³ utworzony, false w przeciwnym razie.</returns>
    public static bool TryCreate(string? value, out Url url)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            url = default;
            return false;
        }

        try
        {
            url = new Url(value);
            return true;
        }
        catch (UriFormatException)
        {
            url = default;
            return false;
        }
    }

    /// <summary>
    /// Próbuje utworzyæ URL z podanego adresu z opcj¹ akceptacji wzglêdnych adresów.
    /// </summary>
    /// <param name="value">Adres URL.</param>
    /// <param name="allowRelative">Czy akceptowaæ wzglêdne URL.</param>
    /// <param name="url">Utworzony URL lub default jeœli wartoœæ jest nieprawid³owa.</param>
    /// <returns>True jeœli URL zosta³ utworzony, false w przeciwnym razie.</returns>
    public static bool TryCreate(string? value, bool allowRelative, out Url url)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            url = default;
            return false;
        }

        try
        {
            url = new Url(value, allowRelative);
            return true;
        }
        catch (UriFormatException)
        {
            url = default;
            return false;
        }
    }
}
