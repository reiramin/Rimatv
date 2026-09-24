namespace iptv.Services._ChannelRegistry;

public sealed record CuratedCountryInfo(string Key, string Order, string DisplayEn, string DisplayFa);

/// <summary>
/// Curated country keys, their canonical ordering (AGENT_PROMPT §5.2) and display names.
/// </summary>
public static class CuratedCountries
{
    // Order matters: iran, iran-foreign, tr, az, ae, qa, af, in, iq, de, uz, tm, am, sy, us, es, sa, ca, pk, cn, ru, intl.
    public static readonly IReadOnlyList<CuratedCountryInfo> Ordered =
    [
        new("iran", "00", "Iran (domestic)", "ایران"),
        new("iran-foreign", "01", "Iranian (satellite/abroad)", "ایرانی خارج از کشور"),
        new("tr", "02", "Turkey", "ترکیه"),
        new("az", "03", "Azerbaijan", "آذربایجان"),
        new("ae", "04", "United Arab Emirates", "امارات"),
        new("qa", "05", "Qatar", "قطر"),
        new("af", "06", "Afghanistan", "افغانستان"),
        new("in", "07", "India", "هند"),
        new("iq", "08", "Iraq", "عراق"),
        new("de", "09", "Germany", "آلمان"),
        new("uz", "10", "Uzbekistan", "ازبکستان"),
        new("tm", "11", "Turkmenistan", "ترکمنستان"),
        new("am", "12", "Armenia", "ارمنستان"),
        new("sy", "13", "Syria", "سوریه"),
        new("us", "14", "United States", "آمریکا"),
        new("es", "15", "Spain", "اسپانیا"),
        new("sa", "16", "Saudi Arabia", "عربستان"),
        new("ca", "17", "Canada", "کانادا"),
        new("pk", "18", "Pakistan", "پاکستان"),
        new("cn", "19", "China", "چین"),
        new("ru", "20", "Russia", "روسیه"),
        new("intl", "21", "International", "بین‌المللی")
    ];

    private static readonly Dictionary<string, CuratedCountryInfo> ByKey =
        Ordered.ToDictionary(c => c.Key, StringComparer.OrdinalIgnoreCase);

    /// <summary>Sort key so unknown keys sort after the known ones, then alphabetically.</summary>
    public static string OrderKey(string country)
        => ByKey.TryGetValue(country ?? string.Empty, out var info)
            ? info.Order
            : $"99{country}";

    public static CuratedCountryInfo Info(string country)
        => ByKey.GetValueOrDefault(country ?? string.Empty)
           ?? new CuratedCountryInfo(country, "99", country, country);
}
