using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;
using Zonit.Extensions.Cultures.Options;

namespace Zonit.Extensions.Cultures.Services;

internal partial class DetectCultureService(IOptions<CultureOption> options)
{
    private readonly HashSet<string> _supportedCultures = options.Value.SupportedCultures.Select(c => c.ToLower()).ToHashSet();

    public record PathCulture(string Url, string Culture);

    public PathCulture? GetUrl(string adres)
    {
        var match = GetUrlRegex().Match(adres);

        //if(match.Success)
        //    return new PathCulture(match.Groups["url"].Value, match.Groups["culture"].Value.ToLower());

        if (match.Success)
        {
            var culture = match.Groups["culture"].Value.ToLower();

            // TODO: Zrób wsparcie dla "en", "pl" itp. Obecnie działa tylko dla "en-us", "pl-pl" itp.
            if (_supportedCultures.Contains(culture))
                return new PathCulture(match.Groups["url"].Value, culture);
        }

        return null;
    }

    [GeneratedRegex(@"^/(?<culture>[a-z]{2}(?:-[a-z]{2})?)/(?<url>.+)", options: RegexOptions.Compiled | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex GetUrlRegex();
}
