using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Zonit.Extensions.Website;

public static class ProxyServiceExtensions
{
    public static IServiceCollection AddProxyService(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        using (var serviceProvider = services.BuildServiceProvider())
        {
            var websiteOptions = serviceProvider.GetRequiredService<IOptions<WebsiteOptions>>();

            if (websiteOptions.Value.Proxy)
            {
                services.Configure<ForwardedHeadersOptions>(options =>
                {
                    options.ForwardedHeaders =
                        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                });
            }
        }

        return services;
    }

    public static IApplicationBuilder UseProxy(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        using (var scope = app.ApplicationServices.CreateScope())
        {
            var serviceProvider = scope.ServiceProvider;
            var websiteOptions = serviceProvider.GetRequiredService<IOptions<WebsiteOptions>>().Value;

            if (websiteOptions.Proxy)
            {
                app.UseForwardedHeaders(new ForwardedHeadersOptions
                {
                    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
                });
            }
        }

        return app;
    }

}