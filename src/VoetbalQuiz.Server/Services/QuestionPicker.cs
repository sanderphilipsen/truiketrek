using Microsoft.EntityFrameworkCore;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Services;

/// <summary>
/// Kiest de vragen: voor de daily en de specials (volgens het moeilijkheidsplan en de rotatie), voor een online
/// partijtje en voor een derby-wissel (willekeurig, nooit iets uit de daily van vandaag). Bewaart zelf niets:
/// de reeks aanmaken doet <see cref="QuizService"/>, een partijtje <see cref="MatchService"/>.
/// </summary>
public class QuestionPicker(QuizDbContext db, TimeProvider clock, ILogger<QuestionPicker>? logger = null)
{
    private DateOnly Today => QuizClock.Today(clock);

    // ---------- Types ----------

    public async Task<HashSet<QuestionType>> DisabledTypesAsync() =>
        (await db.DisabledQuestionTypes.Select(d => d.Type).ToListAsync()).ToHashSet();

    /// <summary>De vraagtypes van een dag volgens de weekplanning (één type of een mix), zonder uitgeschakelde types.</summary>
    public async Task<List<QuestionType>> TypesForAsync(DateOnly date)
    {
        var row = await db.DailySchedule.SingleOrDefaultAsync(d => d.Day == date.DayOfWeek);
        return QuestionTypes.ResolveSchedule(row?.Type, row?.MixTypes, date.DayOfWeek, await DisabledTypesAsync());
    }

    /// <summary>De types van een special, zonder de uitgeschakelde (staan ze allemaal uit, dan toch de gekozen types).</summary>
    public async Task<List<QuestionType>> SpecialTypesAsync(SpecialEvent special) =>
        QuestionTypes.ResolveSpecial(special.Types, await DisabledTypesAsync());

    /// <summary>
    /// De types die nu aan staan en die je kan kiezen voor een online partijtje: geen solo-types
    /// (<see cref="QuestionTypeInfo.Alone"/>: De Zondag, Detectivedag, de kaart), want die vullen een hele dag.
    /// </summary>
    public async Task<List<QuestionType>> ActiveTypesAsync()
    {
        var disabled = await DisabledTypesAsync();
        return QuestionTypes.All.Where(t => !t.Alone).Select(t => t.Type).Where(t => !disabled.Contains(t)).ToList();
    }

    // ---------- Daily en specials ----------

    /// <summary>
    /// De vragen van de daily van <paramref name="date"/> volgens de weekplanning. Te weinig vragen van een type in de
    /// bank? Dan wordt het gewoon "Wie ben ik?". Null als er zelfs daarvoor te weinig spelers zijn.
    /// </summary>
    public async Task<List<DailyQuestion>?> BuildDailyAsync(DateOnly date)
    {
        var types = await TypesForAsync(date);
        // Een kaartdag: het land dat de admin in de weekplanning koos (null = willekeurig).
        var country = (await db.DailySchedule.SingleOrDefaultAsync(d => d.Day == date.DayOfWeek))?.KaartCountry;
        return await BuildMixAsync(types, null, kaartCountry: country)
               ?? await BuildAsync(QuestionType.WieBenIk, GameRules.QuestionsPerDay, null,
                   targets: Difficulty.Plan(GameRules.QuestionsPerDay));
    }

