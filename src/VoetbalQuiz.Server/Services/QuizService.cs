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
    private const int MaxClassicPerDay = 2;

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public DateOnly Today => QuizClock.Today(clock);

    // ---------- Daily challenge ----------

    public async Task<HashSet<QuestionType>> DisabledTypesAsync() =>
        (await db.DisabledQuestionTypes.Select(d => d.Type).ToListAsync()).ToHashSet();

    /// <summary>De vraagtypes van een dag volgens de weekplanning (één type of een mix), zonder uitgeschakelde types.</summary>
    public async Task<List<QuestionType>> TypesForAsync(DateOnly date) =>
        ResolveTypes(await db.DailySchedule.SingleOrDefaultAsync(d => d.Day == date.DayOfWeek), date.DayOfWeek, await DisabledTypesAsync());

    public static List<QuestionType> ResolveTypes(DailyScheduleDay? row, DayOfWeek day, ICollection<QuestionType> disabled) =>
        QuestionTypes.ResolveDay(row is { MixTypes.Count: > 1 } ? row.MixTypes : [row?.Type ?? QuestionTypes.DefaultFor(day)], disabled);

    private static List<QuestionType> Enabled(IEnumerable<QuestionType> types, ICollection<QuestionType> disabled) =>
        types.Where(t => !disabled.Contains(t)).Distinct().Order().ToList();

    public async Task<DailyChallenge> GetOrCreateChallengeAsync(DateOnly date)
    {
        var challenge = await LoadChallengeAsync(c => c.Date == date && c.SpecialEventId == null);
        if (challenge is not null) return challenge;

        var types = await TypesForAsync(date);
        // Te weinig vragen van een type in de bank? Dan wordt het gewoon "Wie ben ik?".
        var questions = await BuildMixAsync(types, null)
                        ?? await BuildAsync(QuestionType.WieBenIk, GameRules.QuestionsPerDay, null)
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

    private async Task<DailyChallenge?> LoadChallengeAsync(System.Linq.Expressions.Expression<Func<DailyChallenge, bool>> filter)
    {
        var challenge = await db.DailyChallenges
            .Include(c => c.Questions).ThenInclude(q => q.Player)
            .Include(c => c.Questions).ThenInclude(q => q.QuizItem)
            .AsSplitQuery()
            .SingleOrDefaultAsync(filter);

        // Counter: de spelers staan als lijst van ids bij de vraag.
        var ids = challenge?.Questions.SelectMany(q => q.CounterPlayerIds).Distinct().ToList() ?? [];
        if (ids.Count > 0)
        {
            var players = await db.Players.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
            foreach (var q in challenge!.Questions)
                q.CounterPlayers = q.CounterPlayerIds.Where(players.ContainsKey).Select(id => players[id]).ToList();
        }
        return challenge;
    }

    /// <summary>
    /// Stelt <paramref name="count"/> vragen van één type samen, of null als er te weinig zijn.
    /// <paramref name="usedPlayers"/>: spelers die al in deze reeks zitten (bv. bij een kluts) komen niet nog eens voor.
    /// </summary>
    private async Task<List<DailyQuestion>?> BuildAsync(
        QuestionType type, int count, string? tag, List<Player>? playerPool = null, HashSet<int>? usedPlayers = null,
        HashSet<int>? excludeItems = null)
    {
        if (QuestionTypes.Get(type).FromPlayers)
        {
            usedPlayers ??= [];
            var perQuestion = type == QuestionType.Counter ? QuestionRules.CounterPlayers : 1;
            var players = await PickPlayersAsync(count * perQuestion, playerPool, usedPlayers);
            if (players.Count < count * perQuestion) return null;
            usedPlayers.UnionWith(players.Select(p => p.Id));

            if (type == QuestionType.Counter)
                return players.Chunk(perQuestion).Select(chunk => chunk.OrderBy(p => p.Difficulty).ToList())
                    .Select(chunk => new DailyQuestion { Type = type, CounterPlayerIds = [.. chunk.Select(p => p.Id)], CounterPlayers = chunk })
                    .ToList();

            return players.OrderBy(p => p.Difficulty).ThenBy(_ => Random.Shared.Next())
                .Select(p => new DailyQuestion { Type = type, PlayerId = p.Id }).ToList();
        }

        var items = await PickItemsAsync(type, count, tag, excludeItems);
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
    private async Task<List<Player>> PickPlayersAsync(int count, List<Player>? pool, HashSet<int> exclude)
    {
        if (pool is not null)
        {
            // Een special: liefst spelers die nog niet in de reeks zitten; is de pool te klein, dan mag iemand terugkomen.
            var shuffled = pool.OrderBy(p => p.Status == PlayerStatus.Approved ? 0 : 1).ThenBy(_ => Random.Shared.Next()).ToList();
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

        var picked = new List<Player>();
        foreach (var p in candidates)
        {
            if (p.Era == "classic" && picked.Count(x => x.Era == "classic") >= MaxClassicPerDay) continue;
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
    private async Task<List<QuizItem>> PickItemsAsync(QuestionType type, int count, string? tag, HashSet<int>? exclude = null)
    {
        var lastUsed = exclude is not null ? new Dictionary<int, DateOnly>() : await db.DailyQuestions
            .Where(q => q.QuizItemId != null)
            .GroupBy(q => q.QuizItemId!.Value)
            .Select(g => new { Id = g.Key, LastUsed = g.Max(q => q.DailyChallenge.Date) })
            .ToDictionaryAsync(x => x.Id, x => x.LastUsed);

        return (await db.QuizItems.Where(i => i.Type == type && i.Status != PlayerStatus.Rejected).ToListAsync())
            .Where(i => exclude?.Contains(i.Id) != true)
            .OrderBy(i => PlayerTags.Any(i.Tags, tag) ? 0 : 1)
            .ThenBy(i => i.Status == PlayerStatus.Approved ? 0 : 1)
            .ThenBy(i => lastUsed.TryGetValue(i.Id, out var d) ? d : DateOnly.MinValue)
            .ThenBy(_ => Random.Shared.Next())
            // Bij emoji, carrièrepad, wissel, rugnummer en mannetje is het antwoord het onderwerp: nooit twee keer dezelfde speler of club
            // op één dag (bv. een feit- én een rebus-raadsel). Bij de andere types mag een antwoord terugkomen.
            .DistinctBy(i => type is QuestionType.Emoji or QuestionType.Carriere or QuestionType.Wissel or QuestionType.Rugnummer or QuestionType.Mannetje
                ? AnswerMatcher.Normalize(QuestionEngine.Describe(i.Type, i.Data).Answer)
                : i.Id.ToString())
            .Take(count)
            .ToList();
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
    public async Task<List<QuestionType>> SpecialTypesAsync(SpecialEvent special)
    {
        List<QuestionType> chosen = special.Types.Count > 0 ? special.Types : [QuestionType.WieBenIk];
        var enabled = Enabled(chosen, await DisabledTypesAsync());
        return enabled.Count > 0 ? enabled : chosen.Distinct().ToList();
    }

    /// <summary>Stelt een reeks samen volgens <see cref="QuestionTypes.Mix"/>; null als een type te weinig vragen heeft.</summary>
    private async Task<List<DailyQuestion>?> BuildMixAsync(
        List<QuestionType> types, string? tag, List<Player>? playerPool = null, HashSet<int>? excludeItems = null, int? questions = null)
    {
        var all = new List<DailyQuestion>();
        var usedPlayers = new HashSet<int>();
        foreach (var (type, count) in questions is { } n ? QuestionTypes.Mix(types, n) : QuestionTypes.Mix(types))
        {
            var part = await BuildAsync(type, count, tag, QuestionTypes.Get(type).FromPlayers ? playerPool : null, usedPlayers, excludeItems);
            if (part is null) return null;
            all.AddRange(part);
        }
        // Afwisselen tussen de types in plaats van ze te groeperen.
        return all.Select((q, i) => (q, i)).GroupBy(x => x.q.Type)
            .SelectMany(g => g.Select((x, n) => (x.q, n))).OrderBy(x => x.n).Select(x => x.q).ToList();
    }

    // ---------- Online ----------

    /// <summary>De types die nu aan staan: enkel die kan je kiezen voor een online tafel.</summary>
    public async Task<List<QuestionType>> ActiveTypesAsync()
    {
        var disabled = await DisabledTypesAsync();
        return QuestionTypes.All.Select(t => t.Type).Where(t => !disabled.Contains(t)).ToList();
    }

    /// <summary>
    /// De vragen van een online tafel (zoveel als de organisator koos, om beurten per type),
    /// of null als een type te weinig vragen heeft.
    /// Willekeurig gekozen en nooit iets uit de daily van vandaag (anders verklap je die).
    /// </summary>
    public async Task<List<DailyQuestion>?> BuildOnlineAsync(List<QuestionType> types, int questions)
    {
        var (pool, dailyItems) = await OnlinePoolAsync();
        return await BuildMixAsync(types, null, pool, dailyItems, questions);
    }

    /// <summary>
    /// Derby-wissel: één nieuwe vraag van dit type, niet uit de daily van vandaag en (zo mogelijk) nog niet aan tafel gebruikt.
    /// Null als er geen vraag meer over is.
    /// </summary>
    public async Task<DailyQuestion?> BuildReplacementAsync(QuestionType type, IEnumerable<int> usedPlayers, IEnumerable<int> usedItems)
    {
        var (pool, dailyItems) = await OnlinePoolAsync();
        // Geen terugval op spelers die al aan tafel waren: liever geen wissel dan een vraag die iedereen al zag.
        var used = usedPlayers.ToHashSet();
        var part = await BuildAsync(type, 1, null, QuestionTypes.Get(type).FromPlayers ? pool.Where(p => !used.Contains(p.Id)).ToList() : null,
            used, [.. dailyItems, .. usedItems]);
        return part?.FirstOrDefault();
    }

    /// <summary>Spelers en vragen voor online: alles behalve wat in de daily van vandaag zit.</summary>
    private async Task<(List<Player> Pool, HashSet<int> DailyItems)> OnlinePoolAsync()
    {
        var daily = await db.DailyQuestions.Where(q => q.DailyChallenge.Date == Today).ToListAsync();
        var dailyPlayers = daily.Where(q => q.PlayerId != null).Select(q => q.PlayerId!.Value)
            .Concat(daily.SelectMany(q => q.CounterPlayerIds)).ToHashSet();
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
            if (a is not null) CloseIfExpired(q, a);
            var status = StatusOf(a);
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
    private static double DifficultyOf(DailyChallenge challenge) => Difficulty.Average(challenge.Questions.Select(q =>
        q.Player?.Difficulty ?? q.QuizItem?.Difficulty
        ?? (q.CounterPlayers.Count > 0 ? q.CounterPlayers.Average(p => p.Difficulty) : Difficulty.Default)));

    /// <summary>De specials (zie <see cref="ResultsService.GetSpecialsAsync"/>), met de moeilijkheid van de lopende.</summary>
    public async Task<List<SpecialSummary>> GetSpecialsAsync(string userId)
    {
        var specials = await results.GetSpecialsAsync(userId);
        for (var i = 0; i < specials.Count; i++)
        {
            if (!specials[i].IsActive) continue;
            try { specials[i] = specials[i] with { Difficulty = DifficultyOf(await GetOrCreateSpecialChallengeAsync(specials[i].Id)) }; }
            catch (QuizException) { } // nog te weinig vragen: dan geen sterren
        }
        return specials;
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

            // Je moet de vragen in volgorde spelen.
            var previous = question.DailyChallenge.Questions.Where(q => q.Number < number).Select(q => q.Id).ToList();
            var answeredBefore = await db.Attempts.CountAsync(a => a.UserId == userId && previous.Contains(a.DailyQuestionId) && a.AnsweredAt != null);
            if (answeredBefore < previous.Count)
                throw new QuizException("Speel eerst de vorige vragen.");

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

        CloseIfExpired(question, attempt);
        var trimmed = guess?.Trim();
        string? lastWrong = null;

        if (attempt.AnsweredAt is null && !string.IsNullOrEmpty(trimmed))
        {
            attempt.Guesses++;
            attempt.LastGuess = trimmed;
            var outcome = QuestionEngine.Guess(question, attempt, trimmed, Seconds(attempt.StartedAt, Now));
            if (outcome.Wrong)
            {
                attempt.WrongGuesses++;
                lastWrong = trimmed;
            }
            attempt.Points = outcome.Points;
            if (outcome.Done)
            {
                attempt.AnsweredAt = Now;
                attempt.Correct = outcome.Correct;
            }
            else if (attempt.Guesses >= GameRules.MaxGuessesPerQuestion)
            {
                attempt.AnsweredAt = Now;
                attempt.Correct = false;
                attempt.Points = QuestionEngine.PointsWhenStopped(question, attempt);
            }
            if (!await SaveAsync()) lastWrong = null;
        }
        return await StateAsync(question, attempt, lastWrong);
    }

    /// <summary>"Ik pas" (of "Klaar" bij een opstelling): de vraag stopt, enkel een opstelling houdt deelpunten.</summary>
    public async Task<QuestionState> GiveUpAsync(string userId, int number, int? specialId = null)
    {
        var (question, attempt) = await FindAsync(userId, number, specialId);
        if (attempt is null) throw new QuizException("Deze vraag is nog niet gestart.");

        if (attempt.AnsweredAt is null)
        {
            attempt.Guesses++;
            attempt.AnsweredAt = Now;
            attempt.Correct = false;
            attempt.Points = QuestionEngine.PointsWhenStopped(question, attempt);
            await SaveAsync();
        }
        return await StateAsync(question, attempt);
    }

    /// <summary>
    /// Bewaart de pogingen. Kwam er tegelijk een ander verzoek voor dezelfde vraag binnen (dubbele tik, of een script
    /// dat gokken afvuurt), dan telt enkel dat andere: deze wijziging vervalt en we lezen de poging opnieuw in.
    /// </summary>
    private async Task<bool> SaveAsync()
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

    private async Task<QuestionState> StateAsync(DailyQuestion question, Attempt attempt, string? lastWrong = null)
    {
        CloseIfExpired(question, attempt);
        await SaveAsync();

        var status = StatusOf(attempt);
        var limit = QuestionEngine.TimeLimit(question);

        if (status == QuestionStatus.Answered)
        {
            var result = QuestionEngine.Result(question, attempt);
            var (_, finalView) = QuestionEngine.Visible(question, attempt, limit);
            return new QuestionState(question.Number, status, result.AllHints, Seconds(attempt.StartedAt, attempt.AnsweredAt!.Value),
                attempt.WrongGuesses, lastWrong, result, question.Type, limit, finalView);
        }

        var elapsed = Seconds(attempt.StartedAt, Now);
        var (hints, view) = QuestionEngine.Visible(question, attempt, elapsed);
        return new QuestionState(question.Number, status, hints, elapsed, attempt.WrongGuesses, lastWrong, null,
            question.Type, limit, view);
    }

    /// <summary>Sluit een vraag af als de tijd om is (een opstelling houdt de gevonden namen).</summary>
    private void CloseIfExpired(DailyQuestion question, Attempt attempt)
    {
        if (attempt.AnsweredAt is not null) return;
        var limit = QuestionEngine.TimeLimit(question);
        if (Seconds(attempt.StartedAt, Now) <= limit + GameRules.LatencyGraceSeconds) return;

        attempt.AnsweredAt = attempt.StartedAt.AddSeconds(limit);
        attempt.Correct = false;
        attempt.Points = QuestionEngine.PointsWhenStopped(question, attempt);
    }

    private static QuestionStatus StatusOf(Attempt? a) => a switch
    {
        null => QuestionStatus.NotStarted,
        { AnsweredAt: not null } => QuestionStatus.Answered,
        _ => QuestionStatus.InProgress,
    };

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

    private static double Seconds(DateTime from, DateTime to) => (to - from).TotalSeconds;
}
