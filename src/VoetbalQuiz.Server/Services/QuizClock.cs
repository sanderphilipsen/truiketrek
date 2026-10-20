using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Services;

/// <summary>De quizdag wisselt om middernacht Belgische tijd (zie <see cref="QuizDay"/>), met de injecteerbare klok van de server.</summary>
public static class QuizClock
{
    public static DateOnly Today(TimeProvider clock) => QuizDay.DateOf(clock.GetUtcNow());

    /// <summary>Een UTC-tijdstip uit de database in Belgische tijd.</summary>
    public static DateTime ToBrussels(DateTime utc) => QuizDay.ToBrussels(utc);

    /// <summary>De Belgische datum van een UTC-tijdstip uit de database.</summary>
    public static DateOnly ToBrusselsDate(DateTime utc) => DateOnly.FromDateTime(QuizDay.ToBrussels(utc));
}
