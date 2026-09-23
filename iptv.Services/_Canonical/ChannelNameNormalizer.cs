using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace iptv.Services._Canonical;

/// <summary>
/// Shared, deterministic channel-name normalizer used for alias matching in the Channel Registry.
/// Rules (see AGENT_PROMPT §4): lower-case; fold Persian/Arabic digits; unify ي/ی and ك/ک; strip
/// ZWNJ/zero-width chars; drop punctuation ()[]-_.,/&amp;!|; collapse spaces; strip quality/noise
/// tokens (hd fhd uhd 4k sd hevc h265 backup bk live); but keep +/plus as a distinguishing token
/// so "GEM TV" and "GEM TV +" never collapse together.
/// </summary>
public static partial class ChannelNameNormalizer
{
    private const string PlusToken = "plus";

    // Whole-word quality/noise tokens to drop (kept in sync with AGENT_PROMPT §4).
    [GeneratedRegex(@"\b(hd|fhd|uhd|4k|sd|hevc|h265|h\.265|backup|bk|live)\b", RegexOptions.IgnoreCase)]
    private static partial Regex NoiseTokenPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();

    // Punctuation that carries no distinguishing meaning. NOTE: '+' is intentionally excluded.
    [GeneratedRegex(@"[()\[\]\-_.,/&!|:'""«»`~*^%$#@?;]")]
    private static partial Regex PunctuationPattern();

    public static string Normalize(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var sb = new StringBuilder(name.Length);

        foreach (var ch in name.Trim())
        {
            // Drop zero-width / directional marks (ZWNJ etc.).
            if (ch is '‌' or '​' or '‍' or '﻿' or '‎' or '‏')
                continue;

            var c = char.ToLowerInvariant(ch);

            // Arabic/Persian/Eastern-Arabic digit folding -> latin.
            var folded = FoldDigit(c);
            if (folded != '\0')
            {
                sb.Append(folded);
                continue;
            }

            // Letter unification.
            c = c switch
            {
                'ي' or 'ﻱ' or 'ﻲ' => 'ی',
                'ك' or 'ﻙ' or 'ﻚ' => 'ک',
                'أ' or 'إ' or 'آ' or 'ٱ' => 'ا',
                'ة' => 'ه',
                _ => c
            };

            sb.Append(c);
        }

        var normalized = sb.ToString();

        // Unify the "+"/"plus" marker into a single distinguishing token BEFORE stripping punctuation.
        normalized = Regex.Replace(normalized, @"\bplus\b", $" {PlusToken} ", RegexOptions.IgnoreCase);
        normalized = normalized.Replace("+", $" {PlusToken} ");

        normalized = PunctuationPattern().Replace(normalized, " ");
        normalized = NoiseTokenPattern().Replace(normalized, " ");
        normalized = WhitespacePattern().Replace(normalized, " ").Trim();

        return normalized;
    }

    private static char FoldDigit(char c)
    {
        // Persian ۰-۹ (U+06F0..U+06F9) and Arabic ٠-٩ (U+0660..U+0669).
        if (c is >= '۰' and <= '۹')
            return (char)('0' + (c - '۰'));
        if (c is >= '٠' and <= '٩')
            return (char)('0' + (c - '٠'));
        return '\0';
    }

    // Exposed for callers that want a stable comparer.
    public static readonly StringComparer Comparer = StringComparer.Ordinal;
}
