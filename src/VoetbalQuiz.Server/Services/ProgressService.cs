using Microsoft.EntityFrameworkCore;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Services;

/// <summary>Badges, bekers per seizoen en het spelersprofiel met head-to-head.</summary>
public class ProgressService(QuizDbContext db, TimeProvider clock, ResultsService results, OnlineService online, MonthRankingService ranking, GoldenBallService goldenBall)
{
    /// <summary>Alles wat we over één speler moeten weten om badges te bepalen.</summary>
    private record Facts(
        int DaysPlayed, int LongestStreak, int PerfectDays, int FirstHintHits, int DaysWon, int BestDay, int WrongGuesses,
        bool NightOwl, bool EarlyBird, int MatchesPlayed, int MatchesWon, int SpecialsPlayed, int SpecialsWon,
        int Level, int GoldenBalls, int GoldenBallStreak, int SilverBalls,
        int TypesPlayed, int ExactYears, int FastSnelvuur, int NietBijHits, int FirstClubHits, int EmojiHits,
        int FullLineups, int MonthPodiums, int OnlineWinStreak, bool FullWeek, int RedDevilHits, int ChampionsHits,
        int CleanDays, int TotalPoints, int CorrectTotal, int Stubborn, int LastSecond, int BigTableWins, int Rating);

    private record BadgeDef(string Id, string Icon, string Name, string Description, BadgeTier Tier, Func<Facts, int> Value, int Goal);

