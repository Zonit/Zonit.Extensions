namespace Zonit.Extensions.Organizations;

public class OrganizationModel
{
    /// <summary>
    /// Organization identifier. Must be non-empty: a row carrying <see cref="Guid.Empty"/> is not
    /// addressable by <c>IWorkspaceManager.SwitchOrganizationAsync</c> and is therefore reported by
    /// <c>IWorkspaceProvider</c> as "no organization selected".
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Organization name (display label).
    /// </summary>
    /// <remarks>
    /// Projected to <see cref="Title"/> for <c>IWorkspaceProvider.Organization</c>, so values that
    /// are blank or longer than <see cref="Title.MaxLength"/> graphemes cannot be represented
    /// faithfully. The projection never throws: blank becomes an empty title, longer text is cut at
    /// the grapheme boundary. Keep the legal entity name in <see cref="FullName"/> and put a short
    /// display label here if you need the switcher to read correctly.
    /// </remarks>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Email kontaktowy, np. do faktur
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Nazwa prawna
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Numer identyfikacji podatkowej (np. VAT ID)
    /// </summary>
    public string TaxIdentification { get; set; } = string.Empty;

    /// <summary>
    /// Kraj siedziby firmy
    /// </summary>
    public string Country { get; set; } = string.Empty;

    /// <summary>
    /// Stan (State), Region lub Województwo
    /// </summary>
    public string Region { get; set; } = string.Empty;

    /// <summary>
    /// Miasto
    /// </summary>
    public string City { get; set; } = string.Empty;

    /// <summary>
    /// Kod pocztowy
    /// </summary>
    public string PostalCode { get; set; } = string.Empty;

    /// <summary>
    /// Adres siedziby firmy - Adres linia 1
    /// </summary>
    public string AddressLine1 { get; set; } = string.Empty;

    /// <summary>
    /// Adres siedziby firmy - Adres linia 2
    /// </summary>
    public string AddressLine2 { get; set; } = string.Empty;

    /// <summary>
    /// Date of creation
    /// </summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}