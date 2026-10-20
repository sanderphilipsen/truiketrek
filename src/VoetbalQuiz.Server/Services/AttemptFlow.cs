using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Services;

/// <summary>
/// De klok en de toestand van één vraag, los van waar de poging bewaard wordt (zelfde regels als de daily in
/// <see cref="QuizService"/>). Gebruikt door de training, die haar pogingen in een eigen tabel bewaart.
/// </summary>
public static class AttemptFlow
{
    public static double Seconds(DateTime from, DateTime to) => (to - from).TotalSeconds;

    public static QuestionStatus StatusOf(AttemptState? a) => a switch
    {
        null => QuestionStatus.NotStarted,
        { AnsweredAt: not null } => QuestionStatus.Answered,
        _ => QuestionStatus.InProgress,
    };

    /// <summary>Sluit een vraag af als de tijd om is (een opstelling houdt de gevonden namen). True als er iets wijzigde.</summary>
    public static bool CloseIfExpired(DailyQuestion question, AttemptState attempt, DateTime now)
    {
        if (attempt.AnsweredAt is not null) return false;
        var limit = QuestionEngine.TimeLimit(question);
        if (Seconds(attempt.StartedAt, now) <= limit + GameRules.LatencyGraceSeconds) return false;

        attempt.AnsweredAt = attempt.StartedAt.AddSeconds(limit);
        attempt.Correct = false;
        attempt.Points = QuestionEngine.PointsWhenStopped(question, attempt);
        return true;
    }

    /// <summary>Verwerkt een gok (getypte naam, gekozen optie of jaartal). Geeft de foute gok terug, of null.</summary>
    public static string? Guess(DailyQuestion question, AttemptState attempt, string? guess, DateTime now)
    {
        CloseIfExpired(question, attempt, now);
        var trimmed = guess?.Trim();
        if (attempt.AnsweredAt is not null || string.IsNullOrEmpty(trimmed)) return null;

        string? lastWrong = null;
        attempt.Guesses++;
        attempt.LastGuess = trimmed;
        var outcome = QuestionEngine.Guess(question, attempt, trimmed, Seconds(attempt.StartedAt, now));
        if (outcome.Wrong)
        {
            attempt.WrongGuesses++;
            lastWrong = trimmed;
        }
        attempt.Points = outcome.Points;
        if (outcome.Done)
        {
            attempt.AnsweredAt = now;
            attempt.Correct = outcome.Correct;
        }
        else if (attempt.Guesses >= GameRules.MaxGuessesPerQuestion)
        {
            attempt.AnsweredAt = now;
            attempt.Correct = false;
            attempt.Points = QuestionEngine.PointsWhenStopped(question, attempt);
        }
        return lastWrong;
    }

    /// <summary>"Ik pas" (of "Klaar" bij een opstelling): de vraag stopt, enkel een opstelling houdt deelpunten.</summary>
    public static void GiveUp(DailyQuestion question, AttemptState attempt, DateTime now)
    {
        if (attempt.AnsweredAt is not null) return;
        attempt.Guesses++;
        attempt.AnsweredAt = now;
        attempt.Correct = false;
        attempt.Points = QuestionEngine.PointsWhenStopped(question, attempt);
    }

    /// <summary>Wat de client van de vraag mag zien: het antwoord pas als de vraag afgelopen is.</summary>
    public static QuestionState State(DailyQuestion question, AttemptState attempt, DateTime now, string? lastWrong = null)
    {
        var status = StatusOf(attempt);
        var limit = QuestionEngine.TimeLimit(question);

        if (status == QuestionStatus.Answered)
        {
            var result = QuestionEngine.Result(question, attempt);
            var (_, finalView) = QuestionEngine.Visible(question, attempt, limit);
            return new QuestionState(question.Number, status, result.AllHints, Seconds(attempt.StartedAt, attempt.AnsweredAt!.Value),
                attempt.WrongGuesses, lastWrong, result, question.Type, limit, finalView);
        }

        var elapsed = Seconds(attempt.StartedAt, now);
        var (hints, view) = QuestionEngine.Visible(question, attempt, elapsed);
        return new QuestionState(question.Number, status, hints, elapsed, attempt.WrongGuesses, lastWrong, null,
            question.Type, limit, view);
    }
}
