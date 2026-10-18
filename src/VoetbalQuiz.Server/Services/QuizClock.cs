namespace VoetbalQuiz.Server.Services;

/// <summary>De quizdag wisselt om middernacht Belgische tijd.</summary>
public static class QuizClock
{
    private static readonly TimeZoneInfo Brussels = TimeZoneInfo.FindSystemTimeZoneById("Europe/Brussels");

    public static DateOnly Today(TimeProvider clock) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Brussels).DateTime);
}
