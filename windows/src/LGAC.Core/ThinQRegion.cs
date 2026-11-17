namespace LGAC.Core;

public static class ThinQRegion
{
    private static readonly (string Region, HashSet<string> Countries)[] Table =
    [
        ("kic", new HashSet<string>
        {
            "AU", "BD", "CN", "HK", "ID", "IN", "JP", "KH", "KR", "LA", "LK", "MM", "MY", "NP", "NZ", "PH",
            "SG", "TH", "TW", "VN",
        }),
        ("aic", new HashSet<string>
        {
            "AG", "AR", "AW", "BB", "BO", "BR", "BS", "BZ", "CA", "CL", "CO", "CR", "CU", "DM", "DO", "EC",
            "GD", "GT", "GY", "HN", "HT", "JM", "KN", "LC", "MX", "NI", "PA", "PE", "PR", "PY", "SR", "SV",
            "TT", "US", "UY", "VC", "VE",
        }),
        ("eic", new HashSet<string>
        {
            "AE", "AF", "AL", "AM", "AO", "AT", "AZ", "BA", "BE", "BF", "BG", "BH", "BJ", "BY", "CD", "CF",
            "CG", "CH", "CI", "CM", "CV", "CY", "CZ", "DE", "DJ", "DK", "DZ", "EE", "EG", "ES", "ET", "FI",
            "FR", "GA", "GB", "GE", "GH", "GM", "GN", "GQ", "GR", "HR", "HU", "IE", "IL", "IQ", "IR", "IS",
            "IT", "JO", "KE", "KG", "KW", "KZ", "LB", "LR", "LT", "LU", "LV", "LY", "MA", "MD", "ME", "MK",
            "ML", "MR", "MT", "MU", "MW", "NE", "NG", "NL", "NO", "OM", "PK", "PL", "PS", "PT", "QA", "RO",
            "RS", "RU", "RW", "SA", "SD", "SE", "SI", "SK", "SL", "SN", "SO", "ST", "SY", "TD", "TG", "TN",
            "TR", "TZ", "UA", "UG", "UZ", "XK", "YE", "ZA", "ZM",
        }),
    ];

    public static IReadOnlyList<string> SupportedCountries { get; } = Table
        .SelectMany(entry => entry.Countries)
        .Order(StringComparer.Ordinal)
        .ToList();

    public static string? RegionFor(string country)
    {
        var code = country.ToUpperInvariant();
        foreach (var (region, countries) in Table)
        {
            if (countries.Contains(code))
            {
                return region;
            }
        }

        return null;
    }

    public static Uri? BaseUrlFor(string country)
    {
        var region = RegionFor(country);
        return region is null ? null : new Uri($"https://api-{region}.lgthinq.com/");
    }
}