    private static readonly BadgeDef[] Badges =
    [
        new("aftrap", "🎯", "Aftrap", "Speel je eerste daily", BadgeTier.Brons, f => f.DaysPlayed, 1),
        new("vaste-waarde", "📅", "Vaste waarde", "Speel 30 dagen de daily", BadgeTier.Zilver, f => f.DaysPlayed, 30),
        new("clubicoon", "🏟️", "Clubicoon", "Speel 100 dagen de daily", BadgeTier.Goud, f => f.DaysPlayed, 100),
        new("op-dreef", "🔥", "Op dreef", "Speel 7 dagen op rij", BadgeTier.Zilver, f => f.LongestStreak, 7),
        new("onstuitbaar", "☄️", "Onstuitbaar", "Speel 30 dagen op rij", BadgeTier.Goud, f => f.LongestStreak, 30),
        new("perfecte-dag", "💯", "Perfecte dag", "Alle vragen van een daily juist", BadgeTier.Zilver, f => f.PerfectDays, 1),
        new("snelle-vinger", "⚡", "Snelle vinger", "10 keer juist bij de eerste tip", BadgeTier.Zilver, f => f.FirstHintHits, 10),
        new("bliksem", "🚀", "Bliksem", "50 keer juist bij de eerste tip", BadgeTier.Goud, f => f.FirstHintHits, 50),
        new("topdag", "🔝", "Topdag", "Haal 800 punten op één dag", BadgeTier.Zilver, f => f.BestDay, 800),
        new("dagwinnaar", "👑", "Dagwinnaar", "Win een daily", BadgeTier.Brons, f => f.DaysWon, 1),
        new("dagkoning", "🏅", "Dagkoning", "Win 10 keer de daily", BadgeTier.Goud, f => f.DaysWon, 10),
        new("online-debuut", "⚔️", "Online debuut", "Speel een online wedstrijd uit", BadgeTier.Brons, f => f.MatchesPlayed, 1),
        new("eerste-zege", "🥇", "Eerste zege", "Win een online wedstrijd", BadgeTier.Brons, f => f.MatchesWon, 1),
        new("tafelkampioen", "🏆", "Partijtjeskampioen", "Win 10 online wedstrijden", BadgeTier.Goud, f => f.MatchesWon, 10),
        new("feestganger", "✨", "Feestganger", "Speel een special", BadgeTier.Brons, f => f.SpecialsPlayed, 1),
        new("specialist", "🎉", "Specialist", "Win een special", BadgeTier.Goud, f => f.SpecialsWon, 1),
        new("seizoenskampioen", "⚽", "Gouden Bal", "Win de Gouden Bal: word nummer 1 van een maand", BadgeTier.Goud, f => f.GoldenBalls, 1),
        new("basisspeler", "⭐", "Basisspeler", "Bereik level 10", BadgeTier.Zilver, f => f.Level, 10),
        new("rode-duivel", "🌟", "Rode Duivel", "Bereik level 20", BadgeTier.Goud, f => f.Level, 20),
        new("nachtuil", "🦉", "Nachtuil", "Speel een vraag tussen middernacht en 5 uur", BadgeTier.Brons, f => f.NightOwl ? 1 : 0, 1),
        new("vroege-vogel", "🐦", "Vroege vogel", "Speel een vraag tussen 5 en 7 uur 's ochtends", BadgeTier.Brons, f => f.EarlyBird ? 1 : 0, 1),
        new("gokker", "🎲", "Gokker van dienst", "50 foute pogingen (gewaagd!)", BadgeTier.Brons, f => f.WrongGuesses, 50),

        // Vraagtypes
        new("allrounder", "🧠", "Allrounder", $"Speel elk van de {QuestionTypes.All.Count} vraagtypes", BadgeTier.Zilver, f => f.TypesPlayed, QuestionTypes.All.Count),
        new("jaartallenkenner", "📆", "Jaartallenkenner", "10 keer het exacte jaar", BadgeTier.Brons, f => f.ExactYears, 10),
        new("historicus", "📜", "Historicus", "50 keer het exacte jaar", BadgeTier.Goud, f => f.ExactYears, 50),
        new("supersnel", "💨", "Supersnel", "25 keer 90+ punten bij Snelvuur", BadgeTier.Zilver, f => f.FastSnelvuur, 25),
        new("vreemde-eend", "🦆", "Vreemde eend", "25 keer juist bij Wie hoort er niet bij?", BadgeTier.Zilver, f => f.NietBijHits, 25),
        new("scout", "🔭", "Scout", "10 keer een carrièrepad geraden bij de eerste club", BadgeTier.Zilver, f => f.FirstClubHits, 10),
        new("emoji-expert", "😎", "Emoji-expert", "25 emoji-raadsels opgelost", BadgeTier.Zilver, f => f.EmojiHits, 25),
        new("bondscoach", "📋", "Bondscoach", "Vul een opstelling volledig aan", BadgeTier.Brons, f => f.FullLineups, 1),
        new("trainer-van-het-jaar", "🧥", "Trainer van het jaar", "10 opstellingen volledig aangevuld", BadgeTier.Goud, f => f.FullLineups, 10),

        // Thema's
        new("rode-duivels-fan", "😈", "Rode Duivels-fan", "50 juiste antwoorden over de Rode Duivels", BadgeTier.Zilver, f => f.RedDevilHits, 50),
        new("cl-kenner", "🌌", "Champions League-kenner", "25 juiste antwoorden over de Champions League", BadgeTier.Zilver, f => f.ChampionsHits, 25),

        // Regelmaat en klassement
        new("volle-week", "🗓️", "Volle week", "Speel elke dag van een kalenderweek (ma–zo)", BadgeTier.Brons, f => f.FullWeek ? 1 : 0, 1),
        new("maandpodium", "🥉", "Maandpodium", "Eindig in de top 3 van een maand", BadgeTier.Zilver, f => f.MonthPodiums, 1),
        // Gouden Bal (de nummer 1 van een maand, zie GoldenBallService)
        new("zilveren-bal", "🥈", "Zilveren Bal", "Eindig als tweede in het maandklassement", BadgeTier.Zilver, f => f.SilverBalls, 1),
        new("titelverdediger", "🛡️", "Titelverdediger", "Win de Gouden Bal twee maanden na elkaar", BadgeTier.Goud, f => f.GoldenBallStreak, 2),
        new("gouden-hattrick", "🎩", "Gouden hattrick", "Win 3 Gouden Ballen", BadgeTier.Goud, f => f.GoldenBalls, 3),
        new("messi-niveau", "💫", "Messi-niveau", "Win 8 Gouden Ballen, net als Messi", BadgeTier.Goud, f => f.GoldenBalls, 8),

        new("specials-verzamelaar", "🎁", "Specials-verzamelaar", "Speel 5 specials", BadgeTier.Zilver, f => f.SpecialsPlayed, 5),

        // Online
        new("vaste-gast", "🪑", "Vaste gast", "Speel 25 online wedstrijden", BadgeTier.Zilver, f => f.MatchesPlayed, 25),
        new("online-veteraan", "🎖️", "Online veteraan", "Speel 100 online wedstrijden", BadgeTier.Goud, f => f.MatchesPlayed, 100),
        new("tafelkoning", "🤴", "Partijtjeskoning", "Win 3 online wedstrijden op rij", BadgeTier.Goud, f => f.OnlineWinStreak, 3),
        new("volle-tafel", "👥", "Vol partijtje", "Win een online wedstrijd tegen minstens 3 tegenstanders", BadgeTier.Zilver, f => f.BigTableWins, 1),
        new("elo-meester", "📈", "Elo-meester", "Bereik een online rating van 1200", BadgeTier.Goud, f => f.Rating, 1200),

        // Lange adem
        new("eeuwfeest", "🎂", "Seizoenkaart", "Speel 365 dagen de daily", BadgeTier.Goud, f => f.DaysPlayed, 365),
        new("ijzeren-man", "🦾", "IJzeren man", "Speel 100 dagen op rij", BadgeTier.Goud, f => f.LongestStreak, 100),
        new("legende", "🐐", "GOAT", "Bereik level 50", BadgeTier.Goud, f => f.Level, 50),
        new("puntenmachine", "🧮", "Puntenmachine", "Verzamel 10.000 punten (daily en specials)", BadgeTier.Zilver, f => f.TotalPoints, 10_000),
        new("puntenkanon", "💣", "Puntenkanon", "Verzamel 50.000 punten (daily en specials)", BadgeTier.Goud, f => f.TotalPoints, 50_000),
        new("scherpschutter", "🎯", "Scherpschutter", "100 juiste antwoorden", BadgeTier.Brons, f => f.CorrectTotal, 100),
        new("encyclopedie", "📚", "Wandelende encyclopedie", "1000 juiste antwoorden", BadgeTier.Goud, f => f.CorrectTotal, 1000),

        // Topprestaties en karakter
        new("perfectionist", "💎", "Perfectionist", "5 perfecte dagen", BadgeTier.Goud, f => f.PerfectDays, 5),
        new("foutloos", "🧼", "Clean sheet", "Een volledige daily alles juist, zonder één foute poging", BadgeTier.Zilver, f => f.CleanDays, 1),
        new("topdag-plus", "🌠", "Sterrendag", "Haal 900 punten op één dag", BadgeTier.Goud, f => f.BestDay, 900),
        new("doorzetter", "🐂", "Doorzetter", "Toch nog juist na 3 of meer foute pogingen", BadgeTier.Brons, f => f.Stubborn, 1),
        new("op-de-valreep", "⏱️", "Op de valreep", "10 keer juist in de allerlaatste seconden van Wie ben ik?", BadgeTier.Brons, f => f.LastSecond, 10),
    ];

