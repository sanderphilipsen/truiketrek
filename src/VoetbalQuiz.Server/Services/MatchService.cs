using Microsoft.EntityFrameworkCore;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Services;

/// <summary>
/// Online wedstrijden: partijtjes aanmaken, joinen, starten en antwoorden.
/// De organisator kiest een vraagtype of een mix; wat per type verschilt, doet <see cref="QuestionEngine"/> (net als in de daily).
/// De overgangen in de tijd (vraag afsluiten, volgende vraag, einde) doet <see cref="MatchEngine"/>.
/// Een derby-partijtje heeft daarbovenop wapens (zie <see cref="DerbyRules"/>).
/// </summary>
public class MatchService(
    QuizDbContext db, TimeProvider clock, ResultsService results, QuizService quiz, IMatchNotifier notifier, Random? random = null)
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
        var active = await quiz.ActiveTypesAsync();
        if (types.Any(t => !active.Contains(t)))
            throw new QuizException("Dat vraagtype staat momenteel uit. Kies een ander type.");
        var count = MatchRules.ClampQuestions(req.Questions, types.Count);
        if (await quiz.BuildOnlineAsync(types, count) is null)
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
        var match = await LoadAsync(matchId);
        if (match.Players.Any(p => p.UserId == userId)) return;
        if (match.Status != MatchStatus.Lobby) throw new QuizException("Deze wedstrijd is al begonnen.");
        if (match.Players.Count >= match.Seats) throw new QuizException("Dit partijtje is vol.");

        match.Players.Add(new MatchPlayer { UserId = userId, JoinedAt = Now });
        await SaveAndNotifyAsync(match);
    }

    public async Task LeaveAsync(string userId, int matchId)
    {
        var match = await LoadAsync(matchId);
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
        if (types.Count == 0) throw new QuizException("Het gekozen vraagtype staat intussen uit. Maak een nieuw partijtje.");

        // Willekeurige vragen, maar niets uit de daily van vandaag (anders verklap je die).
        var questions = await quiz.BuildOnlineAsync(types, match.QuestionCount)
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
        await SaveAndNotifyAsync(match);
    }

    /// <summary>De types van een partijtje; leeg (oudere partijtjes) = "Wie ben ik?".</summary>
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

    /// <summary>De tijd van deze speler voor deze vraag: met een verlenging (derby) wat langer.</summary>
    private static double LimitFor(MatchQuestion q, MatchAnswer? a) => TimeLimit(q) + (a?.ExtraSeconds ?? 0);

    /// <summary>
    /// De tijd waarmee een antwoord gescoord wordt. In de verlenging telt het als op de valreep
    /// (de laagste punten van een juist antwoord), niet als te laat.
    /// </summary>
    private double ScoringElapsed(MatchQuestion q, MatchAnswer a) =>
        a.ExtraSeconds > 0 ? Math.Min(Elapsed(q), TimeLimit(q) - 0.01) : Elapsed(q);

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
            var outcome = QuestionEngine.Guess(AsDaily(question), attempt, trimmed, ScoringElapsed(question, answer));
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
        var answer = question.Answers.SingleOrDefault(a => a.UserId == userId);
        if (Elapsed(question) > LimitFor(question, answer) + GameRules.LatencyGraceSeconds)
            return (match, null, null);

        if (answer is null)
        {
            answer = new MatchAnswer { UserId = userId };
            question.Answers.Add(answer);
        }
        return (match, question, answer);
    }

    // ---------- Derby ----------

    /// <summary>
    /// Zet je wapen in. Tijdens een vraag heeft het effect op die vraag; tijdens het aftellen of de tussenstand
    /// wordt het klaargezet voor de vraag die komt.
    /// </summary>
    public async Task<MatchState> UseWeaponAsync(string userId, int matchId, string? target)
    {
        var match = await LoadAsync(matchId);
        if (!match.Derby) throw new QuizException("Dit is geen derby.");
        var me = match.Players.SingleOrDefault(p => p.UserId == userId && p.LeftAt == null)
            ?? throw new QuizException("Je speelt niet mee in dit partijtje.");
        if (me.Weapon is not { } weapon) throw new QuizException("Je hebt geen wapen.");

        var targetPlayer = DerbyRules.NeedsTarget(weapon)
            ? match.Players.SingleOrDefault(p => p.User.UserName == target && p.LeftAt == null && p.UserId != userId)
                ?? throw new QuizException("Kies eerst een tegenstander.")
            : null;
        var (question, elapsed) = WeaponTarget(match);
        if (WhyNot(match, me, question, elapsed, targetPlayer) is { } why) throw new QuizException(why);
        await ClaimAsync(me, question!, weapon);

        switch (weapon)
        {
            case DerbyWeapon.RodeKaart:
                AnswerOf(question!, targetPlayer!.UserId).RedCardFromUserId = userId;
                me.WeaponTargetUserId = targetPlayer.UserId;
                break;
            case DerbyWeapon.PropereHanden:
                AnswerOf(question!, targetPlayer!.UserId).StolenByUserId = userId;
                me.WeaponTargetUserId = targetPlayer.UserId;
                break;
            case DerbyWeapon.Gokchinees:
                AnswerOf(question!, userId).Gamble = true;
                break;
            case DerbyWeapon.Verlenging:
                AnswerOf(question!, userId).ExtraSeconds = DerbyRules.ExtensionSeconds;
                break;
            case DerbyWeapon.BengaalsVuur:
                question!.FlareUserId = userId;
                question.FlareAt = Now;
                break;
            case DerbyWeapon.Trump:
                question!.TrumpUserId = userId;
                break;
            case DerbyWeapon.Wissel:
                await SwapAsync(match, question!);
                question!.SwapUserId = userId;
                break;
        }
        me.WeaponUsedAt = Now;
        me.WeaponQuestion = question!.Number;
        await SaveAndNotifyAsync(match, lobby: false);
        return await GetStateAsync(userId, matchId);
    }

    /// <summary>De antwoordrij van een speler bij een vraag; bestaat ze nog niet, dan wordt ze aangemaakt.</summary>
    private static MatchAnswer AnswerOf(MatchQuestion question, string userId)
    {
        var answer = question.Answers.SingleOrDefault(a => a.UserId == userId);
        if (answer is null)
        {
            answer = new MatchAnswer { UserId = userId };
            question.Answers.Add(answer);
        }
        return answer;
    }

    /// <summary>
    /// Legt het wapen en (bij vuur, Trump en wissel) de plaats op de vraag vast in de database zelf,
    /// zodat twee verzoeken tegelijk (dubbel tikken, of twee spelers samen) nooit allebei slagen.
    /// </summary>
    private async Task ClaimAsync(MatchPlayer me, MatchQuestion question, DerbyWeapon weapon)
    {
        var questions = db.MatchQuestions.Where(q => q.Id == question.Id);
        var slot = weapon switch
        {
            DerbyWeapon.Trump => await questions.Where(q => q.TrumpUserId == null)
                .ExecuteUpdateAsync(s => s.SetProperty(q => q.TrumpUserId, me.UserId)),
            DerbyWeapon.BengaalsVuur => await questions.Where(q => q.FlareUserId == null)
                .ExecuteUpdateAsync(s => s.SetProperty(q => q.FlareUserId, me.UserId)),
            DerbyWeapon.Wissel => await questions.Where(q => q.SwapUserId == null && q.EndedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(q => q.SwapUserId, me.UserId)),
            _ => 1,
        };
        if (slot == 0) throw new QuizException("Iemand was je net voor bij deze vraag.");

        var now = Now;
        var claimed = await db.MatchPlayers.Where(p => p.Id == me.Id && p.WeaponUsedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.WeaponUsedAt, now));
        if (claimed > 0) return;

        // Wapen was al op: de plaats op de vraag terug vrijgeven.
        switch (weapon)
        {
            case DerbyWeapon.Trump:
                await questions.Where(q => q.TrumpUserId == me.UserId).ExecuteUpdateAsync(s => s.SetProperty(q => q.TrumpUserId, (string?)null));
                break;
            case DerbyWeapon.BengaalsVuur:
                await questions.Where(q => q.FlareUserId == me.UserId).ExecuteUpdateAsync(s => s.SetProperty(q => q.FlareUserId, (string?)null));
                break;
            case DerbyWeapon.Wissel:
                await questions.Where(q => q.SwapUserId == me.UserId).ExecuteUpdateAsync(s => s.SetProperty(q => q.SwapUserId, (string?)null));
                break;
        }
        throw new QuizException("Je hebt je wapen al ingezet.");
    }

    /// <summary>
    /// De vraag waarop een wapen nu effect zou hebben, en hoe lang die al loopt (negatief = start nog).
    /// Tijdens de tussenstand is dat de volgende vraag.
    /// </summary>
    private (MatchQuestion? Question, double Elapsed) WeaponTarget(Match match)
    {
        var current = CurrentQuestion(match);
        return PhaseOf(match, current) switch
        {
            MatchPhase.Countdown or MatchPhase.Question when current is not null => (current, Elapsed(current)),
            MatchPhase.Reveal => (match.Questions.SingleOrDefault(q => q.Number == current!.Number + 1), -1),
            _ => (null, 0),
        };
    }

    /// <summary>Waarom je wapen nu niet kan (null = het kan). Zelfde controle voor de knop en voor het inzetten.</summary>
    private static string? WhyNot(Match match, MatchPlayer me, MatchQuestion? question, double elapsed, MatchPlayer? target)
    {
        if (me.Weapon is not { } weapon) return "Je hebt geen wapen.";
        if (me.WeaponUsedAt is not null) return "Je hebt je wapen al ingezet.";
        if (match.Status != MatchStatus.Running || question is null) return "Nu kan je geen wapen inzetten.";
        if (question.EndedAt is not null) return "Deze vraag is al afgelopen.";
        if (DerbyRules.Ceasefire(question.Number, match.Questions.Count)) return "Wapenstilstand: op de eerste en de laatste vraag zwijgen de wapens.";
        if (weapon == DerbyWeapon.Trump && !DerbyRules.TrumpWorksOn(question.Type)) return "Trump werkt enkel bij een vraag met antwoordopties of een jaartal.";
        if (!DerbyRules.CanUse(weapon, question.Type, question.Number, match.Questions.Count, elapsed, TimeLimit(question)))
            return "Daarvoor is het nu te laat.";

        return weapon switch
        {
            DerbyWeapon.BengaalsVuur when question.FlareUserId is not null => "Er brandt al bengaals vuur bij deze vraag.",
            DerbyWeapon.Wissel when question.SwapUserId is not null => "Deze vraag werd al gewisseld.",
            DerbyWeapon.Trump when question.TrumpUserId is not null => "Er is al een Trump booster actief bij deze vraag.",
            DerbyWeapon.Trump when question.Answers.Any(a => a.UserId == me.UserId && a.AnsweredAt != null) => "Zet Trump in vóór je antwoordt.",
            // Anders is het een herkansing: fout antwoorden, het zien, en dan wisselen.
            DerbyWeapon.Wissel when question.Answers.Any(a => a.UserId == me.UserId && a.AnsweredAt != null) => "Wisselen kan enkel vóór je antwoordt.",
            DerbyWeapon.Verlenging when question.Answers.Any(a => a.UserId == me.UserId && a.AnsweredAt != null) => "Een verlenging vraag je vóór je antwoordt.",
            DerbyWeapon.RodeKaart when target is not null && Hit(question, target.UserId, weapon)
                => $"{target.User.UserName} heeft al rood bij deze vraag.",
            // Je mag de vraag eerst zien, maar gokken doe je vóór je antwoordt.
            DerbyWeapon.Gokchinees when question.Answers.Any(a => a.UserId == me.UserId && a.AnsweredAt != null) => "Gokken doe je vóór je antwoordt.",
            DerbyWeapon.PropereHanden when target is not null && Hit(question, target.UserId, weapon)
                => $"Bij {target.User.UserName} wordt al gestolen bij deze vraag.",
            DerbyWeapon.RodeKaart or DerbyWeapon.PropereHanden when target is null
                    && !match.Players.Any(p => p.UserId != me.UserId && p.LeftAt == null && !Hit(question, p.UserId, weapon))
                => "Er is niemand meer om te kiezen bij deze vraag.",
            _ => null,
        };
    }

    /// <summary>Heeft deze speler bij deze vraag al een rode kaart (of: is hij al omgekocht)?</summary>
    private static bool Hit(MatchQuestion question, string userId, DerbyWeapon weapon) =>
        question.Answers.Any(a => a.UserId == userId
            && (weapon == DerbyWeapon.PropereHanden ? a.StolenByUserId != null : a.RedCardFromUserId != null));

    /// <summary>
    /// Wissel: dezelfde nieuwe vraag (van hetzelfde type) voor iedereen, met een volle timer na het wisselbord.
    /// De antwoorden, het vuur en de Trump booster van de oude vraag vervallen; rode kaarten, omkoping en verlenging blijven staan.
    /// </summary>
    private async Task SwapAsync(Match match, MatchQuestion question)
    {
        var usedPlayers = match.Questions.SelectMany(QuestionContent.PlayersOf);
        var usedItems = match.Questions.Where(q => q.QuizItemId != null).Select(q => q.QuizItemId!.Value);
        var fresh = await quiz.BuildReplacementAsync(question.Type, usedPlayers, usedItems)
            ?? throw new QuizException("Er is geen vraag meer om mee te wisselen.");

        question.Player = fresh.PlayerId is { } playerId ? await db.Players.FindAsync(playerId) : null;
        question.PlayerId = fresh.PlayerId;
        question.QuizItem = fresh.QuizItemId is { } itemId ? await db.QuizItems.FindAsync(itemId) : null;
        question.QuizItemId = fresh.QuizItemId;
        question.Blanks = fresh.Blanks;
        question.CounterPlayerIds = fresh.CounterPlayerIds;
        question.CounterPlayers = [];
        await db.LoadCounterPlayersAsync([question]);

        foreach (var answer in question.Answers.ToList())
        {
            // Rode kaarten, stelen, gokken en verlenging blijven gelden voor de nieuwe vraag.
            if (answer is { RedCardFromUserId: null, StolenByUserId: null, ExtraSeconds: 0, Gamble: false })
            {
                db.MatchAnswers.Remove(answer);
                question.Answers.Remove(answer);
                continue;
            }
            (answer.WrongGuesses, answer.LastGuess, answer.Correct, answer.GaveUp, answer.AnsweredAt) = (0, null, false, false, null);
            (answer.SpeedPoints, answer.Choice, answer.Found, answer.Skipped) = (0, null, [], []);
        }
        (question.TrumpUserId, question.FlareUserId, question.FlareAt) = (null, null, null);
        question.StartsAt = Now.AddSeconds(DerbyRules.SwapCountdownSeconds);
    }

    /// <summary>
    /// De vraag zoals ze beoordeeld wordt. Met een Trump booster (en een antwoord van wie hem inzette)
    /// is diens keuze het juiste antwoord; anders de gewone vraag.
    /// </summary>
    private static DailyQuestion Judged(MatchQuestion question) =>
        TrumpChoice(question) is { } choice ? QuestionEngine.WithAnswer(AsDaily(question), choice) : AsDaily(question);

    private static int? TrumpChoice(MatchQuestion question) =>
        question.TrumpUserId is { } trump && DerbyRules.TrumpWorksOn(question.Type)
            ? question.Answers.SingleOrDefault(a => a.UserId == trump && a.AnsweredAt != null)?.Choice
            : null;

    /// <summary>
    /// Trump: elke keuze opnieuw beoordelen tegen het "officiële" antwoord, aan hetzelfde tempo als toen.
    /// Wat tegenstanders erdoor verliezen, komt in het derby-rapport.
    /// </summary>
    private static void ApplyTrump(Match match, MatchQuestion question)
    {
        if (TrumpChoice(question) is null) return;
        var judged = Judged(question);
        var lost = 0;
        foreach (var answer in question.Answers.Where(a => a.Choice != null && a.AnsweredAt != null))
        {
            var attempt = AsAttempt(answer.UserId, answer);
            var outcome = QuestionEngine.Guess(judged, attempt, answer.Choice!.Value.ToString(), (answer.AnsweredAt!.Value - question.StartsAt!.Value).TotalSeconds);
            if (answer.UserId != question.TrumpUserId) lost += Math.Max(0, answer.SpeedPoints - outcome.Points);
            answer.Correct = outcome.Correct;
            answer.SpeedPoints = outcome.Points;
        }
        match.Players.Single(p => p.UserId == question.TrumpUserId).WeaponEffect = lost;
    }

    /// <summary>Rode kaart: halve punten (snelheid en bonus) voor deze vraag.</summary>
    private static void ApplyRedCards(Match match, MatchQuestion question)
    {
        foreach (var answer in question.Answers.Where(a => a.RedCardFromUserId != null))
        {
            var before = answer.SpeedPoints + answer.Bonus;
            answer.SpeedPoints = DerbyRules.AfterRedCard(answer.SpeedPoints);
            answer.Bonus = DerbyRules.AfterRedCard(answer.Bonus);
            match.Players.Single(p => p.UserId == answer.RedCardFromUserId).WeaponEffect = before - answer.SpeedPoints - answer.Bonus;
        }
    }

    /// <summary>
    /// Gokchinees: punten gehaald (ook deelpunten) = dubbel, inclusief positiebonus.
    /// Niets gehaald = <see cref="DerbyRules.GamblePenalty"/> kwijt; dat geeft het terug, af te trekken na de score.
    /// </summary>
    private static List<MatchPlayer> ApplyGambles(Match match, MatchQuestion question)
    {
        var losers = new List<MatchPlayer>();
        foreach (var answer in question.Answers.Where(a => a.Gamble))
        {
            var player = match.Players.Single(p => p.UserId == answer.UserId);
            var points = answer.SpeedPoints + answer.Bonus;
            if (points > 0)
            {
                (answer.SpeedPoints, answer.Bonus) = (answer.SpeedPoints * 2, answer.Bonus * 2);
                player.WeaponEffect = points;
            }
            else losers.Add(player);
        }
        return losers;
    }

    /// <summary>
    /// Propere handen: de helft van wat het doelwit bij deze vraag haalt (na rode kaart en gok), gaat naar de dief.
    /// Geeft per dief het bedrag terug, om bij de score op te tellen.
    /// </summary>
    private static Dictionary<string, int> ApplySteals(Match match, MatchQuestion question)
    {
        var gains = new Dictionary<string, int>();
        foreach (var answer in question.Answers.Where(a => a.StolenByUserId != null))
        {
            var (speed, bonus) = (DerbyRules.Stolen(answer.SpeedPoints), DerbyRules.Stolen(answer.Bonus));
            (answer.SpeedPoints, answer.Bonus) = (answer.SpeedPoints - speed, answer.Bonus - bonus);
            gains[answer.StolenByUserId!] = gains.GetValueOrDefault(answer.StolenByUserId!) + speed + bonus;
            match.Players.Single(p => p.UserId == answer.StolenByUserId).WeaponEffect = speed + bonus;
        }
        return gains;
    }

    private DerbyState Derby(Match match, MatchQuestion? question, MatchPhase phase, string userId, Dictionary<string, ResultsService.UserCard> cards)
    {
        var me = match.Players.SingleOrDefault(p => p.UserId == userId);
        var (target, elapsed) = WeaponTarget(match);
        var canUse = me is { LeftAt: null } && WhyNot(match, me, target, elapsed, null) is null;

        string Name(string id) => match.Players.Single(p => p.UserId == id).User.UserName!;
        DerbyEvent Event(MatchQuestion q, DerbyWeapon weapon, string by, string? to = null)
        {
            var player = match.Players.Single(p => p.UserId == by);
            // Klaargezet voor een vraag die nog niet gestart is: negatief.
            var since = q.StartsAt is { } start
                ? (Now - new[] { player.WeaponUsedAt ?? Now, start }.Max()).TotalSeconds
                : -1;
            int? effect = q.EndedAt is null || weapon is DerbyWeapon.BengaalsVuur or DerbyWeapon.Wissel or DerbyWeapon.Verlenging
                ? null : player.WeaponEffect;
            return new DerbyEvent(weapon, Name(by), cards.GetValueOrDefault(by)?.AvatarId, by == userId,
                to is null ? null : Name(to), to == userId, since, effect);
        }
        List<DerbyEvent> EventsOf(MatchQuestion q)
        {
            var list = new List<DerbyEvent>();
            if (q.SwapUserId is { } swap) list.Add(Event(q, DerbyWeapon.Wissel, swap));
            if (q.FlareUserId is { } flare) list.Add(Event(q, DerbyWeapon.BengaalsVuur, flare));
            if (q.TrumpUserId is { } trump) list.Add(Event(q, DerbyWeapon.Trump, trump));
            foreach (var a in q.Answers.Where(a => a.RedCardFromUserId != null))
                list.Add(Event(q, DerbyWeapon.RodeKaart, a.RedCardFromUserId!, a.UserId));
            foreach (var a in q.Answers.Where(a => a.StolenByUserId != null))
                list.Add(Event(q, DerbyWeapon.PropereHanden, a.StolenByUserId!, a.UserId));
            foreach (var a in q.Answers.Where(a => a.Gamble))
                list.Add(Event(q, DerbyWeapon.Gokchinees, a.UserId));
            foreach (var a in q.Answers.Where(a => a.ExtraSeconds > 0))
                list.Add(Event(q, DerbyWeapon.Verlenging, a.UserId));
            // Iedereen ziet alles meteen, ook wat klaargezet is voor de volgende vraag (Since < 0).
            return list;
        }

        var events = new List<DerbyEvent>();
        if (question is not null && phase is MatchPhase.Countdown or MatchPhase.Question or MatchPhase.Reveal)
        {
            events.AddRange(EventsOf(question));
            if (phase == MatchPhase.Reveal && match.Questions.SingleOrDefault(q => q.Number == question.Number + 1) is { } next)
                events.AddRange(EventsOf(next));
        }

        string? official = null, actual = null;
        if (phase == MatchPhase.Reveal && question is not null && TrumpChoice(question) is not null)
        {
            official = QuestionEngine.AnswerLabel(Judged(question));
            actual = QuestionEngine.AnswerLabel(AsDaily(question));
        }

        var report = phase == MatchPhase.Finished
            ? match.Players.Where(p => p.Weapon != null).OrderBy(p => p.WeaponQuestion ?? int.MaxValue)
                .Select(p => new DerbyReportLine(p.User.UserName!, cards.GetValueOrDefault(p.UserId)?.AvatarId, p.Weapon!.Value,
                    p.WeaponUsedAt is null ? null : p.WeaponQuestion,
                    p.WeaponTargetUserId is null ? null : Name(p.WeaponTargetUserId), p.WeaponEffect, p.UserId == userId))
                .ToList()
            : null;

        return new DerbyState(
            me?.Weapon, me?.WeaponUsedAt is not null, canUse, target?.Number ?? 0,
            target is not null && DerbyRules.Ceasefire(target.Number, match.Questions.Count),
            events, official, actual, report);
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
            // Met een verlenging loopt de vraag door tot ook die speler zijn tijd op is.
            var extra = question.Answers.Select(a => a.ExtraSeconds).DefaultIfEmpty(0).Max();
            var timeUp = Elapsed(question) > TimeLimit(question) + extra + GameRules.LatencyGraceSeconds;
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
        ApplyTrump(match, question);

        // Wie vertrok, telt niet meer mee: geen positie (die gaat naar wie bleef) en geen punten.
        var left = match.Players.Where(p => p.LeftAt != null).Select(p => p.UserId).ToHashSet();
        var position = 1;
        foreach (var answer in question.Answers.Where(a => a.Correct && !left.Contains(a.UserId)).OrderBy(a => a.AnsweredAt))
        {
            answer.Position = position;
            answer.Bonus = MatchRules.BonusFor(position);
            position++;
        }
        // Ook niets om te stelen bij een vertrekker.
        foreach (var answer in question.Answers.Where(a => left.Contains(a.UserId)))
            (answer.SpeedPoints, answer.Bonus) = (0, 0);
        ApplyRedCards(match, question);
        var losers = ApplyGambles(match, question);
        var steals = ApplySteals(match, question);
        foreach (var answer in question.Answers)
            match.Players.Single(p => p.UserId == answer.UserId).Score += answer.SpeedPoints + answer.Bonus;
        foreach (var (thief, amount) in steals)
            match.Players.Single(p => p.UserId == thief).Score += amount;
        foreach (var loser in losers)
        {
            // Een verloren gok kost punten van je totaal, maar je zakt nooit onder 0.
            var penalty = Math.Min(DerbyRules.GamblePenalty, loser.Score);
            loser.Score -= penalty;
            loser.WeaponEffect = -penalty;
        }
        foreach (var player in match.Players.Where(p => left.Contains(p.UserId)))
            player.Score = 0;
    }

    /// <summary>
    /// Eindstand: wie bleef, volgens de punten (gelijk = zelfde plaats). Daaronder wie vertrok, met 0 punten:
    /// wie het langst bleef het hoogst. Blijft er één speler over, dan wint die dus zowiezo.
    /// </summary>
    private void Finish(Match match)
    {
        match.Status = MatchStatus.Finished;
        match.FinishedAt = Now;
        var stayed = match.Players.Where(p => p.LeftAt == null).OrderByDescending(p => p.Score).ToList();
        for (var i = 0; i < stayed.Count; i++)
            stayed[i].Rank = i > 0 && stayed[i - 1].Score == stayed[i].Score ? stayed[i - 1].Rank : i + 1;
        var rank = stayed.Count;
        foreach (var p in match.Players.Where(p => p.LeftAt != null).OrderByDescending(p => p.LeftAt))
        {
            p.Rank = ++rank;
            p.Score = 0; // ook als het vertrek samenviel met het afsluiten van een vraag
        }
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
            limit = LimitFor(question, myAnswer); // met een verlenging: jouw eigen, langere tijd
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
                    // Na een Trump booster toont de reveal het "officiële" antwoord (het echte staat in de derby-info).
                    var judged = Judged(question);
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
            ? question.Answers.Where(a => a.SpeedPoints + a.Bonus > 0 && match.Players.Any(p => p.UserId == a.UserId && p.LeftAt == null))
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
            match.Questions.Count > 0 ? match.Questions.Count : match.QuestionCount,
            question?.Type ?? types[0],
            limit,
            view,
            result,
            (myAnswer?.SpeedPoints ?? 0) + (myAnswer?.Bonus ?? 0),
            match.Derby ? Derby(match, question, phase, userId, cards) : null);
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
        await db.LoadCounterPlayersAsync(match.Questions);
        return match;
    }

    private async Task SaveAndNotifyAsync(Match match, bool lobby = true)
    {
        await db.SaveChangesAsync();
        await notifier.MatchChangedAsync(match.Id);
        if (lobby) await notifier.LobbyChangedAsync();
    }
}
