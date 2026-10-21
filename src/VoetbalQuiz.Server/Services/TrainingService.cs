using Microsoft.EntityFrameworkCore;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Services;

/// <summary>
/// Academie (in de code: training): oude dailies en afgelopen specials opnieuw spelen, zo vaak je wil. De pogingen staan in
/// <see cref="QuizDbContext.TrainingRuns"/>, los van <see cref="QuizDbContext.Attempts"/>: klassementen, XP, reeksen, badges
/// en statistieken zien ze dus nooit. De klok draait op de server, met dezelfde regels als de daily (<see cref="AttemptFlow"/>).
/// </summary>
public class TrainingService(QuizDbContext db, TimeProvider clock, QuizService quiz, ResultsService results)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private DateOnly Today => QuizClock.Today(clock);

    // ---------- Archief ----------

    /// <summary>
    /// De dailies en afgelopen specials van een maand, nieuwste eerst. Een special staat in de maand van haar startdatum,
    /// net als in het maandklassement (<see cref="MonthRules.Days"/>), maar pas als ze afgelopen is.
    /// Zonder maand: de maand van gisteren, de laatste dag die je kan spelen (Belgische tijd).
    /// </summary>
    public async Task<TrainingArchive> GetArchiveAsync(string userId, int? year = null, int? month = null)
    {
        var today = Today;
        var yesterday = today.AddDays(-1);
        var latest = new DateOnly(yesterday.Year, yesterday.Month, 1);
        var (from, to) = year is { } y && month is { } m ? MonthRules.Days(y, m) : MonthRules.Days(latest.Year, latest.Month);

        var challenges = await db.DailyChallenges
            .Where(c => c.Date >= from && c.Date < to
                        && (c.SpecialEventId == null ? c.Date < today : c.SpecialEvent!.EndDate < today))
            .Include(c => c.SpecialEvent)
            .Include(c => c.Questions).ThenInclude(q => q.Player)
            .Include(c => c.Questions).ThenInclude(q => q.QuizItem)
            .AsSplitQuery()
            .ToListAsync();
        await db.LoadCounterPlayersAsync(challenges.SelectMany(c => c.Questions)); // nodig voor de moeilijkheid

        var ids = challenges.Select(c => c.Id).ToList();
        var standings = await results.DayStandingsAsync(ids);
        var runs = (await RunsAsync(userId, ids)).ToLookup(r => r.ChallengeId);

        var days = challenges.Select(c =>
        {
            var infos = RunInfos(runs[c.Id]);
            var special = SpecialOf(c.SpecialEvent);
            return new TrainingDay(c.Date, QuizService.DifficultyOf(c), TypesOf(c),
                RealOf(userId, standings.GetValueOrDefault(c.Id)), infos.Count, infos.FirstOrDefault(r => r.IsBest)?.Points, special);
        }).OrderByDescending(d => d.Date).ThenBy(d => d.Special is null ? 0 : 1).ToList();

        var hasEarlier = await db.DailyChallenges.AnyAsync(c => c.Date < from && (c.SpecialEventId == null || c.SpecialEvent!.EndDate < today));
        return new TrainingArchive(from.Year, from.Month, days, hasEarlier, from < latest);
    }

    /// <summary>Eén daily met al je scores: je echte poging en elke trainingsrun.</summary>
    public async Task<TrainingDayDetail> GetDayAsync(string userId, DateOnly date) =>
        await DetailAsync(userId, await TrainableAsync(date), null);

    /// <summary>Eén afgelopen special met al je scores.</summary>
    public async Task<TrainingDayDetail> GetSpecialAsync(string userId, int specialId)
    {
        var (special, challenge) = await TrainableSpecialAsync(specialId);
        return await DetailAsync(userId, challenge, special);
    }

    private async Task<TrainingDayDetail> DetailAsync(string userId, DailyChallenge challenge, SpecialEvent? special)
    {
        var standings = (await results.DayStandingsAsync([challenge.Id])).GetValueOrDefault(challenge.Id) ?? [];
        var runs = RunInfos(await RunsAsync(userId, [challenge.Id]));
        return new TrainingDayDetail(challenge.Date, QuizService.DifficultyOf(challenge), TypesOf(challenge),
            challenge.Questions.Count, MaxPointsOf(challenge), standings.Count, RealOf(userId, standings), runs,
            SharesWithToday(challenge, await OpenTodayAsync(userId)), SpecialOf(special));
    }

    /// <summary>Start een nieuwe run op deze daily.</summary>
    public async Task<TrainingRunInfo> StartRunAsync(string userId, DateOnly date) =>
        await StartRunAsync(userId, await TrainableAsync(date));

    /// <summary>Start een nieuwe run op deze afgelopen special.</summary>
    public async Task<TrainingRunInfo> StartSpecialRunAsync(string userId, int specialId) =>
        await StartRunAsync(userId, (await TrainableSpecialAsync(specialId)).Challenge);

    /// <summary>"Opnieuw spelen" na een run: een nieuwe run op dezelfde daily of special.</summary>
    public async Task<TrainingRunInfo> StartAgainAsync(string userId, int runId)
    {
        var (_, challenge) = await RunAsync(userId, runId);
        return await StartRunAsync(userId, challenge);
    }

    /// <summary>
    /// Start een nieuwe run. Vorige runs blijven bewaard, ook onafgewerkte; enkel een run zonder één gestarte vraag wordt
    /// hergebruikt, zodat herhaald op "Spelen" tikken geen lege runs opstapelt.
    /// </summary>
    private async Task<TrainingRunInfo> StartRunAsync(string userId, DailyChallenge challenge)
    {
        if (SharesWithToday(challenge, await OpenTodayAsync(userId)))
            throw new QuizException(SharesWithTodayMessage);

        var run = await db.TrainingRuns.FirstOrDefaultAsync(r => r.UserId == userId && r.DailyChallengeId == challenge.Id && !r.Attempts.Any());
        if (run is null)
        {
            run = new TrainingRun { UserId = userId, DailyChallengeId = challenge.Id };
            db.TrainingRuns.Add(run);
        }
        run.StartedAt = Now;
        await db.SaveChangesAsync();
        return new TrainingRunInfo(run.Id, run.StartedAt, false, 0, 0, false);
    }

    // ---------- Niets verklappen van de daily van vandaag ----------

    public const string SharesWithTodayMessage =
        "Een van deze vragen zit ook in de daily van vandaag. Speel eerst de daily van vandaag.";

    /// <summary>Spelers en vragen uit de daily van vandaag die je daar nog niet beantwoordde.</summary>
    private record OpenToday(HashSet<int> Players, HashSet<int> Items);

    /// <summary>
    /// Een speler of vraag kan na een tijd terugkomen in de daily. Wie een oude daily traint, mag zo niet het antwoord
    /// leren van een vraag die vandaag nog voor punten telt: zolang je die vraag vandaag niet beantwoordde, blijft ze dicht.
    /// </summary>
    private async Task<OpenToday> OpenTodayAsync(string userId)
    {
        DailyChallenge today;
        try { today = await quiz.GetOrCreateChallengeAsync(Today); }
        catch (QuizException) { return new([], []); } // geen daily vandaag (te weinig inhoud): niets te verklappen

        var ids = today.Questions.Select(q => q.Id).ToList();
        var answered = (await db.Attempts
            .Where(a => a.UserId == userId && ids.Contains(a.DailyQuestionId) && a.AnsweredAt != null)
            .Select(a => a.DailyQuestionId).ToListAsync()).ToHashSet();
        var open = today.Questions.Where(q => !answered.Contains(q.Id)).ToList();
        return new(open.SelectMany(QuestionContent.PlayersOf).ToHashSet(), open.Where(q => q.QuizItemId is not null).Select(q => q.QuizItemId!.Value).ToHashSet());
    }

    private static bool Shares(DailyQuestion q, OpenToday open) =>
        QuestionContent.PlayersOf(q).Any(open.Players.Contains) || (q.QuizItemId is { } item && open.Items.Contains(item));

    private static bool SharesWithToday(DailyChallenge c, OpenToday open) => c.Questions.Any(q => Shares(q, open));

    // ---------- Een run spelen ----------

    /// <summary>Overzicht van een run, in dezelfde vorm als de daily (zodat het vraagscherm hetzelfde blijft).</summary>
    public async Task<DailyOverview> GetRunOverviewAsync(string userId, int runId)
    {
        var (run, challenge) = await RunAsync(userId, runId);
        var attempts = await CloseExpiredAsync(run, challenge);

        var questions = challenge.Questions.OrderBy(q => q.Number).Select(q =>
        {
            var a = attempts.GetValueOrDefault(q.Id);
            var status = AttemptFlow.StatusOf(a);
            var answered = status == QuestionStatus.Answered;
            return new QuestionSummary(q.Number, status, answered ? a!.Points : null, answered ? a!.Correct : null,
                answered ? QuestionEngine.AnswerLabel(q) : null);
        }).ToList();

        var types = TypesOf(challenge);
        return new DailyOverview(challenge.Date, attempts.Values.Sum(a => a.Points), questions, null,
            types.Count == 1 ? types[0] : QuestionType.WieBenIk, MaxPointsOf(challenge), types, QuizService.DifficultyOf(challenge));
    }

    /// <summary>Je score van deze run, waar je die dag (of special) mee geëindigd was, en al je scores erop.</summary>
    public async Task<TrainingSummary> GetSummaryAsync(string userId, int runId)
    {
        var (run, challenge) = await RunAsync(userId, runId);
        var attempts = await CloseExpiredAsync(run, challenge);
        var points = attempts.Values.Sum(a => a.Points);
        var correct = attempts.Values.Count(a => a.Correct);
        var wrong = attempts.Values.Sum(a => a.WrongGuesses);

        var standings = (await results.DayStandingsAsync([challenge.Id])).GetValueOrDefault(challenge.Id) ?? [];
        var others = standings.Where(s => s.UserId != userId).ToList();
        var runs = RunInfos(await RunsAsync(userId, [challenge.Id]));

        return new TrainingSummary(run.Id, challenge.Date, run.FinishedAt is not null, points, correct, challenge.Questions.Count,
            MaxPointsOf(challenge), WouldBeRank(others, points, correct, wrong), others.Count + 1, RealOf(userId, standings), runs,
            SpecialOf(challenge.SpecialEventId is { } sid ? await db.SpecialEvents.FindAsync(sid) : null));
    }

    /// <summary>Start de vraag (of geeft de lopende toestand terug: herladen zet de klok niet terug).</summary>
    public async Task<QuestionState> StartAsync(string userId, int runId, int number)
    {
        var (run, question, attempt) = await FindAsync(userId, runId, number);
        if (attempt is null)
        {
            // Ook in training speel je de vragen in volgorde.
            await AttemptFlow.EnsureInOrderAsync(question, previous => db.TrainingAttempts.CountAsync(a =>
                a.TrainingRunId == run.Id && previous.Contains(a.DailyQuestionId) && a.AnsweredAt != null));
            // Opnieuw nagekeken per vraag: de daily van vandaag kan pas na het starten van de run aangemaakt zijn.
            if (Shares(question, await OpenTodayAsync(userId)))
                throw new QuizException(SharesWithTodayMessage);

            attempt = new TrainingAttempt { TrainingRunId = run.Id, DailyQuestionId = question.Id, StartedAt = Now };
            db.TrainingAttempts.Add(attempt);
            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Dubbele tik: de andere start won; verder met die poging.
                db.Entry(attempt).State = EntityState.Detached;
                attempt = await db.TrainingAttempts.SingleAsync(a => a.TrainingRunId == run.Id && a.DailyQuestionId == question.Id);
            }
        }
        return await StateAsync(run, question, attempt);
    }

    public async Task<QuestionState> GetStateAsync(string userId, int runId, int number)
    {
        var (run, question, attempt) = await FindAsync(userId, runId, number);
        if (attempt is null) throw new QuizException("Deze vraag is nog niet gestart.");
        return await StateAsync(run, question, attempt);
    }

    public async Task<QuestionState> GuessAsync(string userId, int runId, int number, string? guess)
    {
        var (run, question, attempt) = await FindAsync(userId, runId, number);
        if (attempt is null) throw new QuizException("Deze vraag is nog niet gestart.");

        var lastWrong = AttemptFlow.Guess(question, attempt, guess, Now);
        if (!await SaveAsync()) lastWrong = null;
        return await StateAsync(run, question, attempt, lastWrong);
    }

    public async Task<QuestionState> GiveUpAsync(string userId, int runId, int number)
    {
        var (run, question, attempt) = await FindAsync(userId, runId, number);
        if (attempt is null) throw new QuizException("Deze vraag is nog niet gestart.");

        AttemptFlow.GiveUp(question, attempt, Now);
        await SaveAsync();
        return await StateAsync(run, question, attempt);
    }

    // ---------- Rangschikking ----------

    /// <summary>
    /// Je plaats als je die dag zo gespeeld had, met de volgorde van het dagklassement (<see cref="DayScore"/>).
    /// Bij een volledige gelijkstand gaat wie echt speelde voor: die was eerder klaar.
    /// </summary>
    public static int WouldBeRank(IEnumerable<ResultsService.DayStanding> others, int points, int correct, int wrong)
    {
        var mine = new DayScore(points, correct, wrong);
        return 1 + others.Count(o => new DayScore(o.Points, o.Correct, o.Wrong).CompareTo(mine) <= 0);
    }

    // ---------- Hulp ----------

    private record RunRow(int Id, int ChallengeId, DateTime StartedAt, DateTime? FinishedAt, int Points, int Correct);

    private Task<List<RunRow>> RunsAsync(string userId, ICollection<int> challengeIds) =>
        db.TrainingRuns
            .Where(r => r.UserId == userId && challengeIds.Contains(r.DailyChallengeId))
            .Select(r => new RunRow(r.Id, r.DailyChallengeId, r.StartedAt, r.FinishedAt,
                r.Attempts.Sum(a => a.Points), r.Attempts.Count(a => a.Correct)))
            .ToListAsync();

    /// <summary>Nieuwste eerst; de beste is de hoogste afgewerkte run (bij gelijkstand de eerste).</summary>
    private static List<TrainingRunInfo> RunInfos(IEnumerable<RunRow> runs)
    {
        var list = runs.ToList();
        var best = list.Where(r => r.FinishedAt is not null)
            .OrderByDescending(r => r.Points).ThenBy(r => r.StartedAt).ThenBy(r => r.Id).FirstOrDefault()?.Id;
        return list.OrderByDescending(r => r.StartedAt).ThenByDescending(r => r.Id)
            .Select(r => new TrainingRunInfo(r.Id, r.StartedAt, r.FinishedAt is not null, r.Points, r.Correct, r.Id == best))
            .ToList();
    }

    private static RealResult? RealOf(string userId, List<ResultsService.DayStanding>? standings)
    {
        var index = standings?.FindIndex(s => s.UserId == userId) ?? -1;
        return index < 0 ? null : new RealResult(standings![index].Points, standings[index].Correct, index + 1, standings.Count);
    }

    private static List<QuestionType> TypesOf(DailyChallenge c) => c.Questions.Select(q => q.Type).Distinct().Order().ToList();

    private static int MaxPointsOf(DailyChallenge c) => c.Questions.Sum(q => QuestionTypes.Get(q.Type).MaxPoints);

    /// <summary>Een gewone daily die je mag trainen: ze bestaat en haar dag is voorbij.</summary>
    private async Task<DailyChallenge> TrainableAsync(DateOnly date)
    {
        if (!TrainingRules.CanTrain(date, Today)) throw new QuizException("Deze daily komt pas in de academie als de dag voorbij is.");
        return await quiz.LoadChallengeAsync(c => c.Date == date && c.SpecialEventId == null)
               ?? throw new QuizException("Op deze dag was er geen daily.");
    }

    /// <summary>
    /// Een special die je mag herspelen: ze is afgelopen en werd gespeeld. Een lopende special nooit: die telt nog voor
    /// haar klassement. Een special zonder reeks wordt niet achteraf samengesteld.
    /// </summary>
    private async Task<(SpecialEvent Special, DailyChallenge Challenge)> TrainableSpecialAsync(int specialId)
    {
        var special = await db.SpecialEvents.FindAsync(specialId) ?? throw new QuizException("Deze special bestaat niet.");
        if (!TrainingRules.CanTrainSpecial(special.EndDate, Today))
            throw new QuizException("Deze special komt pas in de academie als ze afgelopen is.");
        var challenge = await quiz.LoadChallengeAsync(c => c.SpecialEventId == specialId)
                        ?? throw new QuizException("Deze special werd niet gespeeld.");
        return (special, challenge);
    }

    private static TrainingSpecial? SpecialOf(SpecialEvent? s) =>
        s is null ? null : new TrainingSpecial(s.Id, s.Name, s.Icon, s.StartDate, s.EndDate);

    /// <summary>Een run is altijd van de ingelogde gebruiker; die van een ander bestaat voor jou niet.</summary>
    private async Task<(TrainingRun, DailyChallenge)> RunAsync(string userId, int runId)
    {
        var run = await db.TrainingRuns.SingleOrDefaultAsync(r => r.Id == runId && r.UserId == userId)
                  ?? throw new QuizException("Deze poging bestaat niet.");
        var challenge = await quiz.LoadChallengeAsync(c => c.Id == run.DailyChallengeId)
                        ?? throw new QuizException("Deze poging bestaat niet.");
        return (run, challenge);
    }

    private async Task<(TrainingRun, DailyQuestion, TrainingAttempt?)> FindAsync(string userId, int runId, int number)
    {
        var (run, challenge) = await RunAsync(userId, runId);
        var question = challenge.Questions.SingleOrDefault(q => q.Number == number)
                       ?? throw new QuizException("Deze vraag bestaat niet.");
        var attempt = await db.TrainingAttempts.SingleOrDefaultAsync(a => a.TrainingRunId == run.Id && a.DailyQuestionId == question.Id);
        return (run, question, attempt);
    }

    /// <summary>Sluit verlopen vragen van de run af en geeft de pogingen per vraag terug.</summary>
    private async Task<Dictionary<int, TrainingAttempt>> CloseExpiredAsync(TrainingRun run, DailyChallenge challenge)
    {
        var attempts = await db.TrainingAttempts.Where(a => a.TrainingRunId == run.Id).ToDictionaryAsync(a => a.DailyQuestionId);
        foreach (var q in challenge.Questions)
            if (attempts.TryGetValue(q.Id, out var a))
            {
                AttemptFlow.CloseIfExpired(q, a, Now);
                MarkFinished(run, q, a);
            }
        await SaveAsync();
        return attempts;
    }

    private async Task<QuestionState> StateAsync(TrainingRun run, DailyQuestion question, TrainingAttempt attempt, string? lastWrong = null)
    {
        AttemptFlow.CloseIfExpired(question, attempt, Now);
        MarkFinished(run, question, attempt);
        await SaveAsync();
        return AttemptFlow.State(question, attempt, Now, lastWrong);
    }

    /// <summary>De laatste vraag is af (de vorige moesten al af zijn om ze te starten): dan is de run klaar.</summary>
    private static void MarkFinished(TrainingRun run, DailyQuestion question, TrainingAttempt attempt)
    {
        if (run.FinishedAt is null && attempt.AnsweredAt is { } answered
            && question.Number == question.DailyChallenge.Questions.Max(q => q.Number))
            run.FinishedAt = answered;
    }

    /// <summary>Zoals in de daily: van gokken die tegelijk binnenkomen telt er maar één (zie <see cref="AttemptFlow.SaveAsync"/>).</summary>
    private Task<bool> SaveAsync() => AttemptFlow.SaveAsync(db);
}
