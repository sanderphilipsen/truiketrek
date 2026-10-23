using Microsoft.EntityFrameworkCore;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Services;

/// <summary>
/// De klok en de toestand van één vraag, los van waar de poging bewaard wordt: één plek voor de regels van een poging
/// (speling, maximum aantal pogingen, deelpunten bij stoppen). Gebruikt door de daily en de specials
/// (<see cref="QuizService"/>, tabel Attempts), de academie (<see cref="TrainingService"/>, tabel TrainingAttempts) en
/// online (<see cref="MatchService"/>, tabel MatchAnswers). Online heeft een gedeelde klok per vraag: daar scoort
/// <see cref="GuessAt"/> met de tijd van de wedstrijd en sluit de wedstrijd de vraag af.
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
    public static bool CloseIfExpired(IQuizQuestion question, AttemptState attempt, DateTime now)
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
    public static string? Guess(IQuizQuestion question, AttemptState attempt, string? guess, DateTime now)
    {
        CloseIfExpired(question, attempt, now);
        return GuessAt(question, attempt, guess, now, Seconds(attempt.StartedAt, now));
    }

    /// <summary>
    /// Zoals <see cref="Guess"/>, maar de tijd waarmee gescoord wordt komt van de oproeper en de vraag sluit hier niet
    /// op tijd af. Voor online (<see cref="MatchService"/>): de klok is daar gedeeld per vraag, met verlenging (derby),
    /// en de wedstrijd sluit de vraag zelf af.
    /// </summary>
    public static string? GuessAt(IQuizQuestion question, AttemptState attempt, string? guess, DateTime now, double elapsed)
    {
        var trimmed = guess?.Trim();
        if (attempt.AnsweredAt is not null || string.IsNullOrEmpty(trimmed)) return null;

        string? lastWrong = null;
        var outcome = QuestionEngine.Guess(question, attempt, trimmed, elapsed);
        // Een gok die niet telt (kaart: een andere speld dan de huidige, of een ongeldig antwoord) wijzigt niets, ook
        // niet het aantal pogingen: zo kan een dubbele tik of een script de grens van QuestionRules.MaxGuesses niet opsouperen.
        if (outcome.Ignored) return null;
        attempt.Guesses++;
        attempt.LastGuess = trimmed;
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
        else if (attempt.Guesses >= QuestionRules.MaxGuesses(question.Type))
        {
            attempt.AnsweredAt = now;
            attempt.Correct = false;
            attempt.Points = QuestionEngine.PointsWhenStopped(question, attempt);
        }
        return lastWrong;
    }

    /// <summary>"Ik pas" (of "Klaar" bij een opstelling): de vraag stopt, enkel een opstelling houdt deelpunten.</summary>
    public static void GiveUp(IQuizQuestion question, AttemptState attempt, DateTime now)
    {
        if (attempt.AnsweredAt is not null) return;
        attempt.Guesses++;
        attempt.AnsweredAt = now;
        attempt.Correct = false;
        attempt.Points = QuestionEngine.PointsWhenStopped(question, attempt);
    }

    /// <summary>
    /// Je speelt de vragen van een reeks in volgorde: alle vorige vragen moeten af zijn.
    /// <paramref name="answeredAmong"/> telt hoeveel van de gegeven vragen (ids) je al afwerkte, in de tabel van die flow.
    /// </summary>
    public static async Task EnsureInOrderAsync(DailyQuestion question, Func<List<int>, Task<int>> answeredAmong)
    {
        var previous = question.DailyChallenge.Questions.Where(q => q.Number < question.Number).Select(q => q.Id).ToList();
        if (await answeredAmong(previous) < previous.Count)
            throw new QuizException("Speel eerst de vorige vragen.");
    }

    /// <summary>
    /// Bewaart de pogingen. Kwam er tegelijk een ander verzoek voor dezelfde vraag binnen (dubbele tik, of een script
    /// dat gokken afvuurt), dan telt enkel dat andere: deze wijziging vervalt en we lezen de poging opnieuw in
    /// (<see cref="AttemptState.Guesses"/> is het concurrency token). False als deze wijziging verviel.
    /// </summary>
    public static async Task<bool> SaveAsync(QuizDbContext db)
    {
        try
        {
            await db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            foreach (var entry in ex.Entries) await entry.ReloadAsync();
            return false;
        }
    }

    /// <summary>Wat de client van de vraag mag zien: het antwoord pas als de vraag afgelopen is.</summary>
    public static QuestionState State(IQuizQuestion question, AttemptState attempt, DateTime now, string? lastWrong = null)
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
