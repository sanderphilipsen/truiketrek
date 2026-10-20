using Microsoft.EntityFrameworkCore;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Services;

public class QuizException(string message) : Exception(message);

/// <summary>
/// Alle spellogica van de daily en de specials. De klok draait op de server: de client krijgt enkel
/// te zien wat op dat moment vrijgegeven is, en de punten worden hier berekend.
/// Wat per vraagtype verschilt, staat in <see cref="QuestionEngine"/>.
/// </summary>
public class QuizService(QuizDbContext db, TimeProvider clock, ResultsService results)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public DateOnly Today => QuizClock.Today(clock);

    // ---------- Daily challenge ----------

    public async Task<HashSet<QuestionType>> DisabledTypesAsync() =>
        (await db.DisabledQuestionTypes.Select(d => d.Type).ToListAsync()).ToHashSet();

    /// <summary>De vraagtypes van een dag volgens de weekplanning (één type of een mix), zonder uitgeschakelde types.</summary>
    public async Task<List<QuestionType>> TypesForAsync(DateOnly date)
    {
        var row = await db.DailySchedule.SingleOrDefaultAsync(d => d.Day == date.DayOfWeek);
        return QuestionTypes.ResolveSchedule(row?.Type, row?.MixTypes, date.DayOfWeek, await DisabledTypesAsync());
    }

    public async Task<DailyChallenge> GetOrCreateChallengeAsync(DateOnly date)
    {
        var challenge = await LoadChallengeAsync(c => c.Date == date && c.SpecialEventId == null);
        if (challenge is not null) return challenge;

        var types = await TypesForAsync(date);
        // Te weinig vragen van een type in de bank? Dan wordt het gewoon "Wie ben ik?".
        var questions = await BuildMixAsync(types, null)
                        ?? await BuildAsync(QuestionType.WieBenIk, GameRules.QuestionsPerDay, null,
                            targets: Difficulty.Plan(GameRules.QuestionsPerDay))
                        ?? throw new QuizException("Er zitten nog niet genoeg spelers in de database.");

        return await SaveChallengeAsync(new DailyChallenge { Date = date }, questions,
            () => LoadChallengeAsync(c => c.Date == date && c.SpecialEventId == null));
    }

    private async Task<DailyChallenge> SaveChallengeAsync(
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
        }
        return (await reload())!;
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

    /// <summary>
    /// Stelt <paramref name="count"/> vragen van één type samen, of null als er te weinig zijn.
    /// <paramref name="usedPlayers"/>: spelers die al in deze reeks zitten (bv. bij een kluts) komen niet nog eens voor.
    /// <paramref name="targets"/>: de gewenste moeilijkheid per vraag (zie <see cref="Difficulty.Plan"/>); null = geen plan (online).
    /// </summary>
    private async Task<List<DailyQuestion>?> BuildAsync(
        QuestionType type, int count, string? tag, List<Player>? playerPool = null, HashSet<int>? usedPlayers = null,
        HashSet<int>? excludeItems = null, List<int>? targets = null)
    {
        if (QuestionTypes.Get(type).FromPlayers)
        {
            usedPlayers ??= [];
            var perQuestion = type == QuestionType.Counter ? QuestionRules.CounterPlayers : 1;
            // Counter: elke speler van de vraag krijgt de moeilijkheid van die vraag als doel.
            var players = await PickPlayersAsync(count * perQuestion, playerPool, usedPlayers,
                targets?.SelectMany(t => Enumerable.Repeat(t, perQuestion)).ToList());
            if (players.Count < count * perQuestion) return null;
            usedPlayers.UnionWith(players.Select(p => p.Id));

            if (type == QuestionType.Counter)
                return players.Chunk(perQuestion).Select(chunk => chunk.OrderBy(p => p.Difficulty).ToList())
                    .Select(chunk => new DailyQuestion { Type = type, CounterPlayerIds = [.. chunk.Select(p => p.Id)], CounterPlayers = chunk })
                    .ToList();

            return players.OrderBy(p => p.Difficulty).ThenBy(_ => Random.Shared.Next())
                .Select(p => new DailyQuestion { Type = type, PlayerId = p.Id }).ToList();
        }

        var items = await PickItemsAsync(type, count, tag, excludeItems, targets);
        return items.Count < count
            ? null
            : items.OrderBy(i => i.Difficulty).ThenBy(_ => Random.Shared.Next())
                .Select(i => new DailyQuestion
                {
                    Type = type,
                    QuizItemId = i.Id,
                    Blanks = type == QuestionType.Opstelling ? QuestionEngine.PickBlanks(i) : [],
                })
                .ToList();
    }

    /// <summary>
    /// Kiest de spelers die het langst geleden gevraagd werden (nooit gebruikt eerst), maximaal twee klassiekers.
    /// Goedgekeurde spelers eerst; te keuren spelers enkel als er niet genoeg goedgekeurde zijn.
    /// </summary>
    private async Task<List<Player>> PickPlayersAsync(int count, List<Player>? pool, HashSet<int> exclude, List<int>? targets = null)
    {
        if (pool is not null)
        {
            // Een special: liefst spelers die nog niet in de reeks zitten; is de pool te klein, dan mag iemand terugkomen.
            var shuffled = pool.OrderBy(p => p.Status == PlayerStatus.Approved ? 0 : 1).ThenBy(_ => Random.Shared.Next()).ToList();
            if (targets is not null)
                return PickByPlan(shuffled, targets, p => p.Difficulty,
                    p => (exclude.Contains(p.Id) ? 2 : 0) + (p.Status == PlayerStatus.Approved ? 0 : 1), (_, _) => true);
            var fresh = shuffled.Where(p => !exclude.Contains(p.Id)).ToList();
            return (fresh.Count >= count ? fresh : fresh.Concat(shuffled.Where(p => exclude.Contains(p.Id)))).Take(count).ToList();
        }

        var lastUsed = (await db.DailyQuestions
                .Where(q => q.PlayerId != null)
                .Select(q => new { PlayerId = q.PlayerId!.Value, q.DailyChallenge.Date })
                .ToListAsync())
            .Select(x => (x.PlayerId, x.Date))
            // Ook de spelers uit een counter tellen mee voor de rotatie.
            .Concat((await db.DailyQuestions.Where(q => q.Type == QuestionType.Counter)
                    .Select(q => new { q.CounterPlayerIds, q.DailyChallenge.Date }).ToListAsync())
                .SelectMany(x => x.CounterPlayerIds.Select(id => (PlayerId: id, x.Date))))
            .GroupBy(x => x.PlayerId)
            .ToDictionary(g => g.Key, g => g.Max(x => x.Date));

        var candidates = (await db.Players.Where(p => p.Status != PlayerStatus.Rejected).ToListAsync())
            .Where(p => !exclude.Contains(p.Id))
            .OrderBy(p => p.Status == PlayerStatus.Approved ? 0 : 1)
            .ThenBy(p => lastUsed.TryGetValue(p.Id, out var d) ? d : DateOnly.MinValue)
            .ThenBy(_ => Random.Shared.Next());

        if (targets is not null)
            return PickByPlan(candidates.ToList(), targets, p => p.Difficulty, p => p.Status == PlayerStatus.Approved ? 0 : 1,
                (picked, p) => p.Era != QuestionRules.ClassicEra || picked.Count(x => x.Era == QuestionRules.ClassicEra) < QuestionRules.MaxClassicPerDay);

        var picked = new List<Player>();
        foreach (var p in candidates)
        {
            if (p.Era == QuestionRules.ClassicEra && picked.Count(x => x.Era == QuestionRules.ClassicEra) >= QuestionRules.MaxClassicPerDay) continue;
            picked.Add(p);
            if (picked.Count == count) break;
        }
        return picked;
    }

    /// <summary>
    /// Vragen uit de bank: bij een special eerst die met het thema (ook als ze nog te keuren zijn), dan goedgekeurd eerst,
    /// dan langst niet gebruikt eerst. Zijn er te weinig themavragen, dan vullen gewone vragen aan.
    /// <paramref name="exclude"/> (online): deze vragen niet, en verder willekeurig, zodat de rotatie van de daily ongemoeid blijft.
    /// </summary>
    private async Task<List<QuizItem>> PickItemsAsync(QuestionType type, int count, string? tag, HashSet<int>? exclude = null,
        List<int>? targets = null)
    {
        var lastUsed = exclude is not null ? new Dictionary<int, DateOnly>() : await db.DailyQuestions
            .Where(q => q.QuizItemId != null)
            .GroupBy(q => q.QuizItemId!.Value)
            .Select(g => new { Id = g.Key, LastUsed = g.Max(q => q.DailyChallenge.Date) })
            .ToDictionaryAsync(x => x.Id, x => x.LastUsed);

        var ordered = (await db.QuizItems.Where(i => i.Type == type && i.Status != PlayerStatus.Rejected).ToListAsync())
            .Where(i => exclude?.Contains(i.Id) != true)
            .OrderBy(i => PlayerTags.Any(i.Tags, tag) ? 0 : 1)
            .ThenBy(i => i.Status == PlayerStatus.Approved ? 0 : 1)
            .ThenBy(i => lastUsed.TryGetValue(i.Id, out var d) ? d : DateOnly.MinValue)
            .ThenBy(_ => Random.Shared.Next())
            .ToList();

        // Is het antwoord het onderwerp (zie QuestionRules.AnswerIsSubject), dan nooit twee keer dezelfde speler of club op één dag.
        string Subject(QuizItem i) =>
            QuestionRules.AnswerIsSubject(type)
                ? AnswerMatcher.Normalize(QuestionEngine.Describe(i.Type, i.Data).Answer)
                : i.Id.ToString();

        if (targets is not null)
            return PickByPlan(ordered, targets, i => i.Difficulty,
                i => (PlayerTags.Any(i.Tags, tag) ? 0 : 2) + (i.Status == PlayerStatus.Approved ? 0 : 1),
                (picked, i) => picked.All(p => Subject(p) != Subject(i)));

        return ordered.DistinctBy(Subject).Take(count).ToList();
    }

    /// <summary>
    /// Kiest per gewenste moeilijkheid uit <paramref name="targets"/> één kandidaat: eerst de laagste <paramref name="tier"/>
    /// (thema, goedgekeurd), dan de moeilijkheid die het dichtst bij het doel ligt, dan de volgorde van <paramref name="candidates"/> (rotatie).
    /// Meer vragen van 1 of 5 sterren dan gepland komen er enkel in als er niets anders over is.
    /// Geeft minder terug dan gevraagd als de kandidaten op zijn.
    /// </summary>
    internal static List<T> PickByPlan<T>(IReadOnlyList<T> candidates, IReadOnlyList<int> targets,
        Func<T, int> difficulty, Func<T, int> tier, Func<List<T>, T, bool> allowed) where T : class
    {
        var picked = new List<T>();
        var left = candidates.ToList();
        bool TooMany(T c) => difficulty(c) is Difficulty.Min or Difficulty.Max
                             && picked.Count(p => difficulty(p) == difficulty(c)) >= targets.Count(t => t == difficulty(c));

        // De opwarmer en de uitsmijter eerst, zodat het midden hun vragen niet opsnoept.
        foreach (var target in targets.OrderBy(t => t is Difficulty.Min or Difficulty.Max ? 0 : 1))
        {
            var choice = left.Select((c, index) => (c, index))
                .Where(x => allowed(picked, x.c))
                .OrderBy(x => TooMany(x.c) ? 1 : 0)
                .ThenBy(x => tier(x.c))
                .ThenBy(x => Math.Abs(difficulty(x.c) - target))
                .ThenBy(x => x.index)
                .Select(x => x.c)
                .FirstOrDefault();
            if (choice is null) break;
            picked.Add(choice);
            left.Remove(choice);
        }
        return picked;
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

        var questions = await BuildMixAsync(await SpecialTypesAsync(special), special.Tag, await SpecialPoolAsync(special))
                        ?? throw new QuizException("Deze special heeft nog niet genoeg vragen.");
        return await SaveChallengeAsync(new DailyChallenge { Date = special.StartDate, SpecialEventId = special.Id }, questions,
            () => LoadChallengeAsync(c => c.SpecialEventId == specialId));
    }

    /// <summary>De types van een special, zonder de uitgeschakelde (staan ze allemaal uit, dan toch de gekozen types).</summary>
    public async Task<List<QuestionType>> SpecialTypesAsync(SpecialEvent special) =>
        QuestionTypes.ResolveSpecial(special.Types, await DisabledTypesAsync());

    /// <summary>Stelt een reeks samen volgens <see cref="QuestionTypes.Mix"/>; null als een type te weinig vragen heeft.</summary>
    private async Task<List<DailyQuestion>?> BuildMixAsync(
        List<QuestionType> types, string? tag, List<Player>? playerPool = null, HashSet<int>? excludeItems = null, int? questions = null)
    {
        var all = new List<DailyQuestion>();
        var usedPlayers = new HashSet<int>();
        var mix = questions is { } n ? QuestionTypes.Mix(types, n) : QuestionTypes.Mix(types);
        // Daily en specials volgen het moeilijkheidsplan, verdeeld over de types; online kiest willekeurig.
        var plan = questions is null
            ? new Queue<int>(Difficulty.Plan(mix.Values.Sum()).OrderBy(_ => Random.Shared.Next()))
            : null;
        foreach (var (type, count) in mix)
        {
            var targets = plan is null ? null : Enumerable.Range(0, count).Select(_ => plan.Dequeue()).ToList();
            var part = await BuildAsync(type, count, tag, QuestionTypes.Get(type).FromPlayers ? playerPool : null, usedPlayers, excludeItems, targets);
            if (part is null) return null;
            all.AddRange(part);
        }
        // Afwisselen tussen de types in plaats van ze te groeperen.
        return all.Select((q, i) => (q, i)).GroupBy(x => x.q.Type)
            .SelectMany(g => g.Select((x, n) => (x.q, n))).OrderBy(x => x.n).Select(x => x.q).ToList();
    }

    // ---------- Online ----------

    /// <summary>De types die nu aan staan: enkel die kan je kiezen voor een online partijtje.</summary>
    public async Task<List<QuestionType>> ActiveTypesAsync()
    {
        var disabled = await DisabledTypesAsync();
        return QuestionTypes.All.Select(t => t.Type).Where(t => !disabled.Contains(t)).ToList();
    }

    /// <summary>
    /// De vragen van een online partijtje (zoveel als de organisator koos, om beurten per type),
    /// of null als een type te weinig vragen heeft.
    /// Willekeurig gekozen en nooit iets uit de daily van vandaag (anders verklap je die).
    /// </summary>
    public async Task<List<DailyQuestion>?> BuildOnlineAsync(List<QuestionType> types, int questions)
    {
        var (pool, dailyItems) = await OnlinePoolAsync();
        return await BuildMixAsync(types, null, pool, dailyItems, questions);
    }

    /// <summary>
    /// Derby-wissel: één nieuwe vraag van dit type, niet uit de daily van vandaag en (zo mogelijk) nog niet in het partijtje gebruikt.
    /// Null als er geen vraag meer over is.
    /// </summary>
    public async Task<DailyQuestion?> BuildReplacementAsync(QuestionType type, IEnumerable<int> usedPlayers, IEnumerable<int> usedItems)
    {
        var (pool, dailyItems) = await OnlinePoolAsync();
        // Geen terugval op spelers die al in het partijtje zaten: liever geen wissel dan een vraag die iedereen al zag.
        var used = usedPlayers.ToHashSet();
        var part = await BuildAsync(type, 1, null, QuestionTypes.Get(type).FromPlayers ? pool.Where(p => !used.Contains(p.Id)).ToList() : null,
            used, [.. dailyItems, .. usedItems]);
        return part?.FirstOrDefault();
    }

    /// <summary>Spelers en vragen voor online: alles behalve wat in de daily van vandaag zit.</summary>
    private async Task<(List<Player> Pool, HashSet<int> DailyItems)> OnlinePoolAsync()
    {
        var daily = await db.DailyQuestions.Where(q => q.DailyChallenge.Date == Today).ToListAsync();
        var dailyPlayers = daily.SelectMany(QuestionContent.PlayersOf).ToHashSet();
        var dailyItems = daily.Where(q => q.QuizItemId != null).Select(q => q.QuizItemId!.Value).ToHashSet();
        var pool = await db.Players.Where(p => p.Status != PlayerStatus.Rejected && !dailyPlayers.Contains(p.Id)).ToListAsync();
        return (pool, dailyItems);
    }

    /// <summary>
    /// Spelers voor de spelersvragen van een special (wie ben ik, kluts, counter): de handmatig gekozen spelers,
    /// anders iedereen met minstens één van de thema's (bv. "anderlecht,club-brugge").
    /// </summary>
    public async Task<List<Player>> SpecialPoolAsync(SpecialEvent special)
    {
        var query = db.Players.Where(p => p.Status != PlayerStatus.Rejected);
        if (special.PlayerIds.Count > 0) return await query.Where(p => special.PlayerIds.Contains(p.Id)).ToListAsync();
        if (special.Tag is null) return [];
        return (await query.ToListAsync()).Where(p => PlayerTags.Any(p.Tags, special.Tag)).ToList();
    }

    /// <summary>Waarom een special (nog) niet kan: te weinig spelers of vragen voor de gekozen mix.</summary>
    public async Task<List<string>> SpecialShortagesAsync(SpecialEvent special)
    {
        var problems = new List<string>();
        foreach (var (type, count) in QuestionTypes.Mix(await SpecialTypesAsync(special)))
        {
            var fromPlayers = QuestionTypes.Get(type).FromPlayers;
            var needed = type == QuestionType.Counter ? count * QuestionRules.CounterPlayers : count;
            var available = fromPlayers
                ? (await SpecialPoolAsync(special)).Count
                : await db.QuizItems.CountAsync(i => i.Type == type && i.Status != PlayerStatus.Rejected);
            if (available < needed)
                problems.Add($"{QuestionTypes.Get(type).Name}: {available} {(fromPlayers ? "spelers" : "vragen")} beschikbaar, {needed} nodig.");
        }
        return problems;
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
                status == QuestionStatus.Answered ? QuestionEngine.AnswerLabel(q) : null);
        }).ToList();

        await SaveAsync();
        var lastWinner = specialId is null ? await results.GetLastWinnerAsync(userId) : null;
        var types = challenge.Questions.Select(q => q.Type).Distinct().Order().ToList();
        return new DailyOverview(challenge.Date, attempts.Values.Sum(a => a.Points), questions, lastWinner,
            types.Count == 1 ? types[0] : QuestionType.WieBenIk,
            challenge.Questions.Sum(q => QuestionTypes.Get(q.Type).MaxPoints), types, DifficultyOf(challenge));
    }

    /// <summary>Gemiddelde moeilijkheid van een reeks (0–5 sterren); een counter telt als het gemiddelde van zijn spelers.</summary>
    public static double DifficultyOf(DailyChallenge challenge) => Difficulty.Average(challenge.Questions.Select(q =>
        q.Player?.Difficulty ?? q.QuizItem?.Difficulty
        ?? (q.CounterPlayers.Count > 0 ? q.CounterPlayers.Average(p => p.Difficulty) : Difficulty.Default)));

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
