namespace Zonit.Extensions;

/// <summary>
/// Helper that converts a human-typed numeric string into a canonical, culture-free
/// representation ready to be fed into <see cref="decimal.TryParse(string, System.Globalization.NumberStyles, System.IFormatProvider, out decimal)"/>
/// with <see cref="System.Globalization.CultureInfo.InvariantCulture"/>.
/// </summary>
/// <remarks>
/// <para>The Zonit value objects <see cref="Money"/>, <see cref="Price"/> and any future
/// numeric VOs accept both <c>","</c> and <c>"."</c> as the decimal separator so that
/// inputs survive copy/paste between Polish (<c>"19,99"</c>) and US (<c>"19.99"</c>) hosts
/// without any culture awareness on the call site. The same rule applies to thousand
/// separators — they are dropped before parsing.</para>
///
/// <para>Heuristic:</para>
/// <list type="number">
///   <item>Strip whitespace and underscores (occasional readability separators).</item>
///   <item>If both separators are present, the <em>last</em> one is treated as the
///         decimal point; everything before it is digits + thousand separators which
///         get dropped.</item>
///   <item>If only a single separator type is present, it is taken as the decimal
///         point regardless of which character it is.</item>
///   <item>Multiple occurrences of a single separator (<c>"1.234.567"</c>) are treated
///         as thousand separators except the last one, which is the decimal point.
///         The grammar matches the conventions used by both <c>en-US</c> and
///         <c>pl-PL</c> users.</item>
/// </list>
///
/// <para>The output is always Invariant-culture-shaped: a single optional sign, digits,
/// optional <c>"."</c>, digits.</para>
///
/// <para><b>Known ambiguity (by design).</b> A single separator with exactly three digits
/// after it — e.g. <c>"1.234"</c> or <c>"1,234"</c> — is interpreted as
/// <i>integer.fraction</i> (1.234), <b>not</b> as <i>thousands</i> (1234). For Polish
/// users who write <c>"1.234"</c> meaning "tysiąc dwieście trzydzieści cztery" this is
/// counter-intuitive; for US users who write <c>"1.234"</c> meaning a 3-decimal value
/// (rare but legal for currencies like KWD/BHD) it is correct. The unambiguous form for
/// money is to always include the cents part — <c>"1.234,56"</c> or <c>"1,234.56"</c>
/// both round-trip to <c>1234.56</c>. Sanity table:</para>
///
/// <list type="table">
///   <listheader>
///     <term>Input</term><description>Output</description>
///   </listheader>
///   <item><term><c>"19,99"</c> / <c>"19.99"</c></term><description><c>19.99</c></description></item>
///   <item><term><c>"0,5"</c> / <c>"0.5"</c></term><description><c>0.5</c></description></item>
///   <item><term><c>"1234,56"</c></term><description><c>1234.56</c></description></item>
///   <item><term><c>"1.234,56"</c> / <c>"1,234.56"</c></term><description><c>1234.56</c></description></item>
///   <item><term><c>"1.234"</c> / <c>"1,234"</c></term><description><c>1.234</c> (NOT 1234 — see ambiguity note)</description></item>
///   <item><term><c>"-19,99"</c></term><description><c>-19.99</c></description></item>
/// </list>
/// </remarks>
internal static class NumericInputNormalizer
{
    public static bool TryNormalize(string? input, out string normalized)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            normalized = string.Empty;
            return false;
        }

        // Strip whitespace + underscores. Span<char> avoids extra allocations only for
        // the trivial path, so we just go with a single string allocation here — the
        // hot path for numeric input is small (< 32 chars) and the JIT collapses it.
        Span<char> buffer = stackalloc char[input.Length];
        int len = 0;
        foreach (var c in input)
        {
            if (char.IsWhiteSpace(c) || c == '_')
                continue;
            buffer[len++] = c;
        }

        if (len == 0)
        {
            normalized = string.Empty;
            return false;
        }

        var trimmed = buffer[..len];

        // Locate the last occurrence of either separator — that is the decimal point
        // in human-typed numbers. Everything before it is "integer part with optional
        // thousand separators" which we strip wholesale.
        int lastSep = -1;
        for (int i = trimmed.Length - 1; i >= 0; i--)
        {
            if (trimmed[i] == '.' || trimmed[i] == ',')
            {
                lastSep = i;
                break;
            }
        }

        if (lastSep < 0)
        {
            // No separator at all — pure integer, pass through.
            normalized = trimmed.ToString();
            return true;
        }

        // Build "<digits-without-separators>.<fractional-digits>".
        Span<char> output = stackalloc char[trimmed.Length];
        int o = 0;
        for (int i = 0; i < lastSep; i++)
        {
            var c = trimmed[i];
            if (c == '.' || c == ',') continue; // drop thousand separators
            output[o++] = c;
        }

        // Fractional part follows only if there is at least one digit after the
        // decimal point; otherwise we leave the value as the integer part.
        if (lastSep < trimmed.Length - 1)
        {
            output[o++] = '.';
            for (int i = lastSep + 1; i < trimmed.Length; i++)
                output[o++] = trimmed[i];
        }

        normalized = output[..o].ToString();
        return true;
    }
}
