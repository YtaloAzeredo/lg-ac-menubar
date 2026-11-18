namespace LGAC.Linux.Platform;

public static class ElapsedText
{
    private const long SecondsPerMinute = 60;
    private const long SecondsPerHour = 3600;
    private const long SecondsPerDay = 86400;

    public static string Format(TimeSpan elapsed)
    {
        var total = (long)Math.Max(0, Math.Floor(elapsed.TotalSeconds));
        if (total < SecondsPerMinute)
        {
            return $"{total} seg";
        }

        if (total < SecondsPerHour)
        {
            return $"{total / SecondsPerMinute} min e {total % SecondsPerMinute} seg";
        }

        if (total < SecondsPerDay)
        {
            return $"{total / SecondsPerHour} h e {total % SecondsPerHour / SecondsPerMinute} min";
        }

        var days = total / SecondsPerDay;
        var hours = total % SecondsPerDay / SecondsPerHour;
        var dayText = days == 1 ? "1 dia" : $"{days} dias";
        return hours == 0 ? dayText : $"{dayText} e {hours} h";
    }
}
