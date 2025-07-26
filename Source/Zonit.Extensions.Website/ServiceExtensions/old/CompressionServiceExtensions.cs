using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;
using System.IO.Compression;

namespace Zonit.Extensions.Website;

public static class CompressionServiceExtensions
{
    public static IServiceCollection AddCompressionService(this IServiceCollection services)
    {
        services.Configure<BrotliCompressionProviderOptions>(o =>
        {
            o.Level = CompressionLevel.Optimal;
        });
        services.Configure<GzipCompressionProviderOptions>(o =>
        {
            o.Level = CompressionLevel.Optimal;
        });
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat([
                    "application/javascript",
                    "application/json",
                    "application/xml",
                    "application/xhtml+xml",
                    "application/octet-stream",
                    "application/wasm",
                    "application/font-woff2",
                    "text/css",
                    "text/json",
                    "text/plain",
                    "text/html",
                    "text/plain",
                    "text/xml",
                    "image/jpeg",
                    "image/png",
                    "image/webp",
                    "image/svg+xml",
                    "image/x-icon",
                    "image/svg+xml",
                    "font/woff2",
                ]);
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
        });

        return services;
    }
}