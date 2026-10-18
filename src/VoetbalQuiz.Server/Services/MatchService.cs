using Microsoft.EntityFrameworkCore;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Services;

/// <summary>
/// Online wedstrijden: tafels aanmaken, joinen, starten en antwoorden.
/// De organisator kiest een vraagtype of een mix; wat per type verschilt, doet <see cref="QuestionEngine"/> (net als in de daily).
/// De overgangen in de tijd (vraag afsluiten, volgende vraag, einde) doet <see cref="MatchEngine"/>.
/// </summary>
public class MatchService(QuizDbContext db, TimeProvider clock, ResultsService results, QuizService quiz, IMatchNotifier notifier)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    // ---------- Lobby ----------

    private record LobbyRow(
        int Id, string Name, string HostName, string? HostAvatar, int Seats, MatchStatus Status,
        DateTime CreatedAt, DateTime? FinishedAt, int Players, bool IAmIn, bool Played, List<QuestionType> Types);

    public async Task<LobbyOverview> GetLobbyAsync(string userId)
    {
        var matches = await db.Matches
            .Where(m => m.Status == MatchStatus.Lobby || m.Status == MatchStatus.Running
                        || (m.Status == MatchStatus.Finished && m.Players.Any(p => p.UserId == userId)))
            .OrderByDescending(m => m.CreatedAt)
            .Take(50)
            .Select(m => new LobbyRow(
                m.Id, m.Name, m.Host.UserName!, m.Host.AvatarId, m.Seats, m.Status, m.CreatedAt, m.FinishedAt,
                m.Players.Count(p => p.LeftAt == null),
                m.Players.Any(p => p.UserId == userId && p.LeftAt == null),
                m.Players.Any(p => p.UserId == userId),
                m.Types))
            .ToListAsync();

        static MatchSummary Summary(LobbyRow m) =>
            new(m.Id, m.Name, m.HostName, m.HostAvatar, m.Seats, m.Players, m.Status, m.IAmIn, m.CreatedAt, TypesOf(m.Types));

        return new LobbyOverview(
            matches.Where(m => m.Status == MatchStatus.Lobby && !m.IAmIn && m.Players < m.Seats).Select(m => Summary(m)).ToList(),
            matches.Where(m => m.IAmIn && m.Status is MatchStatus.Lobby or MatchStatus.Running).Select(m => Summary(m)).ToList(),
            matches.Where(m => m.Status == MatchStatus.Finished && m.Played).OrderByDescending(m => m.FinishedAt).Take(5).Select(m => Summary(m)).ToList());
    }

    public async Task<int> CreateAsync(string userId, string userName, CreateMatchRequest req)
    {
        var name = string.IsNullOrWhiteSpace(req.Name) ? $"Tafel van {userName}" : req.Name.Trim();
        if (name.Length > 40) name = name[..40];

        var types = TypesOf(req.Types);
        var active = await quiz.ActiveTypesAsync();
        if (types.Any(t => !active.Contains(t)))
            throw new QuizException("Dat vraagtype staat momenteel uit. Kies een ander type.");
        if (await quiz.BuildOnlineAsync(types) is null)
            throw new QuizException("Er zijn nog niet genoeg vragen voor deze keuze. Kies een ander type of een mix.");

        var match = new Match
        {
            Name = name,
            HostUserId = userId,
            Seats = Math.Clamp(req.Seats, MatchRules.MinSeats, MatchRules.MaxSeats),
            Types = types,
            CreatedAt = Now,
            Players = [new MatchPlayer { UserId = userId, JoinedAt = Now }],
        };
        db.Matches.Add(match);
        await db.SaveChangesAsync();
        await notifier.LobbyChangedAsync();
        return match.Id;
    }

    public async Task JoinAsync(string userId, int matchId)
    {
        var match = await LoadAsync(matchId);
        if (match.Players.Any(p => p.UserId == userId)) return;
        if (match.Status != MatchStatus.Lobby) throw new QuizException("Deze wedstrijd is al begonnen.");
        if (match.Players.Count >= match.Seats) throw new QuizException("Deze tafel is vol.");

        match.Players.Add(new MatchPlayer { UserId = userId, JoinedAt = Now });
        await SaveAndNotifyAsync(match);
    }

    public async Task LeaveAsync(string userId, int matchId)
    {
        var match = await LoadAsync(matchId);
        var me = match.Players.SingleOrDefault(p => p.UserId == userId) ?? throw new QuizException("Je zit niet aan deze tafel.");

        if (match.Status == MatchStatus.Lobby)
        {
            if (match.HostUserId == userId) match.Status = MatchStatus.Cancelled; // organisator weg = tafel weg
            else match.Players.Remove(me);
        }
        else if (match.Status == MatchStatus.Running)
        {
            me.LeftAt = Now;
        }
        await SaveAndNotifyAsync(match);
    }

    public async Task StartAsync(string userId, int matchId)
    {
        var match = await LoadAsync(matchId);
        if (match.HostUserId != userId) throw new QuizException("Enkel de organisator kan starten.");
        if (match.Status != MatchStatus.Lobby) throw new QuizException("Deze wedstrijd is al begonnen.");
        if (match.Players.Count < MatchRules.MinSeats) throw new QuizException("Je hebt minstens 2 spelers nodig.");

        // Werd een type intussen uitgeschakeld, dan valt het weg; de rest van de mix blijft.
        var active = await quiz.ActiveTypesAsync();
        var types = TypesOf(match.Types).Where(active.Contains).ToList();
        if (types.Count == 0) throw new QuizException("Het gekozen vraagtype staat intussen uit. Maak een nieuwe tafel.");

        // Willekeurige vragen, maar niets uit de daily van vandaag (anders verklap je die).
        var questions = await quiz.BuildOnlineAsync(types)
            ?? throw new QuizException("Er zijn nog niet genoeg vragen voor deze keuze.");

        var number = 1;
        foreach (var q in questions)
            match.Questions.Add(new MatchQuestion
            {
                Number = number++,
                Type = q.Type,
                PlayerId = q.PlayerId,
                QuizItemId = q.QuizItemId,
                Blanks = q.Blanks,
                CounterPlayerIds = q.CounterPlayerIds,
            });

        match.Questions[0].StartsAt = Now.AddSeconds(MatchRules.CountdownSeconds);
        match.Status = MatchStatus.Running;
        match.StartedAt = Now;
        await SaveAndNotifyAsync(match);
    }

    /// <summary>De types van een tafel; leeg (oudere tafels) = "Wie ben ik?".</summary>
    private static List<QuestionType> TypesOf(List<QuestionType>? types) =>
        types is { Count: > 0 } ? [.. types.Distinct().Order()] : [QuestionType.WieBenIk];

    // ---------- Brug naar QuestionEngine ----------

    /// <summary>De engine van de daily werkt op een DailyQuestion: we geven hem een kopie die niet opgeslagen wordt.</summary>
    private static DailyQuestion AsDaily(MatchQuestion q) => new()
    {
        Id = q.Id,
        Number = q.Number,
        Type = q.Type,
        PlayerId = q.PlayerId,
        Player = q.Player,
        QuizItemId = q.QuizItemId,
        QuizItem = q.QuizItem,
        Blanks = q.Blanks,
        CounterPlayerIds = q.CounterPlayerIds,
        CounterPlayers = q.CounterPlayers,
    };

    /// <summary>Idem voor een antwoord: een Attempt met dezelfde stand.</summary>
    private static Attempt AsAttempt(string userId, MatchAnswer? a) => new()
    {
        UserId = userId,
        WrongGuesses = a?.WrongGuesses ?? 0,
        Correct = a?.Correct ?? false,
        Points = a?.SpeedPoints ?? 0,
        Choice = a?.Choice,
        Found = [.. a?.Found ?? []],
        Skipped = [.. a?.Skipped ?? []],
    };

    private static double TimeLimit(MatchQuestion q) => QuestionEngine.TimeLimit(AsDaily(q));

    private double Elapsed(MatchQuestion q) => (Now - q.StartsAt!.Value).TotalSeconds;

    // ---------- Spelen ----------

    public async Task<MatchState> GuessAsync(string userId, int matchId, string? guess)
    {
        var trimmed = guess?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return await GetStateAsync(userId, matchId);

        var (match, question, answer) = await PlayableAsync(userId, matchId);
        string? wrong = null;
        if (question is not null && answer is { AnsweredAt: null })
        {
            answer.LastGuess = trimmed;
            var attempt = AsAttempt(userId, answer);
            var outcome = QuestionEngine.Guess(AsDaily(question), attempt, trimmed, Elapsed(question));
            (answer.Choice, answer.Found, answer.Skipped) = (attempt.Choice, attempt.Found, attempt.Skipped);
            if (outcome.Wrong)
            {
                answer.WrongGuesses++;
                wrong = trimmed;
            }
            answer.SpeedPoints = outcome.Points;
            if (outcome.Done)
            {
                answer.Correct = outcome.Correct;
                answer.AnsweredAt = Now;
            }
            await SaveAndNotifyAsync(match, lobby: false);
        }
        return await GetStateAsync(userId, matchId, wrong);
    }

    /// <summary>"Ik pas" (of "Ik ben klaar" bij een opstelling): een opstelling en een counter houden hun deelpunten.</summary>
    public async Task<MatchState> GiveUpAsync(string userId, int matchId)
    {
        var (match, question, answer) = await PlayableAsync(userId, matchId);
        if (question is not null && answer is { AnsweredAt: null })
        {
            answer.GaveUp = true;
            answer.AnsweredAt = Now;
            answer.SpeedPoints = QuestionEngine.PointsWhenStopped(AsDaily(question), AsAttempt(userId, answer));
            await SaveAndNotifyAsync(match, lobby: false);
        }
        return await GetStateAsync(userId, matchId);
    }

    /// <summary>De lopende vraag en jouw antwoordrij, als je op dit moment mag antwoorden.</summary>
    private async Task<(Match, MatchQuestion?, MatchAnswer?)> PlayableAsync(string userId, int matchId)
    {
        var match = await LoadAsync(matchId);
        var me = match.Players.SingleOrDefault(p => p.UserId == userId && p.LeftAt == null);
        var question = CurrentQuestion(match);
        if (me is null || match.Status != MatchStatus.Running || question is null || PhaseOf(match, question) != MatchPhase.Question)
            return (match, null, null);
        if (Elapsed(question) > TimeLimit(question) + GameRules.LatencyGraceSeconds)
            return (match, null, null);

        var answer = question.Answers.SingleOrDefault(a => a.UserId == userId);
        if (answer is null)
        {
            answer = new MatchAnswer { UserId = userId };
            question.Answers.Add(answer);
        }
        return (match, question, answer);
    }

    // ---------- Verloop in de tijd (aangeroepen door MatchEngine) ----------

    /// <summary>Zet alle lopende wedstrijden een stap verder waar nodig. Geeft het aantal gewijzigde wedstrijden terug.</summary>
    public async Task<int> AdvanceAllAsync()
    {
        var changed = 0;

        var staleBefore = Now - MatchRules.LobbyTimeout;
        var stale = await db.Matches.Where(m => m.Status == MatchStatus.Lobby && m.CreatedAt < staleBefore).ToListAsync();
        foreach (var m in stale) m.Status = MatchStatus.Cancelled;
        if (stale.Count > 0)
        {
            await db.SaveChangesAsync();
            await notifier.LobbyChangedAsync();
            changed += stale.Count;
        }

        var running = await db.Matches.Where(m => m.Status == MatchStatus.Running).Select(m => m.Id).ToListAsync();
        foreach (var id in running)
        {
            var match = await LoadAsync(id);
            if (!Advance(match)) continue;
            await SaveAndNotifyAsync(match, lobby: match.Status != MatchStatus.Running);
            changed++;
        }
        return changed;
    }

    private bool Advance(Match match)
    {
        var active = match.Players.Where(p => p.LeftAt == null).ToList();
        if (active.Count == 0)
        {
            match.Status = MatchStatus.Cancelled;
            match.FinishedAt = Now;
            return true;
        }

        var question = CurrentQuestion(match);
        if (question is null || Now < question.StartsAt) return false;

        if (question.EndedAt is null)
        {
            var timeUp = Elapsed(question) > TimeLimit(question) + GameRules.LatencyGraceSeconds;
            var allDone = active.All(p => question.Answers.Any(a => a.UserId == p.UserId && a.AnsweredAt != null));
            if (!timeUp && !allDone) return false;
            EndQuestion(match, question);
            return true;
        }

        if ((Now - question.EndedAt.Value).TotalSeconds < MatchRules.RevealSeconds) return false;

        var next = match.Questions.SingleOrDefault(q => q.Number == question.Number + 1);
        if (next is not null) next.StartsAt = Now;
        else Finish(match);
        return true;
    }

    /// <summary>
    /// Wie het eerst juist was krijgt de grootste bonus; alle punten gaan naar de tussenstand
    /// (ook deelpunten: een jaartal dat er net naast zit, of een half ingevulde opstelling of counter).
    /// </summary>
    private void EndQuestion(Match match, MatchQuestion question)
    {
        question.EndedAt = Now;
        var daily = AsDaily(question);
        foreach (var answer in question.Answers.Where(a => a.AnsweredAt is null))
            answer.SpeedPoints = QuestionEngine.PointsWhenStopped(daily, AsAttempt(answer.UserId, answer));

        var position = 1;
        foreach (var answer in question.Answers.Where(a => a.Correct).OrderBy(a => a.AnsweredAt))
        {
            answer.Position = position;
            answer.Bonus = MatchRules.BonusFor(position);
            position++;
        }
        foreach (var answer in question.Answers)
            match.Players.Single(p => p.UserId == answer.UserId).Score += answer.SpeedPoints + answer.Bonus;
    }

    private void Finish(Match match)
    {
        match.Status = MatchStatus.Finished;
        match.FinishedAt = Now;
        var ordered = match.Players.OrderByDescending(p => p.Score).ToList();
        for (var i = 0; i < ordered.Count; i++)
            ordered[i].Rank = i > 0 && ordered[i - 1].Score == ordered[i].Score ? ordered[i - 1].Rank : i + 1;
    }

    // ---------- Toestand ----------

    public async Task<MatchState> GetStateAsync(string userId, int matchId, string? lastWrong = null)
    {
        var match = await LoadAsync(matchId);
        var question = CurrentQuestion(match);
        var phase = PhaseOf(match, question);
        var cards = await results.UserCardsAsync();

        var myAnswer = question?.Answers.SingleOrDefault(a => a.UserId == userId);

        var hints = new List<string>();
        double elapsed = 0, limit = GameRules.TimeLimitSeconds;
        QuestionView? view = null;
        AnswerResult? result = null;
        string? answerLabel = null;
        if (question is not null)
        {
            var daily = AsDaily(question);
            var attempt = AsAttempt(userId, myAnswer);
            limit = QuestionEngine.TimeLimit(daily);
            switch (phase)
            {
                case MatchPhase.Countdown:
                    elapsed = Elapsed(question); // negatief: aftellen
                    break;
                case MatchPhase.Question:
                    elapsed = Elapsed(question);
                    (hints, view) = QuestionEngine.Visible(daily, attempt, elapsed);
                    break;
                case MatchPhase.Reveal:
                    elapsed = (Now - question.EndedAt!.Value).TotalSeconds;
                    result = QuestionEngine.Result(daily, attempt);
                    (_, view) = QuestionEngine.Visible(daily, attempt, limit);
                    hints = result.AllHints;
                    answerLabel = QuestionEngine.AnswerLabel(daily);
                    break;
            }
        }

        var players = match.Players
            .OrderBy(p => p.Rank ?? int.MaxValue).ThenByDescending(p => p.Score).ThenBy(p => p.JoinedAt)
            .Select(p => new MatchPlayerState(
                p.User.UserName!, cards.GetValueOrDefault(p.UserId)?.AvatarId, cards.GetValueOrDefault(p.UserId)?.Level ?? 1,
                p.Score, p.UserId == match.HostUserId, p.UserId == userId, p.LeftAt is not null,
                question?.Answers.Any(a => a.UserId == p.UserId && a.AnsweredAt != null) == true, p.Rank))
            .ToList();

        // Eerst wie juist was (in volgorde van aankomst), daarna wie deelpunten haalde.
        var reveal = phase is MatchPhase.Reveal && question is not null
            ? question.Answers.Where(a => a.SpeedPoints + a.Bonus > 0)
                .OrderBy(a => a.Position ?? int.MaxValue).ThenByDescending(a => a.SpeedPoints)
                .Select(a => new RevealEntry(a.User.UserName!, cards.GetValueOrDefault(a.UserId)?.AvatarId,
                    a.Position ?? 0, a.SpeedPoints + a.Bonus, a.Bonus, a.UserId == userId))
                .ToList()
            : [];

        var types = TypesOf(match.Types);

        return new MatchState(
            match.Id, match.Name, match.Status, phase, match.Seats,
            match.HostUserId == userId,
            match.Players.Any(p => p.UserId == userId && p.LeftAt == null),
            players,
            question?.Number ?? 0,
            hints,
            elapsed,
            myAnswer?.WrongGuesses ?? 0,
            lastWrong,
            myAnswer?.Correct == true,
            myAnswer?.AnsweredAt != null,
            answerLabel,
            reveal,
            types,
            match.Questions.Count > 0 ? match.Questions.Count : QuestionTypes.Mix(types).Values.Sum(),
            question?.Type ?? types[0],
            limit,
            view,
            result,
            (myAnswer?.SpeedPoints ?? 0) + (myAnswer?.Bonus ?? 0));
    }

    /// <summary>De vraag die nu loopt of net afgelopen is: de laatst gestarte.</summary>
    public static MatchQuestion? CurrentQuestion(Match match) =>
        match.Questions.Where(q => q.StartsAt is not null).OrderByDescending(q => q.Number).FirstOrDefault();

    public MatchPhase PhaseOf(Match match, MatchQuestion? question) => match.Status switch
    {
        MatchStatus.Lobby => MatchPhase.Lobby,
        MatchStatus.Finished => MatchPhase.Finished,
        MatchStatus.Cancelled => MatchPhase.Cancelled,
        _ when question is null => MatchPhase.Countdown,
        _ when Now < question.StartsAt => MatchPhase.Countdown,
        _ when question.EndedAt is null => MatchPhase.Question,
        _ => MatchPhase.Reveal,
    };

    private async Task<Match> LoadAsync(int matchId)
    {
        var match = await db.Matches
            .Include(m => m.Players).ThenInclude(p => p.User)
            .Include(m => m.Questions).ThenInclude(q => q.Player)
            .Include(m => m.Questions).ThenInclude(q => q.QuizItem)
            .Include(m => m.Questions).ThenInclude(q => q.Answers).ThenInclude(a => a.User)
            .AsSplitQuery()
            .SingleOrDefaultAsync(m => m.Id == matchId)
            ?? throw new QuizException("Deze wedstrijd bestaat niet.");

        // Counter: de spelers staan als lijst van ids bij de vraag.
        var ids = match.Questions.SelectMany(q => q.CounterPlayerIds).Distinct().ToList();
        if (ids.Count > 0)
        {
            var players = await db.Players.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
            foreach (var q in match.Questions)
                q.CounterPlayers = q.CounterPlayerIds.Where(players.ContainsKey).Select(id => players[id]).ToList();
        }
        return match;
    }

    private async Task SaveAndNotifyAsync(Match match, bool lobby = true)
    {
        await db.SaveChangesAsync();
        await notifier.MatchChangedAsync(match.Id);
        if (lobby) await notifier.LobbyChangedAsync();
    }
}
