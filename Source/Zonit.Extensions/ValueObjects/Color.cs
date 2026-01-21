using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Zonit.Extensions;

/// <summary>
/// Represents a color stored in OKLCH format for maximum precision and perceptual uniformity.
/// OKLCH is a modern color space that provides better color manipulation than traditional RGB/HSL.
/// </summary>
/// <remarks>
/// <para><strong>Why OKLCH?</strong></para>
/// <list type="bullet">
///   <item>Perceptually uniform - equal changes in values produce equal perceived color changes</item>
///   <item>Wider gamut support - can represent colors outside sRGB (P3, Rec2020)</item>
///   <item>Better for color manipulation - adjusting lightness doesn't affect hue</item>
///   <item>CSS Color Level 4 native support</item>
/// </list>
/// <para><strong>Conversion note:</strong> Converting from OKLCH to Hex/RGB may clip colors outside sRGB gamut.</para>
/// </remarks>
[JsonConverter(typeof(ColorJsonConverter))]
public readonly partial struct Color : IEquatable<Color>, IParsable<Color>, IFormattable
{
    /// <summary>
    /// Transparent (no color).
    /// </summary>
    public static readonly Color Transparent = new(0, 0, 0, 0);

    /// <summary>
    /// Black color.
    /// </summary>
    public static readonly Color Black = new(0, 0, 0, 1);

    /// <summary>
    /// White color.
    /// </summary>
    public static readonly Color White = new(1, 0, 0, 1);

    /// <summary>
    /// Lightness component (0-1). 0 = black, 1 = white.
    /// </summary>
    public double L { get; }

    /// <summary>
    /// Chroma component (0 to ~0.4). Higher values = more saturated.
    /// </summary>
    public double C { get; }

    /// <summary>
    /// Hue component (0-360 degrees). Red ≈ 30°, Yellow ≈ 110°, Green ≈ 145°, Blue ≈ 265°.
    /// </summary>
    public double H { get; }

    /// <summary>
    /// Alpha/opacity component (0-1). 0 = transparent, 1 = opaque.
    /// </summary>
    public double Alpha { get; }

    /// <summary>
    /// Indicates whether the color has transparency (alpha &lt; 1).
    /// </summary>
    public bool HasAlpha => Alpha < 1;

    /// <summary>
    /// Creates a new color from OKLCH components.
    /// </summary>
    /// <param name="l">Lightness (0-1)</param>
    /// <param name="c">Chroma (0 to ~0.4)</param>
    /// <param name="h">Hue (0-360 degrees)</param>
    /// <param name="alpha">Alpha/opacity (0-1), defaults to 1 (opaque)</param>
    public Color(double l, double c, double h, double alpha = 1)
    {
        L = Math.Clamp(l, 0, 1);
        C = Math.Max(0, c); // Chroma can theoretically exceed 0.4 for wide gamut
        H = NormalizeHue(h);
        Alpha = Math.Clamp(alpha, 0, 1);
    }

    private static double NormalizeHue(double h)
    {
        h %= 360;
        return h < 0 ? h + 360 : h;
    }

    #region Conversion Properties

    /// <summary>
    /// Gets the color as a hex string (#RRGGBB or #RRGGBBAA if alpha &lt; 1).
    /// </summary>
    /// <remarks>Colors outside sRGB gamut will be clipped.</remarks>
    public string Hex
    {
        get
        {
            var (r, g, b) = ToRgbComponents();
            return Alpha < 1
                ? $"#{r:X2}{g:X2}{b:X2}{(int)Math.Round(Alpha * 255):X2}"
                : $"#{r:X2}{g:X2}{b:X2}";
        }
    }

    /// <summary>
    /// Gets the color as RGB tuple (0-255 for each component).
    /// </summary>
    /// <remarks>Colors outside sRGB gamut will be clipped.</remarks>
    public (byte R, byte G, byte B) Rgb => ToRgbComponents();

    /// <summary>
    /// Gets the color as RGBA tuple (0-255 for RGB, 0-1 for Alpha).
    /// </summary>
    public (byte R, byte G, byte B, double A) Rgba
    {
        get
        {
            var (r, g, b) = ToRgbComponents();
            return (r, g, b, Alpha);
        }
    }

    /// <summary>
    /// Gets the color as CSS rgb() or rgba() string.
    /// </summary>
    public string CssRgb
    {
        get
        {
            var (r, g, b) = ToRgbComponents();
            return Alpha < 1
                ? $"rgba({r}, {g}, {b}, {Alpha.ToString("0.##", CultureInfo.InvariantCulture)})"
                : $"rgb({r}, {g}, {b})";
        }
    }

    /// <summary>
    /// Gets the color as CSS oklch() string (native format).
    /// </summary>
    public string CssOklch => Alpha < 1
        ? $"oklch({(L * 100).ToString("0.##", CultureInfo.InvariantCulture)}% {C.ToString("0.####", CultureInfo.InvariantCulture)} {H.ToString("0.##", CultureInfo.InvariantCulture)} / {Alpha.ToString("0.##", CultureInfo.InvariantCulture)})"
        : $"oklch({(L * 100).ToString("0.##", CultureInfo.InvariantCulture)}% {C.ToString("0.####", CultureInfo.InvariantCulture)} {H.ToString("0.##", CultureInfo.InvariantCulture)})";

    /// <summary>
    /// Gets the color as HSL tuple (H: 0-360, S: 0-1, L: 0-1).
    /// </summary>
    public (double H, double S, double L) Hsl
    {
        get
        {
            var (r, g, b) = ToRgbComponents();
            return RgbToHsl(r, g, b);
        }
    }

    /// <summary>
    /// Gets the color as CSS hsl() or hsla() string.
    /// </summary>
    public string CssHsl
    {
        get
        {
            var (h, s, l) = Hsl;
            return Alpha < 1
                ? $"hsla({h.ToString("0.#", CultureInfo.InvariantCulture)}, {(s * 100).ToString("0.#", CultureInfo.InvariantCulture)}%, {(l * 100).ToString("0.#", CultureInfo.InvariantCulture)}%, {Alpha.ToString("0.##", CultureInfo.InvariantCulture)})"
                : $"hsl({h.ToString("0.#", CultureInfo.InvariantCulture)}, {(s * 100).ToString("0.#", CultureInfo.InvariantCulture)}%, {(l * 100).ToString("0.#", CultureInfo.InvariantCulture)}%)";
        }
    }

    #endregion

    #region Factory Methods

    /// <summary>
    /// Creates a color from a hex string.
    /// </summary>
    /// <param name="hex">Hex color string (#RGB, #RGBA, #RRGGBB, or #RRGGBBAA)</param>
    public static Color FromHex(string hex)
    {
        if (!TryFromHex(hex, out var color))
            throw new FormatException($"Invalid hex color format: '{hex}'");
        return color;
    }

    /// <summary>
    /// Tries to create a color from a hex string.
    /// </summary>
    public static bool TryFromHex(string? hex, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(hex))
            return false;

        hex = hex.TrimStart('#');

        byte r, g, b;
        byte a = 255;

        try
        {
            if (hex.Length == 3) // #RGB
            {
                r = Convert.ToByte(new string(hex[0], 2), 16);
                g = Convert.ToByte(new string(hex[1], 2), 16);
                b = Convert.ToByte(new string(hex[2], 2), 16);
            }
            else if (hex.Length == 4) // #RGBA
            {
                r = Convert.ToByte(new string(hex[0], 2), 16);
                g = Convert.ToByte(new string(hex[1], 2), 16);
                b = Convert.ToByte(new string(hex[2], 2), 16);
                a = Convert.ToByte(new string(hex[3], 2), 16);
            }
            else if (hex.Length == 6) // #RRGGBB
            {
                r = Convert.ToByte(hex[..2], 16);
                g = Convert.ToByte(hex[2..4], 16);
                b = Convert.ToByte(hex[4..6], 16);
            }
            else if (hex.Length == 8) // #RRGGBBAA
            {
                r = Convert.ToByte(hex[..2], 16);
                g = Convert.ToByte(hex[2..4], 16);
                b = Convert.ToByte(hex[4..6], 16);
                a = Convert.ToByte(hex[6..8], 16);
            }
            else
            {
                return false;
            }
        }
        catch
        {
            return false;
        }

        color = FromRgb(r, g, b, a / 255.0);
        return true;
    }

    /// <summary>
    /// Creates a color from RGB components (0-255).
    /// </summary>
    public static Color FromRgb(byte r, byte g, byte b, double alpha = 1)
    {
        var (l, c, h) = RgbToOklch(r, g, b);
        return new Color(l, c, h, alpha);
    }

    /// <summary>
    /// Creates a color from RGB components (0-1).
    /// </summary>
    public static Color FromRgbNormalized(double r, double g, double b, double alpha = 1)
    {
        var (l, c, h) = RgbToOklch(
            (byte)Math.Round(Math.Clamp(r, 0, 1) * 255),
            (byte)Math.Round(Math.Clamp(g, 0, 1) * 255),
            (byte)Math.Round(Math.Clamp(b, 0, 1) * 255));
        return new Color(l, c, h, alpha);
    }

    /// <summary>
    /// Creates a color from HSL components.
    /// </summary>
    /// <param name="h">Hue (0-360)</param>
    /// <param name="s">Saturation (0-1)</param>
    /// <param name="l">Lightness (0-1)</param>
    /// <param name="alpha">Alpha (0-1)</param>
    public static Color FromHsl(double h, double s, double l, double alpha = 1)
    {
        var (r, g, b) = HslToRgb(h, s, l);
        return FromRgb(r, g, b, alpha);
    }

    /// <summary>
    /// Creates a color from OKLCH components directly.
    /// </summary>
    public static Color FromOklch(double l, double c, double h, double alpha = 1)
        => new(l, c, h, alpha);

    #endregion

    #region Color Manipulation

    /// <summary>
    /// Returns a new color with adjusted lightness.
    /// </summary>
    /// <param name="amount">Amount to add (-1 to 1). Positive = lighter, negative = darker.</param>
    public Color WithLightness(double amount) => new(L + amount, C, H, Alpha);

    /// <summary>
    /// Returns a new color with specified lightness.
    /// </summary>
    /// <param name="lightness">New lightness value (0-1).</param>
    public Color SetLightness(double lightness) => new(lightness, C, H, Alpha);

    /// <summary>
    /// Returns a new color with adjusted chroma (saturation).
    /// </summary>
    /// <param name="amount">Amount to add. Positive = more saturated, negative = less saturated.</param>
    public Color WithChroma(double amount) => new(L, C + amount, H, Alpha);

    /// <summary>
    /// Returns a new color with specified chroma.
    /// </summary>
    /// <param name="chroma">New chroma value (0 to ~0.4).</param>
    public Color SetChroma(double chroma) => new(L, chroma, H, Alpha);

    /// <summary>
    /// Returns a new color with rotated hue.
    /// </summary>
    /// <param name="degrees">Degrees to rotate (can be negative).</param>
    public Color WithHue(double degrees) => new(L, C, H + degrees, Alpha);

    /// <summary>
    /// Returns a new color with specified hue.
    /// </summary>
    /// <param name="hue">New hue value (0-360).</param>
    public Color SetHue(double hue) => new(L, C, hue, Alpha);

    /// <summary>
    /// Returns a new color with specified alpha.
    /// </summary>
    /// <param name="alpha">New alpha value (0-1).</param>
    public Color WithAlpha(double alpha) => new(L, C, H, alpha);

    /// <summary>
    /// Returns a lighter version of the color.
    /// </summary>
    /// <param name="amount">Amount to lighten (0-1). Default is 0.1.</param>
    public Color Lighten(double amount = 0.1) => WithLightness(amount);

    /// <summary>
    /// Returns a darker version of the color.
    /// </summary>
    /// <param name="amount">Amount to darken (0-1). Default is 0.1.</param>
    public Color Darken(double amount = 0.1) => WithLightness(-amount);

    /// <summary>
    /// Returns a more saturated version of the color.
    /// </summary>
    /// <param name="amount">Amount to increase saturation. Default is 0.05.</param>
    public Color Saturate(double amount = 0.05) => WithChroma(amount);

    /// <summary>
    /// Returns a less saturated version of the color.
    /// </summary>
    /// <param name="amount">Amount to decrease saturation. Default is 0.05.</param>
    public Color Desaturate(double amount = 0.05) => WithChroma(-amount);

    /// <summary>
    /// Returns the complementary color (180° hue rotation).
    /// </summary>
    public Color Complementary => WithHue(180);

    /// <summary>
    /// Returns a grayscale version of the color (chroma = 0).
    /// </summary>
    public Color Grayscale => new(L, 0, H, Alpha);

    /// <summary>
    /// Mixes this color with another color.
    /// </summary>
    /// <param name="other">The color to mix with.</param>
    /// <param name="amount">Mix ratio (0 = this color, 1 = other color). Default is 0.5.</param>
    public Color Mix(Color other, double amount = 0.5)
    {
        amount = Math.Clamp(amount, 0, 1);
        return new Color(
            L + (other.L - L) * amount,
            C + (other.C - C) * amount,
            InterpolateHue(H, other.H, amount),
            Alpha + (other.Alpha - Alpha) * amount);
    }

    private static double InterpolateHue(double h1, double h2, double amount)
    {
        var diff = h2 - h1;
        if (Math.Abs(diff) > 180)
        {
            if (diff > 0) h1 += 360;
            else h2 += 360;
        }
        return NormalizeHue(h1 + (h2 - h1) * amount);
    }

    #endregion

    #region Color Conversions (Internal)

    private (byte R, byte G, byte B) ToRgbComponents()
    {
        // OKLCH -> OKLab
        var a = C * Math.Cos(H * Math.PI / 180);
        var b = C * Math.Sin(H * Math.PI / 180);

        // OKLab -> Linear RGB
        var l_ = L + 0.3963377774 * a + 0.2158037573 * b;
        var m_ = L - 0.1055613458 * a - 0.0638541728 * b;
        var s_ = L - 0.0894841775 * a - 1.2914855480 * b;

        var l = l_ * l_ * l_;
        var m = m_ * m_ * m_;
        var s = s_ * s_ * s_;

        var rLinear = +4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s;
        var gLinear = -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s;
        var bLinear = -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s;

        // Linear RGB -> sRGB
        static double ToSrgb(double c) => c <= 0.0031308 ? 12.92 * c : 1.055 * Math.Pow(c, 1.0 / 2.4) - 0.055;

        return (
            (byte)Math.Round(Math.Clamp(ToSrgb(rLinear), 0, 1) * 255),
            (byte)Math.Round(Math.Clamp(ToSrgb(gLinear), 0, 1) * 255),
            (byte)Math.Round(Math.Clamp(ToSrgb(bLinear), 0, 1) * 255)
        );
    }

    private static (double L, double C, double H) RgbToOklch(byte r, byte g, byte b)
    {
        // sRGB -> Linear RGB
        static double ToLinear(double c)
        {
            c /= 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        var rLinear = ToLinear(r);
        var gLinear = ToLinear(g);
        var bLinear = ToLinear(b);

        // Linear RGB -> OKLab
        var l = Math.Cbrt(0.4122214708 * rLinear + 0.5363325363 * gLinear + 0.0514459929 * bLinear);
        var m = Math.Cbrt(0.2119034982 * rLinear + 0.6806995451 * gLinear + 0.1073969566 * bLinear);
        var s = Math.Cbrt(0.0883024619 * rLinear + 0.2817188376 * gLinear + 0.6299787005 * bLinear);

        var labL = 0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s;
        var labA = 1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s;
        var labB = 0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s;

        // OKLab -> OKLCH
        var c = Math.Sqrt(labA * labA + labB * labB);
        var h = Math.Atan2(labB, labA) * 180 / Math.PI;
        if (h < 0) h += 360;

        return (labL, c, h);
    }

    private static (byte R, byte G, byte B) HslToRgb(double h, double s, double l)
    {
        h = NormalizeHue(h);
        s = Math.Clamp(s, 0, 1);
        l = Math.Clamp(l, 0, 1);

        double r, g, b;

        if (s == 0)
        {
            r = g = b = l;
        }
        else
        {
            static double HueToRgb(double p, double q, double t)
            {
                if (t < 0) t += 1;
                if (t > 1) t -= 1;
                if (t < 1.0 / 6) return p + (q - p) * 6 * t;
                if (t < 1.0 / 2) return q;
                if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
                return p;
            }

            var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            var p = 2 * l - q;

            r = HueToRgb(p, q, h / 360 + 1.0 / 3);
            g = HueToRgb(p, q, h / 360);
            b = HueToRgb(p, q, h / 360 - 1.0 / 3);
        }

        return (
            (byte)Math.Round(r * 255),
            (byte)Math.Round(g * 255),
            (byte)Math.Round(b * 255)
        );
    }

    private static (double H, double S, double L) RgbToHsl(byte r, byte g, byte b)
    {
        var rNorm = r / 255.0;
        var gNorm = g / 255.0;
        var bNorm = b / 255.0;

        var max = Math.Max(Math.Max(rNorm, gNorm), bNorm);
        var min = Math.Min(Math.Min(rNorm, gNorm), bNorm);
        var l = (max + min) / 2;

        if (max == min)
        {
            return (0, 0, l);
        }

        var d = max - min;
        var s = l > 0.5 ? d / (2 - max - min) : d / (max + min);

        double h;
        if (max == rNorm)
            h = ((gNorm - bNorm) / d + (gNorm < bNorm ? 6 : 0)) / 6;
        else if (max == gNorm)
            h = ((bNorm - rNorm) / d + 2) / 6;
        else
            h = ((rNorm - gNorm) / d + 4) / 6;

        return (h * 360, s, l);
    }

    #endregion

    #region Equality & Comparison

    /// <inheritdoc />
    public bool Equals(Color other) =>
        Math.Abs(L - other.L) < 0.0001 &&
        Math.Abs(C - other.C) < 0.0001 &&
        Math.Abs(H - other.H) < 0.01 &&
        Math.Abs(Alpha - other.Alpha) < 0.0001;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Color other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(
        Math.Round(L, 4),
        Math.Round(C, 4),
        Math.Round(H, 2),
        Math.Round(Alpha, 4));

    /// <summary>Equality operator.</summary>
    public static bool operator ==(Color left, Color right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(Color left, Color right) => !left.Equals(right);

    #endregion

    #region String Conversion & Parsing

    /// <summary>
    /// Returns the color as OKLCH CSS string (native format).
    /// </summary>
    public override string ToString() => CssOklch;

    /// <summary>
    /// Returns the color formatted according to the specified format.
    /// </summary>
    /// <param name="format">Format: "hex", "rgb", "hsl", "oklch" (default), or null for default.</param>
    /// <param name="formatProvider">Format provider (not used).</param>
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        return format?.ToLowerInvariant() switch
        {
            "hex" or "x" => Hex,
            "rgb" => CssRgb,
            "hsl" => CssHsl,
            "oklch" or null or "" => CssOklch,
            _ => CssOklch
        };
    }

    /// <summary>
    /// Parses a color from various string formats (hex, rgb, hsl, oklch).
    /// </summary>
    public static Color Parse(string s, IFormatProvider? provider)
    {
        if (TryParse(s, provider, out var result))
            return result;
        throw new FormatException($"Cannot parse '{s}' as Color.");
    }

    /// <summary>
    /// Tries to parse a color from various string formats.
    /// </summary>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Color result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(s))
            return false;

        s = s.Trim();

        // Hex format
        if (s.StartsWith('#'))
            return TryFromHex(s, out result);

        // RGB format
        var rgbMatch = RgbRegex().Match(s);
        if (rgbMatch.Success)
        {
            if (byte.TryParse(rgbMatch.Groups[1].Value, out var r) &&
                byte.TryParse(rgbMatch.Groups[2].Value, out var g) &&
                byte.TryParse(rgbMatch.Groups[3].Value, out var b))
            {
                var alpha = rgbMatch.Groups[4].Success && double.TryParse(rgbMatch.Groups[4].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var a)
                    ? a
                    : 1.0;
                result = FromRgb(r, g, b, alpha);
                return true;
            }
        }

        // HSL format
        var hslMatch = HslRegex().Match(s);
        if (hslMatch.Success)
        {
            if (double.TryParse(hslMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var h) &&
                double.TryParse(hslMatch.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var sat) &&
                double.TryParse(hslMatch.Groups[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var l))
            {
                var alpha = hslMatch.Groups[4].Success && double.TryParse(hslMatch.Groups[4].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var a)
                    ? a
                    : 1.0;
                result = FromHsl(h, sat / 100, l / 100, alpha);
                return true;
            }
        }

        // OKLCH format
        var oklchMatch = OklchRegex().Match(s);
        if (oklchMatch.Success)
        {
            if (double.TryParse(oklchMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var lVal) &&
                double.TryParse(oklchMatch.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var c) &&
                double.TryParse(oklchMatch.Groups[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var hue))
            {
                var alpha = oklchMatch.Groups[4].Success && double.TryParse(oklchMatch.Groups[4].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var a)
                    ? a
                    : 1.0;
                // L can be percentage or 0-1
                var l = oklchMatch.Groups[1].Value.Contains('%') ? lVal / 100 : lVal;
                result = FromOklch(l, c, hue, alpha);
                return true;
            }
        }

        return false;
    }

    [GeneratedRegex(@"^rgba?\s*\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*(?:,\s*([\d.]+))?\s*\)$", RegexOptions.IgnoreCase)]
    private static partial Regex RgbRegex();

    [GeneratedRegex(@"^hsla?\s*\(\s*([\d.]+)\s*,\s*([\d.]+)%?\s*,\s*([\d.]+)%?\s*(?:,\s*([\d.]+))?\s*\)$", RegexOptions.IgnoreCase)]
    private static partial Regex HslRegex();

    [GeneratedRegex(@"^oklch\s*\(\s*([\d.]+)%?\s+(\d*\.?\d+)\s+([\d.]+)\s*(?:/\s*([\d.]+))?\s*\)$", RegexOptions.IgnoreCase)]
    private static partial Regex OklchRegex();

    #endregion

    #region Implicit Conversions

    /// <summary>
    /// Converts a hex string to a Color.
    /// </summary>
    public static implicit operator Color(string hex) => FromHex(hex);

    /// <summary>
    /// Converts a Color to a hex string.
    /// </summary>
    public static implicit operator string(Color color) => color.Hex;

    #endregion
}

/// <summary>
/// JSON converter for Color value object. Stores as OKLCH string for full precision.
/// </summary>
public sealed class ColorJsonConverter : JsonConverter<Color>
{
    /// <inheritdoc />
    public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            return Color.TryParse(value, null, out var color) ? color : Color.Transparent;
        }
        return Color.Transparent;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.CssOklch);
    }
}
