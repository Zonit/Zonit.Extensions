using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Zonit.Extensions.Website;

public static class WebHostServiceExtensions
{
    public static IServiceCollection WebHostService(this IServiceCollection services)
    {
        using var serviceProvider = services.BuildServiceProvider();
        
        var websiteOptions = serviceProvider.GetRequiredService<IOptions<WebsiteOptions>>();

        if (websiteOptions?.Value.Url is not null)
        {
            var builder = serviceProvider.GetRequiredService<IWebHostBuilder>();
            builder.UseUrls(websiteOptions.Value.Url);
        }

        return services;
    }
}