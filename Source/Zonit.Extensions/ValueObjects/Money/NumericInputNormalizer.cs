using System.Buffers;

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
///
/// <para><b>Input length is hard-capped at <see cref="MaxInputLength"/> characters.</b>
/// This method is reachable from untrusted data — <c>MoneyJsonConverter.Read</c> /
/// <c>PriceJsonConverter.Read</c> and every ASP.NET model bind through
/// <see cref="IParsable{TSelf}"/> — so an unbounded buffer sized from the caller's string is a
/// remote denial of service. A <see cref="decimal"/> carries at most 29 significant digits, so
/// no legitimate numeric literal comes close to the cap; anything above it is rejected before a
/// single character is copied.</para>
/// </remarks>
internal static class NumericInputNormalizer
{
    /// <summary>
    /// Longest input accepted. Generous by two orders of magnitude versus the 29 significant
    /// digits a <see cref="decimal"/> can hold, so it only ever rejects input that could not
    /// have parsed anyway — but it bounds the buffers below to a constant.
    /// </summary>
    private const int MaxInputLength = 512;

    /// <summary>
    /// Inputs at or below this length are normalized on the stack. Above it we rent, because
    /// two buffers live in the same frame and <c>StackOverflowException</c> is uncatchable —
    /// there is no recovering from getting this wrong at runtime.
    /// </summary>
    private const int StackAllocThreshold = 128;

    public static bool TryNormalize(string? input, out string normalized)
    {
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
            return false;

        // Gate BEFORE any buffer is reserved: a hostile ~400 KB JSON string would otherwise
        // size the scratch spans and kill the process outright rather than failing the parse.
        if (input.Length > MaxInputLength)
            return false;

        // Two scratch spans of input.Length each: one for the whitespace-stripped copy, one for
        // the separator-normalized output. They are carved out of a single buffer so the rented
        // path needs only one rent/return pair.
        char[]? rented = null;
        try
        {
            Span<char> scratch = input.Length <= StackAllocThreshold
                ? stackalloc char[StackAllocThreshold * 2]
                : (rented = ArrayPool<char>.Shared.Rent(input.Length * 2));

            return TryNormalizeCore(input, scratch, out normalized);
        }
        finally
        {
            if (rented is not null)
                ArrayPool<char>.Shared.Return(rented);
        }
    }

    private static bool TryNormalizeCore(string input, Span<char> scratch, out string normalized)
    {
        normalized = string.Empty;

        // Strip whitespace + underscores (occasional readability separators).
        var buffer = scratch[..input.Length];
        int len = 0;
        foreach (var c in input)
        {
            if (char.IsWhiteSpace(c) || c == '_')
                continue;
            buffer[len++] = c;
        }

        if (len == 0)
            return false;

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

        // Build "<digits-without-separators>.<fractional-digits>" in the second half of the
        // scratch buffer, which is guaranteed to be at least as long as the trimmed input.
        Span<char> output = scratch.Slice(input.Length, trimmed.Length);
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
