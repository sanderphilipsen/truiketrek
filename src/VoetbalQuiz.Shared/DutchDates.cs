namespace VoetbalQuiz.Shared;

/// <summary>Nederlandse datumnotatie zonder afhankelijk te zijn van de cultuurdata in de browser.</summary>
public static class DutchDates
{
    private static readonly string[] Months =
        ["januari", "februari", "maart", "april", "mei", "juni", "juli", "augustus", "september", "oktober", "november", "december"];

    private static readonly string[] Days =
        ["zondag", "maandag", "dinsdag", "woensdag", "donderdag", "vrijdag", "zaterdag"];

    public static string Month(int month) => Months[month - 1];

    public static string MonthYear(int year, int month) => $"{Month(month)} {year}";

    public static string DayMonth(DateOnly date) => $"{Days[(int)date.DayOfWeek]} {date.Day} {Month(date.Month)}";

    /// <summary>
    /// Hoe lang geleden, in gewone woorden: "zonet", "12 min geleden", "3 uur geleden", "gisteren", "5 dagen geleden",
    /// en vanaf een maand de datum ("12 september 2026"). Beide tijdstippen in dezelfde tijdzone (bv. UTC).
    /// </summary>
    public static string Ago(DateTime moment, DateTime now)
    {
        var span = now - moment;
        if (span < TimeSpan.FromMinutes(1)) return "zonet";
        if (span < TimeSpan.FromHours(1)) return $"{(int)span.TotalMinutes} min geleden";
        if (span < TimeSpan.FromHours(24)) return (int)span.TotalHours == 1 ? "1 uur geleden" : $"{(int)span.TotalHours} uur geleden";
        var days = (int)span.TotalDays;
        if (days == 1) return "gisteren";
        if (days < 30) return $"{days} dagen geleden";
        return $"{moment.Day} {Month(moment.Month)} {moment.Year}";
    }

    /// <summary>Een aftelling in gewone woorden: "5u 12m", "34 min", en onder een minuut "1 min" (nooit "0 min").</summary>
    public static string Countdown(TimeSpan left)
    {
        var minutes = Math.Max(1, (int)Math.Ceiling(left.TotalMinutes));
        return minutes < 60 ? $"{minutes} min" : $"{minutes / 60}u {minutes % 60:00}m";
    }

    /// <summary>Achtervoegsel van een rangtelwoord in cijfers: 1ste, 2de, 8ste, 19de, 20ste, 100ste, 101ste, 102de, 108ste.</summary>
    public static string OrdinalSuffix(int n)
    {
        var r = n % 100;
        return r is 1 or 8 || r >= 20 || (n >= 100 && r == 0) ? "ste" : "de";
    }
}
