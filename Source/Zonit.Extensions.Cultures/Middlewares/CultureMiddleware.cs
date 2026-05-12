using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System.Globalization;
using Zonit.Extensions.Cultures.Options;
using Zonit.Extensions.Cultures.Services;

namespace Zonit.Extensions.Cultures.Middlewares;

internal class CultureMiddleware(RequestDelegate next, IOptions<CultureOption> settings)
{
    private readonly RequestDelegate _next = next;
    private readonly CultureOption _settings = settings.Value;

    private static string NormalizeCultureCode(string? culture, string defaultCulture)
    {
        if (string.IsNullOrWhiteSpace(culture))
            return defaultCulture;

        try
        {
            var cultureInfo = CultureInfo.GetCultureInfo(culture);
            return cultureInfo.Name.ToLowerInvariant();
        }
        catch (CultureNotFoundException)
        {
            return defaultCulture;
        }
    }

    private static bool IsSupportedCulture(string culture, string[] supportedCultures)
    {
        return supportedCultures.Any(supported => 
            string.Equals(supported, culture, StringComparison.OrdinalIgnoreCase));
    }

    private void SetCultureInfo(string culture, ICultureManager cultureManager, HttpContext httpContext)
    {
        try
        {
            var normalizedCulture = NormalizeCultureCode(culture, _settings.DefaultCulture);
            
            // Validate culture is supported
            if (!IsSupportedCulture(normalizedCulture, _settings.SupportedCultures))
            {
                normalizedCulture = NormalizeCultureCode(_settings.DefaultCulture, "en-us");
            }

            var cultureInfo = CultureInfo.GetCultureInfo(normalizedCulture);
            
            CultureInfo.CurrentCulture = cultureInfo;
            CultureInfo.CurrentUICulture = cultureInfo;
            
            cultureManager.SetCulture(normalizedCulture);
            
            // Set cookie for persistence
            httpContext.Response.Cookies.Append("Culture", normalizedCulture, 
                new CookieOptions { Expires = DateTime.UtcNow.AddYears(1) });
        }
        catch (Exception)
        {
            // Log exception in production
            // Fallback to default culture
            var defaultCulture = NormalizeCultureCode(_settings.DefaultCulture, "en-us");
            var defaultCultureInfo = CultureInfo.GetCultureInfo(defaultCulture);
            
            CultureInfo.CurrentCulture = defaultCultureInfo;
            CultureInfo.CurrentUICulture = defaultCultureInfo;
            
            cultureManager.SetCulture(defaultCulture);
        }
    }

    public Task Invoke(HttpContext httpContext, DetectCultureService detectCultureService, ICultureManager cultureManager)
    {
        if (string.IsNullOrEmpty(httpContext.Request.Path.Value))
            return _next(httpContext);

        try
        {
            var match = detectCultureService.GetUrl(httpContext.Request.Path.Value);

            // Handle URL-based culture detection
            if (match != null)
            {
                var requestedCulture = NormalizeCultureCode(match.Culture, _settings.DefaultCulture);
                
                // Validate requested culture
                if (IsSupportedCulture(requestedCulture, _settings.SupportedCultures))
                {
                    // Only update if culture is different
                    var currentCulture = NormalizeCultureCode(CultureInfo.CurrentCulture.Name, _settings.DefaultCulture);
                    if (!string.Equals(currentCulture, requestedCulture, StringComparison.OrdinalIgnoreCase))
                    {
                        SetCultureInfo(requestedCulture, cultureManager, httpContext);
                    }

                    // Update request path
                    httpContext.Request.Path = $"/{match.Url}";
                }
                else
                {
                    // Redirect to default culture version or return 404
                    // For now, continue with default culture
                    SetCultureInfo(_settings.DefaultCulture, cultureManager, httpContext);
                }
            }
            else
            {
                // Handle culture from cookie or browser preferences
                var cultureFromCookie = httpContext.Request.Cookies["Culture"];
                
                if (!string.IsNullOrWhiteSpace(cultureFromCookie))
                {
                    SetCultureInfo(cultureFromCookie, cultureManager, httpContext);
                }
                else
                {
                    // Use browser preferred language or default
                    var preferredLanguage = httpContext.Request.GetTypedHeaders()
                        .AcceptLanguage?.FirstOrDefault()?.Value.ToString();
                    
                    var cultureToUse = preferredLanguage ?? _settings.DefaultCulture;
                    SetCultureInfo(cultureToUse, cultureManager, httpContext);
                }
            }
        }
        catch (Exception)
        {
            // Log exception in production
            // Fallback to default culture
            SetCultureInfo(_settings.DefaultCulture, cultureManager, httpContext);
        }

        return _next(httpContext);
    }
}