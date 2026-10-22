namespace VoetbalQuiz.Shared;

/// <summary>
/// De quizdag wisselt om middernacht Belgische tijd, voor iedereen: ook in de browser van wie in een andere tijdzone zit.
/// De server gebruikt dit via <c>QuizClock</c> (met een injecteerbare klok); de client enkel voor weergave
/// (bv. "vandaag" in het admin-paneel of de lopende maand in het klassement).
/// </summary>
public static class QuizDay
{
    private static readonly TimeZoneInfo Brussels = FindBrussels();

    /// <summary>Een UTC-tijdstip (bv. uit de database) in Belgische tijd.</summary>
    public static DateTime ToBrussels(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Brussels);

    /// <summary>De Belgische datum van een tijdstip.</summary>
    public static DateOnly DateOf(DateTimeOffset moment) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(moment, Brussels).DateTime);

    /// <summary>Middernacht Belgische tijd aan het begin van deze dag, als UTC-tijdstip (om op de database te filteren).</summary>
    public static DateTime StartOfDayUtc(DateOnly date) =>
        TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), Brussels);

    /// <summary>Vandaag volgens de klok van dit toestel, in Belgische tijd (enkel voor weergave op de client).</summary>
    public static DateOnly Today() => DateOf(DateTimeOffset.UtcNow);

    /// <summary>
    /// Zonder tijdzonedata valt de browser terug op de lokale tijd, zoals vroeger. De server faalt dan bij het opstarten:
    /// stil op de machinetijd draaien zou de daily op het verkeerde uur laten wisselen.
    /// </summary>
    private static TimeZoneInfo FindBrussels()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Brussels");
        }
        catch (TimeZoneNotFoundException) when (OperatingSystem.IsBrowser())
        {
            return TimeZoneInfo.Local;
        }
    }
}
