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
}
