using System.Globalization;

namespace LGAC.Core;

public static class ACLabels
{
    private static readonly NumberFormatInfo DecimalComma = CreateDecimalComma();

    private static NumberFormatInfo CreateDecimalComma()
    {
        var format = (NumberFormatInfo)NumberFormatInfo.InvariantInfo.Clone();
        format.NumberDecimalSeparator = ",";
        return format;
    }

    public static string JobMode(string mode)
    {
        return mode switch
        {
            "COOL" => "Frio",
            "HEAT" => "Quente",
            "AIR_DRY" => "Seco",
            "FAN" => "Ventilar",
            "AUTO" => "Auto",
            _ => Humanize(mode),
        };
    }

    public static string WindStrength(string value)
    {
        return value switch
        {
            "LOW" => "Baixa",
            "MID" => "Média",
            "HIGH" => "Alta",
            "AUTO" => "Auto",
            _ => Humanize(value),
        };
    }

    public static string Duration(int minutes)
    {
        var hours = minutes / 60;
        var rest = minutes % 60;
        if (hours == 0)
        {
            return $"{rest} min";
        }

        if (rest == 0)
        {
            return $"{hours} h";
        }

        return $"{hours}h {rest}min";
    }

    public static string Temperature(double value)
    {
        return value.ToString("0.#", DecimalComma) + "°";
    }

    public static string Humanize(string raw)
    {
        var words = string.Join(' ', raw.ToLowerInvariant().Split('_', StringSplitOptions.RemoveEmptyEntries));
        return words.Length == 0 ? words : char.ToUpperInvariant(words[0]) + words[1..];
    }
}
