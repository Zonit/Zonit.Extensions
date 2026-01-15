namespace Zonit.Extensions.Website.MudBlazor.Converters;

/// <summary>
/// Pre-configured MudBlazor converters for Zonit.Extensions Value Objects.
/// Use these static instances in MudBlazor form components.
/// </summary>
/// <example>
/// <code>
/// &lt;MudTextField T="Title" @bind-Value="model.Title" Converter="ValueObjectConverters.Title" /&gt;
/// &lt;MudTextField T="Description" @bind-Value="model.Description" Converter="ValueObjectConverters.Description" /&gt;
/// &lt;MudTextField T="UrlSlug" @bind-Value="model.Slug" Converter="ValueObjectConverters.UrlSlug" /&gt;
/// </code>
/// </example>
public static class ValueObjectConverters
{
    /// <summary>
    /// Converter for <see cref="Title"/> Value Object.
    /// Automatically validates length constraints (1-60 characters).
    /// </summary>
    public static readonly global::MudBlazor.Converter<Title> Title = new ValueObjectConverter<Title>(
        getValue: t => t.Value,
        createFromString: s => new Title(s!),
        emptyValue: Extensions.Title.Empty
    );

    /// <summary>
    /// Converter for <see cref="Description"/> Value Object.
    /// Automatically validates length constraints (1-160 characters).
    /// </summary>
    public static readonly global::MudBlazor.Converter<Description> Description = new ValueObjectConverter<Description>(
        getValue: d => d.Value,
        createFromString: s => new Description(s!),
        emptyValue: Extensions.Description.Empty
    );

    /// <summary>
    /// Converter for <see cref="UrlSlug"/> Value Object.
    /// Automatically transforms input into URL-friendly slug.
    /// </summary>
    public static readonly global::MudBlazor.Converter<UrlSlug> UrlSlug = new ValueObjectConverter<UrlSlug>(
        getValue: u => u.Value,
        createFromString: s => new UrlSlug(s!),
        emptyValue: Extensions.UrlSlug.Empty
    );

    /// <summary>
    /// Converter for <see cref="Content"/> Value Object.
    /// For longer text content without strict length limits.
    /// </summary>
    public static readonly global::MudBlazor.Converter<Content> Content = new ValueObjectConverter<Content>(
        getValue: c => c.Value,
        createFromString: s => new Content(s!),
        emptyValue: Extensions.Content.Empty
    );

    /// <summary>
    /// Converter for <see cref="Url"/> Value Object.
    /// Validates URL format and structure.
    /// </summary>
    public static readonly global::MudBlazor.Converter<Url> Url = new ValueObjectConverter<Url>(
        getValue: u => u.Value,
        createFromString: s => new Url(s!),
        emptyValue: Extensions.Url.Empty
    );

    /// <summary>
    /// Converter for <see cref="Culture"/> Value Object.
    /// Validates culture code format.
    /// </summary>
    public static readonly global::MudBlazor.Converter<Culture> Culture = new ValueObjectConverter<Culture>(
        getValue: c => c.Value,
        createFromString: s => new Culture(s!),
        emptyValue: Extensions.Culture.Empty
    );
}