    /// <summary>De vragen van een special (thema en spelerspool van de special); null als een type te weinig vragen heeft.</summary>
    public async Task<List<DailyQuestion>?> BuildSpecialAsync(SpecialEvent special) =>
        await BuildMixAsync(await SpecialTypesAsync(special), special.Tag, await SpecialPoolAsync(special), kaartCountry: special.KaartCountry);

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
            var needed = QuestionTypes.ItemsFor(type, count);
            var approvedOnly = ApprovedOnly(type);
            var available = fromPlayers
                ? (await SpecialPoolAsync(special)).Count
                : await db.QuizItems.CountAsync(i => i.Type == type && i.Status != PlayerStatus.Rejected
                    && (!approvedOnly || i.Status == PlayerStatus.Approved));
            if (available < needed)
                problems.Add($"{QuestionTypes.Get(type).Name}: {available} {(fromPlayers ? "spelers" : "vragen")} beschikbaar, {needed} nodig.");
        }
        return problems;
    }

    // ---------- Online ----------

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

    // ---------- Selectie ----------

    /// <summary>Stelt een reeks samen volgens <see cref="QuestionTypes.Mix"/>; null als een type te weinig vragen heeft.</summary>
    private async Task<List<DailyQuestion>?> BuildMixAsync(
        List<QuestionType> types, string? tag, List<Player>? playerPool = null, HashSet<int>? excludeItems = null, int? questions = null,
        string? kaartCountry = null)
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
            var part = await BuildAsync(type, count, tag, QuestionTypes.Get(type).FromPlayers ? playerPool : null, usedPlayers, excludeItems, targets,
                kaartCountry);
            if (part is null) return null;
            all.AddRange(part);
        }
        // Afwisselen tussen de types in plaats van ze te groeperen. Daily en specials lopen op van makkelijk naar moeilijk
        // (stabiel gesorteerd: binnen dezelfde moeilijkheid blijven de types afwisselen en de delen van een kaart op volgorde).
        var alternated = all.Select((q, i) => (q, i)).GroupBy(x => x.q.Type)
            .SelectMany(g => g.Select((x, n) => (x.q, n))).OrderBy(x => x.n).Select(x => x.q);
        return (plan is null ? alternated : alternated.OrderBy(QuizService.DifficultyOf)).ToList();
    }

    /// <summary>
    /// Stelt <paramref name="count"/> vragen van één type samen, of null als er te weinig zijn.
    /// <paramref name="usedPlayers"/>: spelers die al in deze reeks zitten (bv. bij een kluts) komen niet nog eens voor.
    /// <paramref name="targets"/>: de gewenste moeilijkheid per vraag (zie <see cref="Difficulty.Plan"/>); null = geen plan (online).
    /// </summary>
    private async Task<List<DailyQuestion>?> BuildAsync(
        QuestionType type, int count, string? tag, List<Player>? playerPool = null, HashSet<int>? usedPlayers = null,
        HashSet<int>? excludeItems = null, List<int>? targets = null, string? kaartCountry = null)
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
                .Select(p => new DailyQuestion { Type = type, PlayerId = p.Id, Player = p }).ToList();
        }

        if (type == QuestionType.Kaart)
        {
            // Kaartdag: één kaart, gespeeld in twee delen (twee vragen op hetzelfde item): eerst de clubs, dan de stadions.
            if (await PickMapAsync(tag, kaartCountry, excludeItems) is not { } map) return null;
            return Enumerable.Range(1, QuestionRules.KaartParts)
                .Select(part => new DailyQuestion { Type = type, QuizItemId = map.Id, QuizItem = map, Part = part })
                .Take(count).ToList();
        }

        var items = await PickItemsAsync(type, count, tag, excludeItems, targets);
        return items.Count < count
            ? null
            : items.OrderBy(i => i.Difficulty).ThenBy(_ => Random.Shared.Next())
                .Select(i => new DailyQuestion
                {
                    Type = type,
                    QuizItemId = i.Id,
                    QuizItem = i,
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

        var approvedOnly = ApprovedOnly(type);
        var ordered = (await db.QuizItems.Where(i => i.Type == type && i.Status != PlayerStatus.Rejected
                && (!approvedOnly || i.Status == PlayerStatus.Approved)).ToListAsync())
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
    /// Kaart: een goedgekeurde, geldige kaart (<see cref="QuestionEngine.Problems"/> leeg: een oude kaart die niet meer aan de
    /// regels voldoet, wordt nooit gespeeld). De voorkeur, in deze volgorde:
    /// 1. het land dat de admin koos (<paramref name="country"/>);
    /// 2. anders de kaarten met het thema van de special (<paramref name="tag"/>), ook als dat het land van de vorige kaartdag is;
    /// 3. anders een ander land dan de vorige kaartdag;
    /// 4. anders alle kaarten.
    /// Daarbinnen eerst een ander land dan de vorige kaartdag, dan de langst niet gebruikte kaart. Null = geen kaart.
    /// </summary>
    private async Task<QuizItem?> PickMapAsync(string? tag, string? country, HashSet<int>? exclude)
    {
        var maps = new List<(QuizItem Item, string Land)>();
        foreach (var item in await db.QuizItems.Where(i => i.Type == QuestionType.Kaart && i.Status == PlayerStatus.Approved).ToListAsync())
        {
            if (exclude?.Contains(item.Id) == true) continue;
            // Eén kapotte of ongeldige rij legt de kaartdag niet plat: die kaart slaan we over.
            if (QuestionEngine.Problems(QuestionType.Kaart, item.Data) is [var problem, ..])
            {
                logger?.LogWarning("Kaart {Id} ({Key}) wordt overgeslagen: {Problem}", item.Id, item.Key, problem);
                continue;
            }
            maps.Add((item, QuestionEngine.Parse<KaartData>(item.Data).Land));
        }
        if (maps.Count == 0) return null;

        var played = await db.DailyQuestions.Where(q => q.Type == QuestionType.Kaart && q.QuizItemId != null)
            .Select(q => new { ItemId = q.QuizItemId!.Value, q.DailyChallenge.Date, QuestionId = q.Id }).ToListAsync();
        var lastUsed = played.GroupBy(x => x.ItemId).ToDictionary(g => g.Key, g => g.Max(x => x.Date));
        var previousId = played.OrderByDescending(x => x.Date).ThenByDescending(x => x.QuestionId).Select(x => (int?)x.ItemId).FirstOrDefault();
        var previousLand = previousId is { } id ? LandOf(await db.QuizItems.FindAsync(id)) : null;

        var wanted = KaartProjection.Find(country)?.Code;
        List<(QuizItem Item, string Land)> themed = tag is null ? [] : maps.Where(m => PlayerTags.Any(m.Item.Tags, tag)).ToList();
        var pool = wanted is not null && maps.Any(m => m.Land == wanted) ? maps.Where(m => m.Land == wanted).ToList()
            : themed.Count > 0 ? themed
            : maps.Any(m => m.Land != previousLand) ? maps.Where(m => m.Land != previousLand).ToList()
            : maps;
        return pool.OrderBy(m => m.Land == previousLand ? 1 : 0)
            .ThenBy(m => lastUsed.TryGetValue(m.Item.Id, out var d) ? d : DateOnly.MinValue)
            .ThenBy(_ => Random.Shared.Next())
            .First().Item;
    }

    /// <summary>Het land van een kaart, of null als de kaart weg of niet te lezen is.</summary>
    private static string? LandOf(QuizItem? item)
    {
        if (item is null) return null;
        try { return QuestionEngine.Parse<KaartData>(item.Data).Land; }
        catch (System.Text.Json.JsonException) { return null; }
    }

    /// <summary>
    /// Een solo-type (<see cref="QuestionTypeInfo.Alone"/>: een artikel van De Zondag, een zaak, een kaart) enkel als het goedgekeurd is:
    /// het is een hele dag, een fout weegt dan te zwaar.
    /// </summary>
    private static bool ApprovedOnly(QuestionType type) => QuestionTypes.Get(type).Alone;

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
}
