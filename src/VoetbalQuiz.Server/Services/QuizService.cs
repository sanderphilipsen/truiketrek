using Microsoft.EntityFrameworkCore;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Services;

public class QuizException(string message) : Exception(message);

/// <summary>
/// De daily en de specials spelen. De klok draait op de server: de client krijgt enkel
/// te zien wat op dat moment vrijgegeven is, en de punten worden hier berekend.
/// Welke vragen in een reeks komen, kiest <see cref="QuestionPicker"/>; wat per vraagtype verschilt, staat in <see cref="QuestionEngine"/>.
/// </summary>
public class QuizService(QuizDbContext db, TimeProvider clock, ResultsService results, QuestionPicker picker, MaintenanceNotice maintenance,
    StockAlertService stock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public DateOnly Today => QuizClock.Today(clock);

    // ---------- Daily challenge ----------

    public async Task<DailyChallenge> GetOrCreateChallengeAsync(DateOnly date)
    {
        var challenge = await LoadChallengeAsync(c => c.Date == date && c.SpecialEventId == null);
        if (challenge is not null) return challenge;

        var questions = await picker.BuildDailyAsync(date)
                        ?? throw new QuizException("Er zitten nog niet genoeg spelers in de database.");

        var (saved, created) = await SaveChallengeAsync(new DailyChallenge { Date = date }, questions,
            () => LoadChallengeAsync(c => c.Date == date && c.SpecialEventId == null));
        if (created) await stock.CheckAsync(saved);
        return saved;
    }

    /// <summary>Bewaart een nieuwe reeks. <c>Created</c> is false als iemand anders dezelfde reeks net eerder aanmaakte.</summary>
    private async Task<(DailyChallenge Challenge, bool Created)> SaveChallengeAsync(
        DailyChallenge challenge, List<DailyQuestion> questions, Func<Task<DailyChallenge?>> reload)
    {
        var number = 1;
        foreach (var q in questions) { q.Number = number++; challenge.Questions.Add(q); }

        db.DailyChallenges.Add(challenge);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Iemand anders maakte dezelfde reeks tegelijk aan: die gebruiken we.
            db.ChangeTracker.Clear();
            return ((await reload())!, false);
        }
        return ((await reload())!, true);
    }

    public async Task<DailyChallenge?> LoadChallengeAsync(System.Linq.Expressions.Expression<Func<DailyChallenge, bool>> filter)
    {
        var challenge = await db.DailyChallenges
            .Include(c => c.Questions).ThenInclude(q => q.Player)
            .Include(c => c.Questions).ThenInclude(q => q.QuizItem)
            .AsSplitQuery()
            .SingleOrDefaultAsync(filter);
        if (challenge is not null) await db.LoadCounterPlayersAsync(challenge.Questions);
        return challenge;
    }

    // ---------- Specials ----------

    /// <summary>De vragenreeks van een special; wordt aangemaakt zodra de eerste speler hem opent.</summary>
    public async Task<DailyChallenge> GetOrCreateSpecialChallengeAsync(int specialId)
    {
        var existing = await LoadChallengeAsync(c => c.SpecialEventId == specialId);
        if (existing is not null) return existing;

        var special = await db.SpecialEvents.FindAsync(specialId) ?? throw new QuizException("Deze special bestaat niet.");
        if (Today < special.StartDate) throw new QuizException("Deze special is nog niet begonnen.");
        if (Today > special.EndDate) throw new QuizException("Deze special is afgelopen.");

        var questions = await picker.BuildSpecialAsync(special)
                        ?? throw new QuizException("Deze special heeft nog niet genoeg vragen.");
        return (await SaveChallengeAsync(new DailyChallenge { Date = special.StartDate, SpecialEventId = special.Id }, questions,
            () => LoadChallengeAsync(c => c.SpecialEventId == specialId))).Challenge;
    }

    private Task<DailyChallenge> ChallengeAsync(int? specialId) =>
        specialId is { } id ? GetOrCreateSpecialChallengeAsync(id) : GetOrCreateChallengeAsync(Today);

    // ---------- Overzicht ----------

    public async Task<DailyOverview> GetOverviewAsync(string userId, int? specialId = null)
    {
        var challenge = await ChallengeAsync(specialId);
        var attempts = await AttemptsForAsync(userId, challenge);

        var questions = challenge.Questions.OrderBy(q => q.Number).Select(q =>
        {
            var a = attempts.GetValueOrDefault(q.Id);
            if (a is not null) AttemptFlow.CloseIfExpired(q, a, Now);
            var status = AttemptFlow.StatusOf(a);
            return new QuestionSummary(
                q.Number, status,
                status == QuestionStatus.Answered ? a!.Points : null,
                status == QuestionStatus.Answered ? a!.Correct : null,
                status == QuestionStatus.Answered ? QuestionEngine.AnswerLabel(q) : null,
                q.Type);
        }).ToList();

        await SaveAsync();
        var lastWinner = specialId is null ? await results.GetLastWinnerAsync(userId) : null;
        var types = challenge.Questions.Select(q => q.Type).Distinct().Order().ToList();
        return new DailyOverview(challenge.Date, attempts.Values.Sum(a => a.Points), questions, lastWinner,
            types.Count == 1 ? types[0] : QuestionType.WieBenIk,
            challenge.Questions.Sum(q => QuestionTypes.Get(q.Type).MaxPoints), types, DifficultyOf(challenge));
    }

    /// <summary>Gemiddelde moeilijkheid van een reeks (0–5 sterren); een counter telt als het gemiddelde van zijn spelers.</summary>
    public static double DifficultyOf(DailyChallenge challenge) => Difficulty.Average(challenge.Questions.Select(DifficultyOf));

    /// <summary>De moeilijkheid van één vraag: die van de speler of de vraag, bij een counter het gemiddelde van de spelers.</summary>
    public static double DifficultyOf(DailyQuestion q) =>
        q.Player?.Difficulty ?? q.QuizItem?.Difficulty
        ?? (q.CounterPlayers.Count > 0 ? q.CounterPlayers.Average(p => p.Difficulty) : Difficulty.Default);

    /// <summary>De specials (zie <see cref="ResultsService.GetSpecialsAsync"/>), met de moeilijkheid van de lopende.</summary>
    public async Task<List<SpecialSummary>> GetSpecialsAsync(string userId)
    {
        var specials = await results.GetSpecialsAsync(userId);
        for (var i = 0; i < specials.Count; i++)
        {
            if (!specials[i].IsActive) continue;
            try
            {
                var challenge = await GetOrCreateSpecialChallengeAsync(specials[i].Id);
                specials[i] = specials[i] with { Difficulty = DifficultyOf(challenge), QuestionCount = challenge.Questions.Count };
            }
            catch (QuizException) { } // nog te weinig vragen: dan geen sterren
        }
        return specials;
    }

    // ---------- Klassementspositie voor Home ----------

    /// <summary>
    /// Je plaats van vandaag (zie <see cref="ResultsService.VisitDailyRankAsync"/>). Eerst worden de verlopen vragen van
    /// iedereen in de daily van vandaag afgesloten: anders telt een vraag waarvan de tijd om is nog niet mee, omdat
    /// ze pas lui afgesloten wordt bij het volgende overzicht, en zou er een te lage plaats onthouden worden.
    /// </summary>
    public async Task<DailyRank> VisitDailyRankAsync(string userId)
    {
        await CloseExpiredTodayAsync();
        return await results.VisitDailyRankAsync(userId);
    }

    /// <summary>Sluit alle vragen van de daily van vandaag af waarvan de tijd om is (voor alle spelers).</summary>
    public async Task CloseExpiredTodayAsync()
    {
        var today = Today;
        var open = await db.Attempts
            .Where(a => a.AnsweredAt == null && a.DailyQuestion.DailyChallenge.SpecialEventId == null
                        && a.DailyQuestion.DailyChallenge.Date == today)
            .ToListAsync();
        if (open.Count == 0) return;

        var challenge = await LoadChallengeAsync(c => c.Date == today && c.SpecialEventId == null);
        if (challenge is null) return;
        var questions = challenge.Questions.ToDictionary(q => q.Id);
        foreach (var attempt in open)
            if (questions.TryGetValue(attempt.DailyQuestionId, out var question)) AttemptFlow.CloseIfExpired(question, attempt, Now);
        await SaveAsync();
    }

    // ---------- Één vraag spelen ----------

    /// <summary>Start de vraag (of geeft de lopende toestand terug: herladen zet de klok niet terug).</summary>
    public async Task<QuestionState> StartAsync(string userId, int number, int? specialId = null)
    {
        var (question, attempt) = await FindAsync(userId, number, specialId);
        if (attempt is null)
        {
            if (question.DailyChallenge.SpecialEventId is { } sid
                && await db.SpecialEvents.AnyAsync(s => s.Id == sid && s.EndDate < Today))
                throw new QuizException("Deze special is afgelopen.");

            // Een gestarte vraag mag afgewerkt worden; een nieuwe start pas na de update.
            if (maintenance.UpdateAt() is not null)
                throw new QuizException(MaintenanceNotice.BlockedMessage);

            await AttemptFlow.EnsureInOrderAsync(question, previous =>
                db.Attempts.CountAsync(a => a.UserId == userId && previous.Contains(a.DailyQuestionId) && a.AnsweredAt != null));

            attempt = new Attempt { UserId = userId, DailyQuestionId = question.Id, StartedAt = Now };
            db.Attempts.Add(attempt);
            await db.SaveChangesAsync();
        }
        return await StateAsync(question, attempt);
    }

    public async Task<QuestionState> GetStateAsync(string userId, int number, int? specialId = null)
    {
        var (question, attempt) = await FindAsync(userId, number, specialId);
        if (attempt is null) throw new QuizException("Deze vraag is nog niet gestart.");
        return await StateAsync(question, attempt);
    }

    /// <summary>
    /// Een poging: een getypte naam, een gekozen optie ("0".."3") of een jaartal.
    /// Getypte types mogen opnieuw proberen (met strafpunten); keuzetypes zijn meteen klaar.
    /// </summary>
    public async Task<QuestionState> GuessAsync(string userId, int number, string? guess, int? specialId = null)
    {
        var (question, attempt) = await FindAsync(userId, number, specialId);
        if (attempt is null) throw new QuizException("Deze vraag is nog niet gestart.");

        var lastWrong = AttemptFlow.Guess(question, attempt, guess, Now);
        if (!await SaveAsync()) lastWrong = null;
        return await StateAsync(question, attempt, lastWrong);
    }

    /// <summary>"Ik pas" (of "Klaar" bij een opstelling): de vraag stopt, enkel een opstelling houdt deelpunten.</summary>
    public async Task<QuestionState> GiveUpAsync(string userId, int number, int? specialId = null)
    {
        var (question, attempt) = await FindAsync(userId, number, specialId);
        if (attempt is null) throw new QuizException("Deze vraag is nog niet gestart.");

        AttemptFlow.GiveUp(question, attempt, Now);
        await SaveAsync();
        return await StateAsync(question, attempt);
    }

    /// <summary>Van gokken die tegelijk binnenkomen telt er maar één (zie <see cref="AttemptFlow.SaveAsync"/>).</summary>
    private Task<bool> SaveAsync() => AttemptFlow.SaveAsync(db);

    private async Task<QuestionState> StateAsync(DailyQuestion question, Attempt attempt, string? lastWrong = null)
    {
        AttemptFlow.CloseIfExpired(question, attempt, Now);
        await SaveAsync();
        return AttemptFlow.State(question, attempt, Now, lastWrong);
    }

    private async Task<(DailyQuestion, Attempt?)> FindAsync(string userId, int number, int? specialId)
    {
        var challenge = await ChallengeAsync(specialId);
        var question = challenge.Questions.SingleOrDefault(q => q.Number == number)
            ?? throw new QuizException("Deze vraag bestaat niet.");
        var attempt = await db.Attempts.SingleOrDefaultAsync(a => a.UserId == userId && a.DailyQuestionId == question.Id);
        return (question, attempt);
    }

    private async Task<Dictionary<int, Attempt>> AttemptsForAsync(string userId, DailyChallenge challenge)
    {
        var ids = challenge.Questions.Select(q => q.Id).ToList();
        return await db.Attempts.Where(a => a.UserId == userId && ids.Contains(a.DailyQuestionId))
            .ToDictionaryAsync(a => a.DailyQuestionId);
    }
}
