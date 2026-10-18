namespace VoetbalQuiz.Server.Services;

/// <summary>De quizdag wisselt om middernacht Belgische tijd.</summary>
public static class QuizClock
{
    private static readonly TimeZoneInfo Brussels = TimeZoneInfo.FindSystemTimeZoneById("Europe/Brussels");

    public static DateOnly Today(TimeProvider clock) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Brussels).DateTime);

    /// <summary>De Belgische datum van een UTC-tijdstip uit de database.</summary>
    public static DateOnly ToBrusselsDate(DateTime utc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Brussels));
}