    // ---------- Profiel ----------

    public async Task<PublicProfile> GetProfileAsync(string viewerId, string userName)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.UserName == userName)
            ?? throw new QuizException("Deze speler bestaat niet.");

        var (facts, badges, trophies, rating) = await BadgesAsync(user.Id);
        var level = await results.LevelOfAsync(user.Id);

        return new PublicProfile(
            user.UserName!, user.AvatarId, level, QuizClock.ToBrusselsDate(user.CreatedAt), user.Id == viewerId,
            facts.DaysPlayed, facts.DaysWon, facts.BestDay, rating, facts.MatchesWon,
            badges, trophies.OrderByDescending(t => t.Place == 1).ThenBy(t => t.Place).ToList(),
            user.Id == viewerId ? null : await HeadToHeadAsync(viewerId, user.Id));
    }

    private async Task<(Facts Facts, List<BadgeState> Badges, List<Trophy> Trophies, int Rating)> BadgesAsync(string userId)
    {
        var trophies = (await TrophiesAsync()).GetValueOrDefault(userId) ?? [];
        var rating = (await online.GetRatingLeaderboardAsync(userId)).Entries.FirstOrDefault(e => e.IsMe)?.Value ?? MatchRules.StartRating;
        var facts = await FactsAsync(userId, trophies, rating);

        var badges = Badges.Select(b =>
        {
            var value = b.Value(facts);
            var earned = value >= b.Goal;
            return new BadgeState(b.Id, b.Icon, b.Name, b.Description, b.Tier, earned,
                earned || b.Goal == 1 ? null : $"{Math.Min(value, b.Goal)}/{b.Goal}");
        }).OrderByDescending(b => b.Earned).ThenByDescending(b => b.Tier).ToList();
        return (facts, badges, trophies, rating);
    }

    // ---------- Beloningspopup ----------

    /// <summary>
    /// De XP en badges die erbij kwamen sinds de vorige popup, en legt vast dat de speler ze nu gezien heeft.
    /// Alles wordt hier berekend; de client toont enkel wat de server teruggeeft.
    /// Wie nog nooit een popup kreeg (spelers van vóór deze functie), begint stil vanaf wat die al had.
    /// </summary>
    public async Task<RewardSummary> ClaimRewardAsync(string userId)
    {
        var user = await db.Users.SingleAsync(u => u.Id == userId);
        var input = (await results.XpInputsAsync()).GetValueOrDefault(userId) ?? new XpInput(0, 0, 0, 0, 0);
        var parts = Levels.Breakdown(input);
        var earned = (await BadgesAsync(userId)).Badges.Where(b => b.Earned).ToList();

        var seenParts = user.SeenXpParts ?? parts.Select(p => p.Xp).ToList();
        var seenBadges = user.SeenBadges ?? earned.Select(b => b.Id).ToList();

        // Een XP-onderdeel dat later bij Levels kwam, telt pas vanaf nu mee (geen plotse berg XP).
        var gained = parts
            .Select((p, i) => p with { Xp = i < seenParts.Count ? p.Xp - seenParts[i] : 0 })
            .Where(p => p.Xp > 0)
            .ToList();
        var newBadges = earned.Where(b => !seenBadges.Contains(b.Id)).ToList();

        user.SeenXpParts = parts.Select(p => p.Xp).ToList();
        user.SeenBadges = seenBadges.Union(earned.Select(b => b.Id)).ToList();
        await db.SaveChangesAsync();

        var xp = parts.Sum(p => p.Xp);
        return new RewardSummary(Levels.For(xp - gained.Sum(p => p.Xp)), Levels.For(xp), gained, newBadges);
    }

    private async Task<Facts> FactsAsync(string userId, List<Trophy> trophies, int rating)
    {
        await results.SettleFinishedDaysAsync();
        var attempts = await db.Attempts
            .Where(a => a.UserId == userId && a.AnsweredAt != null)
            .Select(a => new
            {
                a.DailyQuestion.DailyChallengeId,
                a.DailyQuestion.DailyChallenge.Date,
                Special = a.DailyQuestion.DailyChallenge.SpecialEventId != null,
                a.DailyQuestion.Type,
                PlayerTags = a.DailyQuestion.Player != null ? a.DailyQuestion.Player.Tags : null,
                ItemTags = a.DailyQuestion.QuizItem != null ? a.DailyQuestion.QuizItem.Tags : null,
                a.Points, a.Correct, a.WrongGuesses, a.StartedAt, a.AnsweredAt,
            })
            .ToListAsync();
        bool Tagged(List<string>? player, List<string>? item, string tag) =>
            (player?.Contains(tag) ?? false) || (item?.Contains(tag) ?? false);
        int Hits(QuestionType type, Func<int, bool>? points = null) =>
            attempts.Count(a => a.Type == type && a.Correct && (points == null || points(a.Points)));

        var sizes = await results.ChallengeSizesAsync();
        var daily = attempts.Where(a => !a.Special).ToList();
        var days = daily.GroupBy(a => a.DailyChallengeId).ToList();
        var dates = days.Select(d => d.First().Date).Distinct().Order().ToList();
        var hours = attempts.Select(a => QuizClock.ToBrussels(a.StartedAt).Hour).ToList();

        var challengeWins = await db.DailyChallenges.Where(c => c.WinnerUserId == userId)
            .Select(c => c.SpecialEventId != null).ToListAsync();
        // Een zege telt enkel als er genoeg spelers bleven; een groot partijtje = 4 spelers die bleven.
        var bigTableWins = await db.MatchPlayers.CountAsync(p => p.UserId == userId && p.Rank == 1
            && p.Match.Status == MatchStatus.Finished && p.Match.Players.Count(o => o.LeftAt == null) >= 4);
        var matches = await db.MatchPlayers.Where(p => p.UserId == userId && p.Match.Status == MatchStatus.Finished)
            .OrderBy(p => p.Match.FinishedAt)
            .Select(p => new { p.Rank, Counts = p.Match.Players.Count(o => o.LeftAt == null) >= MatchRules.StayersForWin })
            .ToListAsync();
        var goldenBalls = await goldenBall.WonByAsync(userId);
        int winStreak = 0, wins = 0;
        foreach (var m in matches)
        {
            if (m.Rank == 1 && !m.Counts) continue; // als enige over: geen zege, reeks blijft staan
            wins = m.Rank == 1 ? wins + 1 : 0;
            winStreak = Math.Max(winStreak, wins);
        }

        return new Facts(
            days.Count,
            LongestRun(dates),
            days.Count(d => d.Count(a => a.Correct) == sizes.GetValueOrDefault(d.Key)),
            daily.Count(a => a.Points == 100),
            challengeWins.Count(special => !special),
            days.Count == 0 ? 0 : days.Max(d => d.Sum(a => a.Points)),
            attempts.Sum(a => a.WrongGuesses),
            hours.Any(h => h < 5),
            hours.Any(h => h is >= 5 and < 7),
            matches.Count,
            matches.Count(m => m.Rank == 1 && m.Counts),
            attempts.Where(a => a.Special).Select(a => a.DailyChallengeId).Distinct().Count(),
            challengeWins.Count(special => special),
            (await results.LevelOfAsync(userId)).Level,
            goldenBalls.Count,
            LongestMonthRun(goldenBalls),
            trophies.Count(t => t.Kind == TrophyKind.Month && t.Place == 2),
            attempts.Select(a => a.Type).Distinct().Count(),
            Hits(QuestionType.Jaar, p => p == 100),
            Hits(QuestionType.Snelvuur, p => p >= 90),
            Hits(QuestionType.NietBij),
            Hits(QuestionType.Carriere, p => p == 100),
            Hits(QuestionType.Emoji),
            attempts.Count(a => a.Type == QuestionType.Opstelling && a.Points >= QuestionTypes.Get(QuestionType.Opstelling).MaxPoints),
            trophies.Count(t => t.Kind == TrophyKind.Month && t.Place <= 3),
            winStreak,
            HasFullWeek(dates),
            attempts.Count(a => a.Correct && Tagged(a.PlayerTags, a.ItemTags, "rode-duivels")),
            attempts.Count(a => a.Correct && Tagged(a.PlayerTags, a.ItemTags, "champions-league")),
            days.Count(d => d.Count() == sizes.GetValueOrDefault(d.Key) && d.All(a => a.Correct && a.WrongGuesses == 0)),
            attempts.Sum(a => a.Points),
            attempts.Count(a => a.Correct),
            attempts.Count(a => a.Correct && a.WrongGuesses >= 3),
            attempts.Count(a => a.Type == QuestionType.WieBenIk && a.Correct
                && (a.AnsweredAt!.Value - a.StartedAt).TotalSeconds >= GameRules.TimeLimitSeconds - GameRules.LastChanceSeconds),
            bigTableWins,
            rating);
    }

    /// <summary>Zeven dagen op rij, van maandag tot en met zondag.</summary>
    private static bool HasFullWeek(List<DateOnly> sorted)
    {
        var set = sorted.ToHashSet();
        return sorted.Any(d => d.DayOfWeek == DayOfWeek.Monday && Enumerable.Range(1, 6).All(i => set.Contains(d.AddDays(i))));
    }

    private static int LongestRun(List<DateOnly> sorted)
    {
        int longest = 0, run = 0;
        for (var i = 0; i < sorted.Count; i++)
        {
            run = i > 0 && sorted[i - 1].AddDays(1) == sorted[i] ? run + 1 : 1;
            longest = Math.Max(longest, run);
        }
        return longest;
    }

    /// <summary>Meeste maanden na elkaar (jaar, maand; oudste eerst).</summary>
    private static int LongestMonthRun(List<(int Year, int Month)> sorted)
    {
        int longest = 0, run = 0;
        for (var i = 0; i < sorted.Count; i++)
        {
            var next = i > 0 && sorted[i - 1].Year * 12 + sorted[i - 1].Month + 1 == sorted[i].Year * 12 + sorted[i].Month;
            run = next ? run + 1 : 1;
            longest = Math.Max(longest, run);
        }
        return longest;
    }

    // ---------- Seizoenen en bekers ----------

    /// <summary>
    /// Elke afgelopen maand is een seizoen: de top 3 van het maandklassement (daily + specials + online) krijgt een beker.
    /// Wie een special wint, krijgt ook een beker.
    /// </summary>
    public async Task<Dictionary<string, List<Trophy>>> TrophiesAsync()
    {
        await results.SettleFinishedDaysAsync();
        var trophies = new Dictionary<string, List<Trophy>>();
        void AddFor(string userId, Trophy t)
        {
            if (!trophies.TryGetValue(userId, out var list)) trophies[userId] = list = [];
            list.Add(t);
        }

        foreach (var (userId, season, place, points) in await ranking.PodiumsPerMonthAsync(QuizClock.Today(clock)))
            AddFor(userId, new Trophy(TrophyKind.Month, season, place, points));

        var ids = await db.Users.ToDictionaryAsync(u => u.UserName!, u => u.Id);
        void Add(string userName, Trophy t)
        {
            if (ids.TryGetValue(userName, out var id)) AddFor(id, t);
        }

        var specialWins = await db.DailyChallenges
            .Where(c => c.SpecialEventId != null && c.WinnerUserId != null)
            .Select(c => new { c.Winner!.UserName, c.SpecialEvent!.Name, c.SpecialEvent.Icon, c.WinnerPoints })
            .ToListAsync();
        foreach (var w in specialWins)
            Add(w.UserName!, new Trophy(TrophyKind.Special, $"{w.Icon} {w.Name}", 1, w.WinnerPoints ?? 0));

        return trophies;
    }

    // ---------- Head-to-head ----------

    private async Task<HeadToHead> HeadToHeadAsync(string me, string other)
    {
        // Daily: elke dag waarop jullie allebei speelden, wie had meer punten?
        var totals = await db.Attempts
            .Where(a => a.AnsweredAt != null && (a.UserId == me || a.UserId == other)
                        && a.DailyQuestion.DailyChallenge.SpecialEventId == null)
            .GroupBy(a => new { a.DailyQuestion.DailyChallengeId, a.UserId })
            .Select(g => new { g.Key.DailyChallengeId, g.Key.UserId, Points = g.Sum(a => a.Points) })
            .ToListAsync();
        var days = totals.GroupBy(t => t.DailyChallengeId).Where(g => g.Count() == 2)
            .Select(g => (Me: g.Single(t => t.UserId == me).Points, Other: g.Single(t => t.UserId == other).Points))
            .ToList();

        // Online: wedstrijden in hetzelfde partijtje, wie eindigde hoger?
        var ranks = await db.MatchPlayers
            .Where(p => (p.UserId == me || p.UserId == other) && p.Match.Status == MatchStatus.Finished)
            .Select(p => new { p.MatchId, p.UserId, p.Rank })
            .ToListAsync();
        var matches = ranks.GroupBy(r => r.MatchId).Where(g => g.Count() == 2)
            .Select(g => (Me: g.Single(r => r.UserId == me).Rank ?? 99, Other: g.Single(r => r.UserId == other).Rank ?? 99))
            .ToList();

        return new HeadToHead(
            days.Count, days.Count(d => d.Me > d.Other), days.Count(d => d.Other > d.Me), days.Count(d => d.Me == d.Other),
            matches.Count, matches.Count(m => m.Me < m.Other), matches.Count(m => m.Other < m.Me));
    }
}
