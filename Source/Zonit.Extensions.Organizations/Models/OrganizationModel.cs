namespace Zonit.Extensions.Organizations;

public class OrganizationModel
{
    public Guid Id { get; set; }

    /// <summary>
    /// Organization name
    /// </summary>
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