using Microsoft.EntityFrameworkCore;
using Npgsql;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Services;

/// <summary>
/// Online wedstrijden: de lobby (aanmaken, joinen, verlaten, starten), antwoorden, het verloop in de tijd en de toestand
/// voor de client. De organisator kiest een vraagtype of een mix; wat per type verschilt, doet <see cref="QuestionEngine"/>
/// (net als in de daily). De vragen kiest <see cref="QuestionPicker"/>; een vraag afrekenen en de eindstand doet
/// <see cref="MatchScoring"/>. <see cref="MatchEngine"/> roept <see cref="AdvanceAllAsync"/> op om de klok te laten lopen.
/// Een derby-partijtje heeft daarbovenop wapens (zie <see cref="DerbyService"/> en <see cref="DerbyRules"/>).
/// </summary>
public class MatchService(
    QuizDbContext db, TimeProvider clock, ResultsService results, QuestionPicker picker, DerbyService derby, IMatchNotifier notifier,
    Random? random = null)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private readonly Random _random = random ?? Random.Shared;

    // ---------- Lobby ----------

    private record LobbyRow(
        int Id, string Name, string HostName, string? HostAvatar, int Seats, MatchStatus Status,
        DateTime CreatedAt, DateTime? FinishedAt, int Players, bool IAmIn, bool Played, List<QuestionType> Types, bool Derby, int Questions);

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
                m.Types,
                m.Derby,
                m.QuestionCount))
            .ToListAsync();

        static MatchSummary Summary(LobbyRow m) =>
            new(m.Id, m.Name, m.HostName, m.HostAvatar, m.Seats, m.Players, m.Status, m.IAmIn, m.CreatedAt, TypesOf(m.Types), m.Derby, m.Questions);

        return new LobbyOverview(
            matches.Where(m => m.Status == MatchStatus.Lobby && !m.IAmIn && m.Players < m.Seats).Select(m => Summary(m)).ToList(),
            matches.Where(m => m.IAmIn && m.Status is MatchStatus.Lobby or MatchStatus.Running).Select(m => Summary(m)).ToList(),
            matches.Where(m => m.Status == MatchStatus.Finished && m.Played).OrderByDescending(m => m.FinishedAt).Take(5).Select(m => Summary(m)).ToList());
    }

    public async Task<int> CreateAsync(string userId, string userName, CreateMatchRequest req)
    {
        var name = string.IsNullOrWhiteSpace(req.Name) ? $"Partijtje van {userName}" : req.Name.Trim();
        if (name.Length > 40) name = name[..40];

        var types = TypesOf(req.Types);
        // Solo-types (De Krant, de kaart) vullen een hele dag en kunnen niet online.
        if (types.Where(t => QuestionTypes.Get(t).Alone).ToList() is [var notOnline, ..])
            throw new QuizException($"{QuestionTypes.Get(notOnline).Name} kan niet online. Kies een ander type.");
        var active = await picker.ActiveTypesAsync();
        if (types.Any(t => !active.Contains(t)))
            throw new QuizException("Dat vraagtype staat momenteel uit. Kies een ander type.");
        if (req.Derby && types.Where(t => !DerbyRules.AllowedIn(t)).ToList() is [var notInDerby, ..])
            throw new QuizException($"{QuestionTypes.Get(notInDerby).Name} kan niet in een derby. Kies een ander type.");
        var count = MatchRules.ClampQuestions(req.Questions, types.Count);
        if (await picker.BuildOnlineAsync(types, count) is null)
            throw new QuizException("Er zijn nog niet genoeg vragen voor deze keuze. Kies een ander type of een mix.");

        var match = new Match
        {
            Name = name,
            HostUserId = userId,
            Seats = Math.Clamp(req.Seats, MatchRules.MinSeats, MatchRules.MaxSeats),
            Types = types,
            Derby = req.Derby,
            QuestionCount = count,
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
        var (match, saved) = await ChangeAsync(matchId, match =>
        {
            if (match.Players.Any(p => p.UserId == userId)) return false;
            if (match.Status != MatchStatus.Lobby) throw new QuizException("Deze wedstrijd is al begonnen.");
            if (match.Players.Count >= match.Seats) throw new QuizException("Dit partijtje is vol.");

            match.Players.Add(new MatchPlayer { UserId = userId, JoinedAt = Now });
            return true;
        });
        if (saved) await NotifyAsync(match, lobby: true);
    }

    public async Task LeaveAsync(string userId, int matchId)
    {
        var (match, _) = await ChangeAsync(matchId, match => Leave(match, userId));
        await NotifyAsync(match, lobby: true);
    }

    private bool Leave(Match match, string userId)
    {
        var me = match.Players.SingleOrDefault(p => p.UserId == userId) ?? throw new QuizException("Je speelt niet mee in dit partijtje.");

        if (match.Status == MatchStatus.Lobby)
        {
            if (match.HostUserId == userId) match.Status = MatchStatus.Cancelled; // organisator weg = partijtje weg
            else match.Players.Remove(me);
        }
        else if (match.Status == MatchStatus.Running && me.LeftAt is null)
        {
            // Wie een lopende wedstrijd verlaat, verliest al zijn punten en eindigt onder wie bleef.
            me.LeftAt = Now;
            me.Score = 0;
            // Wie weg is, kan niemand meer schaden: wapens die nog niet afgerekend zijn, vervallen.
            foreach (var q in match.Questions.Where(q => q.EndedAt == null))
            {
                if (q.TrumpUserId == userId) q.TrumpUserId = null;
                if (q.FlareUserId == userId) (q.FlareUserId, q.FlareAt) = (null, null);
                foreach (var a in q.Answers)
                {
                    if (a.RedCardFromUserId == userId) a.RedCardFromUserId = null;
                    if (a.StolenByUserId == userId) a.StolenByUserId = null;
                }
            }
        }
        return true;
    }

    public async Task StartAsync(string userId, int matchId)
    {
        var (match, _) = await ChangeAsync(matchId, async match => { await StartAsync(match, userId); return true; });
        await NotifyAsync(match, lobby: true);
    }

    private async Task StartAsync(Match match, string userId)
    {
        if (match.HostUserId != userId) throw new QuizException("Enkel de organisator kan starten.");
        if (match.Status != MatchStatus.Lobby) throw new QuizException("Deze wedstrijd is al begonnen.");
        if (match.Players.Count < MatchRules.MinSeats) throw new QuizException("Je hebt minstens 2 spelers nodig.");

        // Werd een type intussen uitgeschakeld, dan valt het weg; de rest van de mix blijft.
        var active = await picker.ActiveTypesAsync();
        var types = TypesOf(match.Types).Where(active.Contains).ToList();
        if (types.Count == 0) throw new QuizException("Het gekozen vraagtype staat intussen uit. Maak een nieuw partijtje.");

        // Willekeurige vragen, maar niets uit de daily van vandaag (anders verklap je die).
        var questions = await picker.BuildOnlineAsync(types, match.QuestionCount)
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

        if (match.Derby)
        {
            // Elke speler één wapen; Trump enkel als er keuzevragen in het partijtje komen.
            var players = match.Players.OrderBy(p => p.JoinedAt).ToList();
            var armed = questions.Skip(1).SkipLast(1); // de eerste en de laatste vraag: wapenstilstand
            var weapons = DerbyRules.Deal(players.Count, armed.Any(q => DerbyRules.TrumpWorksOn(q.Type)), _random);
            for (var i = 0; i < players.Count; i++) players[i].Weapon = weapons[i];
        }

        match.Questions[0].StartsAt = Now.AddSeconds(MatchRules.CountdownSeconds);
        match.Status = MatchStatus.Running;
        match.StartedAt = Now;
    }

    /// <summary>De types van een partijtje; leeg (oudere partijtjes) = "Wie ben ik?".</summary>
    private static List<QuestionType> TypesOf(List<QuestionType>? types) =>
        types is { Count: > 0 } ? [.. types.Distinct().Order()] : [QuestionType.WieBenIk];

    // ---------- Spelen ----------

    public async Task<MatchState> GuessAsync(string userId, int matchId, string? guess)
    {
        var trimmed = guess?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return await GetStateAsync(userId, matchId);

        string? wrong = null;
        int? guessesSeen = null;
        var (match, saved) = await ChangeAsync(matchId, match =>
        {
            if (Playable(match, userId) is not (var question, var answer)) return false;
            // Kwam er tegelijk een andere gok van jou binnen (dubbele tik), dan telt enkel die, zoals in de daily.
            if (guessesSeen is { } seen && seen != answer.Guesses) return false;
            guessesSeen = answer.Guesses;
            // Dezelfde regels als de daily (ook het maximum aantal pogingen), maar met de gedeelde klok van de vraag.
            wrong = AttemptFlow.GuessAt(question, answer, trimmed, Now, MatchScoring.ScoringElapsed(question, answer, Now));
            return true;
        });
        if (saved) await NotifyAsync(match, lobby: false);
        else wrong = null;
        return await GetStateAsync(userId, matchId, wrong);
    }

    /// <summary>"Ik pas" (of "Ik ben klaar" bij een opstelling): een opstelling en een counter houden hun deelpunten.</summary>
    public async Task<MatchState> GiveUpAsync(string userId, int matchId)
    {
        var (match, saved) = await ChangeAsync(matchId, match =>
        {
            if (Playable(match, userId) is not (var question, var answer)) return false;
            AttemptFlow.GiveUp(question, answer, Now);
            answer.GaveUp = true;
            return true;
        });
        if (saved) await NotifyAsync(match, lobby: false);
        return await GetStateAsync(userId, matchId);
    }

    /// <summary>De lopende vraag en jouw (nog open) antwoordrij, als je op dit moment mag antwoorden; anders null.</summary>
    private (MatchQuestion Question, MatchAnswer Answer)? Playable(Match match, string userId)
    {
        var me = match.Players.SingleOrDefault(p => p.UserId == userId && p.LeftAt == null);
        var question = CurrentQuestion(match);
        if (me is null || match.Status != MatchStatus.Running || question is null || PhaseOf(match, question, Now) != MatchPhase.Question)
            return null;
        var answer = question.Answers.SingleOrDefault(a => a.UserId == userId);
        if (MatchScoring.Elapsed(question, Now) > MatchScoring.LimitFor(question, answer) + GameRules.LatencyGraceSeconds)
            return null;
        if (answer is { AnsweredAt: not null }) return null;

        if (answer is null)
        {
            answer = new MatchAnswer { UserId = userId };
            question.Answers.Add(answer);
        }
        answer.StartedAt = question.StartsAt!.Value; // de gedeelde klok (ook na een wissel of een wapen vooraf)
        return (question, answer);
    }

    // ---------- Derby ----------

    /// <summary>
    /// Zet je wapen in (zie <see cref="DerbyService.UseWeaponAsync"/>) en geeft de nieuwe toestand. In één transactie:
    /// de claims in de database draaien mee terug als het bewaren botst en opnieuw begint. De claim lockt de rij van de
    /// werper vóór de rest bewaard wordt, dus de engine kan in PostgreSQL in een deadlock met dit verzoek komen; ook dat
    /// telt als botsing (zie <see cref="IsRace"/>).
    /// </summary>
    public async Task<MatchState> UseWeaponAsync(string userId, int matchId, string? target)
    {
        var (match, _) = await ChangeAsync(matchId, async match =>
        {
            await derby.UseWeaponAsync(match, userId, target);
            return true;
        }, transaction: true);
        await NotifyAsync(match, lobby: false);
        return await GetStateAsync(userId, matchId);
    }

    // ---------- Verloop in de tijd (aangeroepen door MatchEngine) ----------

    /// <summary>Zet alle lopende wedstrijden een stap verder waar nodig. Geeft het aantal gewijzigde wedstrijden terug.</summary>
    public async Task<int> AdvanceAllAsync()
    {
        var changed = 0;

        var staleBefore = Now - MatchRules.LobbyTimeout;
        var stale = await db.Matches.Where(m => m.Status == MatchStatus.Lobby && m.CreatedAt < staleBefore)
            .Select(m => m.Id).ToListAsync();
        foreach (var id in stale)
        {
            var (_, saved) = await ChangeAsync(id, m =>
            {
                if (m.Status != MatchStatus.Lobby) return false; // intussen gestart of geannuleerd
                m.Status = MatchStatus.Cancelled;
                return true;
            });
            if (saved) changed++;
        }
        if (changed > 0) await notifier.LobbyChangedAsync();

        var running = await db.Matches.Where(m => m.Status == MatchStatus.Running).Select(m => m.Id).ToListAsync();
        foreach (var id in running)
        {
            Match match;
            bool saved;
            try
            {
                (match, saved) = await ChangeAsync(id, Advance);
            }
            catch (QuizException)
            {
                continue; // bleef botsen: de volgende tick probeert het opnieuw, de andere wedstrijden lopen door
            }
            if (!saved) continue;
            await NotifyAsync(match, lobby: match.Status != MatchStatus.Running);
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
            // Met een verlenging loopt de vraag door tot ook die speler zijn tijd op is.
            var extra = question.Answers.Select(a => a.ExtraSeconds).DefaultIfEmpty(0).Max();
            var timeUp = MatchScoring.Elapsed(question, Now) > MatchScoring.TimeLimit(question) + extra + GameRules.LatencyGraceSeconds;
            var allDone = active.All(p => question.Answers.Any(a => a.UserId == p.UserId && a.AnsweredAt != null));
            if (!timeUp && !allDone) return false;
            MatchScoring.EndQuestion(match, question, Now);
            return true;
        }

        if ((Now - question.EndedAt.Value).TotalSeconds < MatchRules.RevealSeconds) return false;

        var next = match.Questions.SingleOrDefault(q => q.Number == question.Number + 1);
        if (next is not null) next.StartsAt = Now;
        else MatchScoring.Finish(match, Now);
        return true;
    }

    // ---------- Toestand ----------

    public async Task<MatchState> GetStateAsync(string userId, int matchId, string? lastWrong = null)
    {
        var match = await LoadAsync(matchId);
        var question = CurrentQuestion(match);
        var phase = PhaseOf(match, question, Now);
        var cards = await results.UserCardsAsync();

        var myAnswer = question?.Answers.SingleOrDefault(a => a.UserId == userId);

        var hints = new List<string>();
        double elapsed = 0, limit = GameRules.TimeLimitSeconds;
        QuestionView? view = null;
        AnswerResult? result = null;
        string? answerLabel = null;
        if (question is not null)
        {
            var attempt = myAnswer ?? new MatchAnswer { UserId = userId, StartedAt = question.StartsAt ?? Now }; // nog niet gespeeld: leeg
            limit = MatchScoring.LimitFor(question, myAnswer); // met een verlenging: jouw eigen, langere tijd
            switch (phase)
            {
                case MatchPhase.Countdown:
                    elapsed = MatchScoring.Elapsed(question, Now); // negatief: aftellen
                    break;
                case MatchPhase.Question:
                    elapsed = MatchScoring.Elapsed(question, Now);
                    (hints, view) = QuestionEngine.Visible(question, attempt, elapsed);
                    break;
                case MatchPhase.Reveal:
                    // Na een Trump booster toont de reveal het "officiële" antwoord (het echte staat in de derby-info).
                    var judged = MatchScoring.Judged(question);
                    elapsed = (Now - question.EndedAt!.Value).TotalSeconds;
                    result = QuestionEngine.Result(judged, attempt);
                    (_, view) = QuestionEngine.Visible(judged, attempt, limit);
                    hints = result.AllHints;
                    answerLabel = QuestionEngine.AnswerLabel(judged);
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
            ? question.Answers.Where(a => a.Points + a.Bonus > 0 && match.Players.Any(p => p.UserId == a.UserId && p.LeftAt == null))
                .OrderBy(a => a.Position ?? int.MaxValue).ThenByDescending(a => a.Points)
                .Select(a => new RevealEntry(a.User.UserName!, cards.GetValueOrDefault(a.UserId)?.AvatarId,
                    a.Position ?? 0, a.Points + a.Bonus, a.Bonus, a.UserId == userId))
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
            match.Questions.Count > 0 ? match.Questions.Count : match.QuestionCount,
            question?.Type ?? types[0],
            limit,
            view,
            result,
            (myAnswer?.Points ?? 0) + (myAnswer?.Bonus ?? 0),
            match.Derby ? derby.State(match, question, phase, userId, cards) : null);
    }

    /// <summary>De vraag die nu loopt of net afgelopen is: de laatst gestarte.</summary>
    public static MatchQuestion? CurrentQuestion(Match match) =>
        match.Questions.Where(q => q.StartsAt is not null).OrderByDescending(q => q.Number).FirstOrDefault();

    public static MatchPhase PhaseOf(Match match, MatchQuestion? question, DateTime now) => match.Status switch
    {
        MatchStatus.Lobby => MatchPhase.Lobby,
        MatchStatus.Finished => MatchPhase.Finished,
        MatchStatus.Cancelled => MatchPhase.Cancelled,
        _ when question is null => MatchPhase.Countdown,
        _ when now < question.StartsAt => MatchPhase.Countdown,
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
        await db.LoadCounterPlayersAsync(match.Questions);
        return match;
    }

    /// <summary>Hoe vaak een wijziging aan een wedstrijd opnieuw begint als iemand anders tegelijk schreef.</summary>
    private const int MaxSaveAttempts = 5;

    /// <summary>
    /// Elke wijziging aan een wedstrijd loopt hierlangs: laden, <paramref name="change"/> toepassen (false = niets te
    /// bewaren) en bewaren met <see cref="Match.Version"/> als concurrency token. Schreef intussen iemand anders (de engine,
    /// een andere speler, een tweede tik), dan begint het opnieuw vanaf een verse toestand, begrensd door
    /// <see cref="MaxSaveAttempts"/>. Zo rekent de engine nooit af op een verouderde toestand en komt een gok die
    /// geladen werd voor het afsluiten van een vraag er niet meer na. Gaat uit van één serverinstantie (zie README).
    /// </summary>
    private async Task<(Match Match, bool Saved)> ChangeAsync(int matchId, Func<Match, Task<bool>> change, bool transaction = false)
    {
        for (var attempt = 1; ; attempt++)
        {
            await using var tx = transaction ? await db.Database.BeginTransactionAsync() : null;
            var match = await LoadAsync(matchId);
            try
            {
                if (!await change(match)) return (match, false);
                match.Version++;
                await db.SaveChangesAsync();
                if (tx is not null) await tx.CommitAsync();
                return (match, true);
            }
            catch (Exception ex) when (IsRace(ex) && attempt < MaxSaveAttempts)
            {
                db.ChangeTracker.Clear(); // de transactie (als die er is) draait terug bij het verlaten van de lus
            }
            catch (Exception ex) when (IsRace(ex))
            {
                db.ChangeTracker.Clear();
                throw new QuizException("Het is even druk in dit partijtje. Probeer het opnieuw.");
            }
        }
    }

    /// <summary>
    /// Iemand schreef tegelijk: de versie klopte niet meer, of een nieuwe rij (je eerste antwoord op een vraag, joinen)
    /// botste op de unieke index omdat een ander verzoek ze net aanmaakte, of PostgreSQL brak een deadlock af.
    /// </summary>
    private static bool IsRace(Exception ex) => ex switch
    {
        DbUpdateConcurrencyException => true,
        DbUpdateException u when u.Entries.Any(e => e.State == EntityState.Added && e.Entity is MatchAnswer or MatchPlayer) => true,
        _ => IsDeadlock(ex),
    };

    private static bool IsDeadlock(Exception? ex)
    {
        for (; ex is not null; ex = ex.InnerException)
            if (ex is PostgresException { SqlState: PostgresErrorCodes.DeadlockDetected }) return true;
        return false;
    }

    private Task<(Match Match, bool Saved)> ChangeAsync(int matchId, Func<Match, bool> change) =>
        ChangeAsync(matchId, match => Task.FromResult(change(match)));

    private async Task NotifyAsync(Match match, bool lobby)
    {
        await notifier.MatchChangedAsync(match.Id);
        if (lobby) await notifier.LobbyChangedAsync();
    }
}
