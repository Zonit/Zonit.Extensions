using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.WebEncoders;
using System.Text.Encodings.Web;
using System.Text.Unicode;

namespace Zonit.Extensions.Website;

public static class TextEncodingServiceExtensions
{
    public static IServiceCollection AddTextEncodingService(this IServiceCollection services)
    {
        services.Configure<WebEncoderOptions>(options =>
        {
            options.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All);
        });

        return services;
    }
}