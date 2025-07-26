//using Microsoft.AspNetCore.Builder;
//using Microsoft.AspNetCore.Localization.Routing;
//using Microsoft.AspNetCore.Localization;
//using Microsoft.Extensions.DependencyInjection;
//using System.Globalization;

//namespace Zonit.Extensions.Website;

//public static class CultureServiceExtensions
//{
//    private static readonly CultureInfo[] configureOptions = [
//            new CultureInfo("pl-pl"),
//            new CultureInfo("en-us"),
//            new CultureInfo("en-gb"),
//            new CultureInfo("es-es"),
//            new CultureInfo("hi-in"),
//            new CultureInfo("ar-sa"),
//            new CultureInfo("ru-ru"),
//            new CultureInfo("de-de"),
//            new CultureInfo("it-it"),
//            new CultureInfo("fr-fr"),
//        ];

//    public static IServiceCollection AddCultureService(this IServiceCollection services)
//    {
//        services.AddLocalization(opts => opts.ResourcesPath = "Resources");
//        services.Configure<RequestLocalizationOptions>(options =>
//        {
//            options.DefaultRequestCulture = new RequestCulture("pl-pl");
//            options.SupportedCultures = configureOptions;
//            options.SupportedUICultures = configureOptions;
//            options.RequestCultureProviders.Insert(0, new RouteDataRequestCultureProvider());
//        });

//        return services;
//    }
//}
