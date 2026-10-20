using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

// Vult de LOKALE database met tien testgebruikers die al een maand spelen: dailies, specials,
// online wedstrijden en feedback. De punten komen uit de echte spelregels (QuestionEngine);
// dagwinnaars, klassementen, badges en de Gouden Bal rekent de app zelf uit.
// Gebruik: dotnet run --project tools/VoetbalQuiz.TestData

// Wachtwoord van alle testgebruikers (enkel voor de lokale ontwikkeldatabase).
const string Password = "truitje-test";
const string EmailDomain = "@example.test";

var root = FindRepoRoot();
var config = new ConfigurationBuilder()
    .AddJsonFile(Path.Combine(root, "src", "VoetbalQuiz.Server", "appsettings.Development.json"))
    .Build();
var connection = config.GetConnectionString("Quiz")!;
if (!connection.Contains("localhost") && !connection.Contains("127.0.0.1"))
    throw new InvalidOperationException("Dit script is enkel bedoeld voor de lokale database.");

var clock = new SimClock();
var services = new ServiceCollection();
services.AddLogging();
services.AddDbContext<QuizDbContext>(o => o.UseNpgsql(connection));
services.AddIdentityCore<AppUser>(o =>
    {
        o.Password.RequiredLength = 6;
        o.Password.RequireDigit = false;
        o.Password.RequireLowercase = false;
        o.Password.RequireUppercase = false;
        o.Password.RequireNonAlphanumeric = false;
        o.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<QuizDbContext>();
services.AddSingleton<TimeProvider>(clock);
services.AddMemoryCache();
services.AddScoped<QuizService>();
services.AddScoped<ResultsService>();
services.AddScoped<MonthRankingService>();
services.AddScoped<GoldenBallService>();

await using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
var sp = scope.ServiceProvider;
var db = sp.GetRequiredService<QuizDbContext>();
var userManager = sp.GetRequiredService<UserManager<AppUser>>();
var quiz = sp.GetRequiredService<QuizService>();
var results = sp.GetRequiredService<ResultsService>();
var goldenBall = sp.GetRequiredService<GoldenBallService>();

var rng = new Random(2026);
var realNow = DateTime.UtcNow;
var today = QuizClock.Today(TimeProvider.System);
var firstDay = today.AddDays(-30);

// Naam, avatar, kennis (0–1), hoe vaak ze spelen, gewoon speeluur (minuten na middernacht), dag van inschrijving, gokdrang.
Profile[] profiles =
[
    new("jens", "debruyne", 0.82, 0.95, 6 * 60 + 30, 0, 0.10),   // vroege vogel, speelt bijna elke dag
    new("lotte", "courtois", 0.75, 0.90, 20 * 60, 0, 0.12),
    new("wout", "lukaku", 0.64, 0.85, 12 * 60 + 15, 0, 0.30),     // gokt graag
    new("elien", "mertens", 0.70, 0.75, 21 * 60, 1, 0.15),
    new("kobe", "hazard", 0.58, 0.80, 23 * 60 + 20, 2, 0.35),     // nachtuil
    new("fien", "doku", 0.62, 0.60, 18 * 60, 4, 0.15),
    new("arne", "kompany", 0.78, 0.70, 8 * 60, 6, 0.08),
    new("nina", "mbappe", 0.50, 0.65, 19 * 60 + 30, 9, 0.20),
    new("milan", "scifo", 0.55, 0.50, 13 * 60, 12, 0.25),
    new("sofie", "witsel", 0.68, 0.88, 22 * 60, 15, 0.12),
];

if (await db.Users.AnyAsync(u => profiles.Select(p => p.Name).Contains(u.UserName!)))
{
    Console.WriteLine("De testgebruikers staan al in de database: er is niets gewijzigd.");
    return;
}

await using var tx = await db.Database.BeginTransactionAsync();

// ---------- Gebruikers ----------

var ids = new Dictionary<Profile, string>();
foreach (var p in profiles)
{
    var user = new AppUser
    {
        UserName = p.Name,
        Email = p.Name + EmailDomain,
        EmailConfirmed = true,
        AvatarId = p.Avatar,
        CreatedAt = Local(firstDay.AddDays(p.JoinDay), Math.Max(10, p.Minutes - 25)),
    };
    var created = await userManager.CreateAsync(user, Password);
    if (!created.Succeeded) throw new InvalidOperationException(string.Join(", ", created.Errors.Select(e => e.Description)));
    ids[p] = user.Id;
}
Console.WriteLine($"{profiles.Length} gebruikers aangemaakt.");

// Al afgerekende dagen in de periode opnieuw laten afrekenen, zodat de testgebruikers meetellen voor de dagwinnaar.
foreach (var c in await db.DailyChallenges.Where(c => c.SpecialEventId == null && c.Date >= firstDay && c.SettledAt != null).ToListAsync())
{
    c.SettledAt = null;
    c.WinnerUserId = null;
    c.WinnerPoints = null;
}

// ---------- Specials ----------

var specials = new List<SpecialEvent>
{
    new()
    {
        Theme = "champions-league", Name = "Champions League-avond", Icon = "⭐",
        Description = "Speeldag in de Champions League: de grootste clubs en hun sterren.",
        StartDate = firstDay.AddDays(12), EndDate = firstDay.AddDays(12), Tag = "champions-league",
        Types = [QuestionType.WieBenIk, QuestionType.Carriere],
        CreatedAt = Local(firstDay.AddDays(5), 20 * 60),
    },
    new()
    {
        Theme = "custom", Name = "Rode Duivels-weekend", Icon = "😈",
        Description = "Interlandweekend: alles over de Rode Duivels, van Scifo tot Doku.",
        StartDate = firstDay.AddDays(22), EndDate = firstDay.AddDays(23), Tag = "rode-duivels",
        Types = [QuestionType.WieBenIk, QuestionType.Kluts, QuestionType.Snelvuur],
        CreatedAt = Local(firstDay.AddDays(16), 21 * 60),
    },
};
db.SpecialEvents.AddRange(specials);
await db.SaveChangesAsync();

// ---------- Een maand spelen ----------

var allPlayers = await db.Players.ToListAsync();
var playersById = allPlayers.ToDictionary(p => p.Id);
var itemsById = await db.QuizItems.ToDictionaryAsync(i => i.Id);
var activeTypes = await quiz.ActiveTypesAsync();
var specialChallenges = new Dictionary<int, DailyChallenge>();
var playedSpecial = new HashSet<(int, Profile)>();
int attempts = 0, matches = 0;

for (var day = firstDay; day <= today; day = day.AddDays(1))
{
    var index = day.DayNumber - firstDay.DayNumber;

    // 's Nachts: de dag van gisteren afrekenen en op de eerste van de maand de Gouden Bal.
    clock.At = Local(day, 2 * 60 + 30);
    if (clock.At < realNow)
    {
        await results.SettleFinishedDaysAsync();
        await goldenBall.SettleAsync();
    }

    clock.At = Local(day, 0).AddSeconds(5);
    var challenge = await quiz.GetOrCreateChallengeAsync(day);
    foreach (var special in specials.Where(s => s.StartDate == day))
        specialChallenges[special.Id] = await quiz.GetOrCreateSpecialChallengeAsync(special.Id);

    var weekend = day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
    foreach (var p in profiles.Where(p => p.JoinDay <= index))
    {
        var chance = index == p.JoinDay ? 1 : p.Activity + (weekend ? 0.05 : 0);
        if (rng.NextDouble() >= chance) continue;

        var start = Local(day, SessionMinutes(p));
        if (start > realNow) continue;
        var end = Play(challenge, p, start);

        // Specials speel je na je daily (een special van twee dagen maar één keer).
        foreach (var special in specials.Where(s => s.StartDate <= day && day <= s.EndDate))
        {
            if (playedSpecial.Contains((special.Id, p)) || rng.NextDouble() >= 0.75) continue;
            playedSpecial.Add((special.Id, p));
            Play(specialChallenges[special.Id], p, end.AddMinutes(rng.Next(1, 20)));
        }
    }

    // Online wedstrijden: 's middags of 's avonds, in het weekend wat meer.
    if (index >= 2)
    {
        var count = Poisson(weekend ? 2.2 : 1.1);
        for (var m = 0; m < count; m++)
        {
            var minutes = rng.NextDouble() < 0.25 ? rng.Next(12 * 60, 13 * 60 + 30) : rng.Next(19 * 60, 22 * 60 + 45);
            var createdAt = Local(day, minutes);
            if (createdAt > realNow.AddMinutes(-30)) continue;
            var available = profiles.Where(p => p.JoinDay <= index).OrderBy(p => rng.NextDouble() / p.Activity).ToList();
            var size = Math.Min(available.Count, 2 + Poisson(1.3));
            if (await AddMatchAsync(createdAt, available.Take(size).ToList())) matches++;
        }
        // Af en toe een partijtje dat nooit start.
        if (rng.NextDouble() < 0.08)
        {
            var host = profiles.Where(p => p.JoinDay <= index).OrderBy(_ => rng.Next()).First();
            var createdAt = Local(day, rng.Next(18 * 60, 22 * 60));
            if (createdAt < realNow.AddHours(-3))
                db.Matches.Add(new Match
                {
                    Name = MatchName(), HostUserId = ids[host], Seats = 4, Status = MatchStatus.Cancelled, CreatedAt = createdAt,
                    Types = [QuestionType.WieBenIk],
                    Players = [new MatchPlayer { UserId = ids[host], JoinedAt = createdAt }],
                });
        }
    }

    await db.SaveChangesAsync();
}

// ---------- Feedback ----------

(string Who, FeedbackType Type, string Title, string Description, int Day, FeedbackStatus Status, string? Reply)[] feedback =
[
    ("lotte", FeedbackType.Bug, "Naam zonder accent fout gerekend", "Ik typte \"Mbappe\" zonder accent en kreeg eerst een fout. Daarna werkte het wel?", 4,
        FeedbackStatus.Opgelost, "Bedankt! Accenten en hoofdletters maken nu niets meer uit."),
    ("jens", FeedbackType.Feature, "Klassement per vriendengroep", "Zou leuk zijn om een apart klassement te hebben met enkel mijn ploegmaats van de zaterdag.", 8,
        FeedbackStatus.Gepland, "Goed idee, dit staat op de planning."),
    ("kobe", FeedbackType.Feature, "Donkere modus", "Ik speel vaak 's nachts en het scherm is dan wel fel.", 11, FeedbackStatus.Bekeken, null),
    ("elien", FeedbackType.Feature, "Elke dag een special", "De specials zijn het leukste, kan dat niet elke dag?", 14,
        FeedbackStatus.Afgewezen, "Dan zijn ze niet meer speciaal 😉 Maar er komen er zeker nog."),
    ("wout", FeedbackType.Feature, "Meer vragen over de Pro League", "Graag meer spelers van Genk, Gent en Antwerp in Wie ben ik.", 19, FeedbackStatus.Nieuw, null),
    ("nina", FeedbackType.Bug, "Opstelling valt van het scherm", "Op mijn kleine gsm zie ik de rechtsachter niet bij een opstelling.", 25, FeedbackStatus.Nieuw, null),
];
foreach (var f in feedback)
{
    var p = profiles.Single(x => x.Name == f.Who);
    db.Feedback.Add(new Feedback
    {
        UserId = ids[p], Type = f.Type, Title = f.Title, Description = f.Description,
        CreatedAt = Local(firstDay.AddDays(f.Day), p.Minutes + 15), Status = f.Status, Reply = f.Reply,
    });
}
await db.SaveChangesAsync();

// Tot vandaag afrekenen, met de echte klok.
clock.At = null;
await results.SettleFinishedDaysAsync();
await goldenBall.SettleAsync();
await tx.CommitAsync();

Console.WriteLine($"{attempts} antwoorden, {matches} online wedstrijden, {specials.Count} specials en {feedback.Length} feedbackberichten toegevoegd ({firstDay:dd/MM} – {today:dd/MM}).");
return;

// ---------- Hulpfuncties ----------

DateTime Play(DailyChallenge c, Profile p, DateTime start)
{
    var t = start;
    var stopAfter = rng.NextDouble() < 0.06 ? rng.Next(3, c.Questions.Count) : int.MaxValue;
    foreach (var q in c.Questions.OrderBy(q => q.Number))
    {
        if (q.Number > stopAfter) break;
        var a = new Attempt { UserId = ids[p], DailyQuestionId = q.Id, StartedAt = t };
        Simulate(q, a, p);
        if (a.AnsweredAt > realNow) break;
        db.Attempts.Add(a);
        attempts++;
        t = a.AnsweredAt!.Value.AddSeconds(rng.Next(3, 18));
        if (rng.NextDouble() < 0.03) t = t.AddMinutes(rng.Next(20, 150));
    }
    return t;
}

async Task<bool> AddMatchAsync(DateTime createdAt, List<Profile> seated)
{
    clock.At = createdAt;
    var types = rng.NextDouble() < 0.45
        ? [activeTypes[rng.Next(activeTypes.Count)]]
        : activeTypes.OrderBy(_ => rng.Next()).Take(rng.Next(2, 5)).Order().ToList();
    var questions = await quiz.BuildOnlineAsync(types);
    if (questions is null)
    {
        types = [QuestionType.WieBenIk];
        questions = await quiz.BuildOnlineAsync(types);
        if (questions is null) return false;
    }

    var match = new Match
    {
        Name = MatchName(), HostUserId = ids[seated[0]], Seats = Math.Min(MatchRules.MaxSeats, seated.Count + rng.Next(0, 3)),
        Status = MatchStatus.Finished, CreatedAt = createdAt, Types = types,
    };
    foreach (var (p, i) in seated.Select((p, i) => (p, i)))
        match.Players.Add(new MatchPlayer { UserId = ids[p], JoinedAt = createdAt.AddSeconds(i == 0 ? 0 : rng.Next(10, 240)) });
    match.StartedAt = match.Players.Max(p => p.JoinedAt).AddSeconds(rng.Next(15, 90));

    var startsAt = match.StartedAt.Value.AddSeconds(MatchRules.CountdownSeconds);
    var number = 1;
    foreach (var q in questions)
    {
        // De engine heeft de speler of de vraag zelf nodig, niet enkel het id.
        q.Player = q.PlayerId is { } pid ? playersById[pid] : null;
        q.QuizItem = q.QuizItemId is { } iid ? itemsById[iid] : null;
        var limit = QuestionEngine.TimeLimit(q);
        var mq = new MatchQuestion
        {
            Number = number++, Type = q.Type, PlayerId = q.PlayerId, QuizItemId = q.QuizItemId,
            Blanks = q.Blanks, CounterPlayerIds = q.CounterPlayerIds, StartsAt = startsAt,
        };

        var timeUp = false;
        var lastAnswer = startsAt;
        foreach (var p in seated)
        {
            var a = new Attempt { UserId = ids[p], StartedAt = startsAt };
            var outcome = Simulate(q, a, p);
            var expired = outcome == Ending.Expired;
            if (expired)
            {
                timeUp = true;
                if (a.LastGuess is null) continue; // nooit iets geprobeerd: geen antwoord
            }
            else lastAnswer = a.AnsweredAt!.Value > lastAnswer ? a.AnsweredAt.Value : lastAnswer;

            mq.Answers.Add(new MatchAnswer
            {
                UserId = ids[p], WrongGuesses = a.WrongGuesses, LastGuess = a.LastGuess, Correct = a.Correct,
                GaveUp = outcome == Ending.GaveUp, AnsweredAt = expired ? null : a.AnsweredAt, SpeedPoints = a.Points,
                Choice = a.Choice, Found = a.Found, Skipped = a.Skipped,
            });
        }
        mq.EndedAt = timeUp ? startsAt.AddSeconds(limit + GameRules.LatencyGraceSeconds + rng.NextDouble()) : lastAnswer.AddSeconds(rng.NextDouble());

        // Zoals MatchService.EndQuestion: positiebonus voor de snelste juiste antwoorden.
        var position = 1;
        foreach (var answer in mq.Answers.Where(a => a.Correct).OrderBy(a => a.AnsweredAt))
        {
            answer.Position = position;
            answer.Bonus = MatchRules.BonusFor(position);
            position++;
        }
        foreach (var answer in mq.Answers)
            match.Players.Single(mp => mp.UserId == answer.UserId).Score += answer.SpeedPoints + answer.Bonus;

        match.Questions.Add(mq);
        startsAt = mq.EndedAt.Value.AddSeconds(MatchRules.RevealSeconds + rng.NextDouble());
    }

    match.FinishedAt = startsAt;
    var ordered = match.Players.OrderByDescending(mp => mp.Score).ToList();
    for (var i = 0; i < ordered.Count; i++)
        ordered[i].Rank = i > 0 && ordered[i - 1].Score == ordered[i].Score ? ordered[i - 1].Rank : i + 1;
    db.Matches.Add(match);
    return true;
}

// Speelt één vraag zoals een speler met dit profiel dat zou doen, via de echte puntentelling.
Ending Simulate(DailyQuestion q, Attempt a, Profile p)
{
    var limit = QuestionEngine.TimeLimit(q);
    var know = Know(p, q.Player?.Difficulty ?? q.QuizItem?.Difficulty ?? 2);
    var knows = rng.NextDouble() < know;

    switch (q.Type)
    {
        case QuestionType.Snelvuur or QuestionType.NietBij:
        {
            if (!knows && rng.NextDouble() < 0.08) return Expire();
            var correct = q.Type == QuestionType.Snelvuur
                ? QuestionEngine.Parse<SnelvuurData>(q.QuizItem!.Data).Correct
                : QuestionEngine.Parse<NietBijData>(q.QuizItem!.Data).Odd;
            var choice = knows ? correct : rng.Next(4);
            var t = 0.9 + Math.Pow(rng.NextDouble(), 1 + p.Skill) * (limit - 1.3);
            return Guess(choice.ToString(), t);
        }

        case QuestionType.Jaar:
        {
            var year = QuestionEngine.Parse<JaarData>(q.QuizItem!.Data).Year;
            var off = knows ? (rng.NextDouble() < 0.6 ? 0 : rng.Next(-2, 3)) : rng.Next(-8, 9);
            return Guess(Math.Clamp(year + off, QuestionRules.MinYear, QuestionRules.MaxYear).ToString(), 2 + rng.NextDouble() * 7);
        }

        case QuestionType.Opstelling:
        {
            var lineup = QuestionEngine.Parse<OpstellingData>(q.QuizItem!.Data);
            var t = 2.0;
            foreach (var blank in q.Blanks.OrderBy(_ => rng.Next()))
            {
                t += 4 + rng.NextDouble() * 7;
                if (t > limit - 0.5) return Expire();
                if (rng.NextDouble() < know && Guess(lineup.Players[blank].Name, t) == Ending.Done) return Ending.Done;
            }
            return t < limit - 5 && rng.NextDouble() < 0.6 ? GiveUp(t + 2) : Expire();
        }

        case QuestionType.Counter:
        {
            var t = 1.0;
            foreach (var player in q.CounterPlayers)
            {
                var kn = rng.NextDouble() < Know(p, player.Difficulty) - 0.05;
                t += kn ? 2.5 + rng.NextDouble() * 4 : 2 + rng.NextDouble() * 3;
                if (t > limit) return Expire();
                if (Guess(kn ? player.Name : QuestionRules.CounterSkip, t) == Ending.Done) return Ending.Done;
            }
            return Expire();
        }

        default: // Getypte namen: wie ben ik, carrièrepad, emoji, wissel, rugnummer, mannetje, kluts
        {
            var answer = QuestionEngine.AnswerLabel(q);
            if (!knows && rng.NextDouble() < 0.25)
            {
                knows = true; // valt toch nog op zijn plaats, maar laat
                know = 0;
            }
            if (knows)
            {
                var t = know == 0
                    ? limit * (0.65 + rng.NextDouble() * 0.3)
                    : 1.8 + Math.Pow(rng.NextDouble(), 1 + 2 * p.Skill) * (limit - 3);
                if (rng.NextDouble() < p.Wrongness) Guess(WrongName(answer), t * (0.3 + rng.NextDouble() * 0.5));
                return Guess(answer, t);
            }

            var wrong = rng.NextDouble() < p.Wrongness * 2 ? rng.Next(1, 4) : 0;
            var at = 3.0;
            for (var i = 0; i < wrong && at < limit - 2; i++)
            {
                at += rng.NextDouble() * (limit - at - 1) / 2;
                Guess(WrongName(answer), at);
            }
            return rng.NextDouble() < 0.45 ? GiveUp(Math.Min(limit - 0.5, at + 2 + rng.NextDouble() * 4)) : Expire();
        }
    }

    // Zoals QuizService.GuessAsync.
    Ending Guess(string guess, double elapsed)
    {
        a.LastGuess = guess;
        var outcome = QuestionEngine.Guess(q, a, guess, elapsed);
        if (outcome.Wrong) a.WrongGuesses++;
        a.Points = outcome.Points;
        if (!outcome.Done) return Ending.Open;
        a.AnsweredAt = a.StartedAt.AddSeconds(elapsed);
        a.Correct = outcome.Correct;
        return Ending.Done;
    }

    // Zoals QuizService.GiveUpAsync.
    Ending GiveUp(double elapsed)
    {
        a.AnsweredAt = a.StartedAt.AddSeconds(elapsed);
        a.Correct = false;
        a.Points = QuestionEngine.PointsWhenStopped(q, a);
        return Ending.GaveUp;
    }

    // Zoals QuizService.CloseIfExpired.
    Ending Expire()
    {
        a.AnsweredAt = a.StartedAt.AddSeconds(limit);
        a.Correct = false;
        a.Points = QuestionEngine.PointsWhenStopped(q, a);
        return Ending.Expired;
    }
}

static double Know(Profile p, int difficulty) => Math.Clamp(p.Skill + (2 - difficulty) * 0.12, 0.05, 0.97);

string WrongName(string answer)
{
    while (true)
    {
        var name = allPlayers[rng.Next(allPlayers.Count)].Name;
        if (!AnswerMatcher.IsMatch(name, answer, [])) return name;
    }
}

// Rond het gewone uur van de speler; een nachtuil speelt soms na middernacht.
int SessionMinutes(Profile p)
{
    if (p.Minutes >= 23 * 60 && rng.NextDouble() < 0.3) return rng.Next(10, 95);
    var jitter = (int)((rng.NextDouble() + rng.NextDouble() - 1) * 80);
    return Math.Clamp(p.Minutes + jitter, 5 * 60 + 5, 23 * 60 + 58);
}

int Poisson(double mean)
{
    int k = 0;
    for (var p = rng.NextDouble(); p > Math.Exp(-mean); p *= rng.NextDouble()) k++;
    return k;
}

string MatchName()
{
    string[] names =
    [
        "Avondje quizzen", "Revanche!", "Wie durft?", "Middagpauze", "Derby van de bureau", "Kopstoot", "Laatste man staat",
        "Strijd om de beker", "Snel een potje", "Penaltyreeks", "Zaterdagavondvoetbal", "De kleedkamer", "Na de training",
    ];
    return names[rng.Next(names.Length)];
}

static DateTime Local(DateOnly day, int minutes) =>
    TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue).AddMinutes(minutes), TimeZoneInfo.FindSystemTimeZoneById("Europe/Brussels"));

static string FindRepoRoot()
{
    for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
        if (File.Exists(Path.Combine(dir.FullName, "VoetbalQuiz.slnx"))) return dir.FullName;
    throw new InvalidOperationException("Start dit vanuit de repository (VoetbalQuiz.slnx niet gevonden).");
}

record Profile(string Name, string Avatar, double Skill, double Activity, int Minutes, int JoinDay, double Wrongness);

enum Ending { Open, Done, GaveUp, Expired }

/// <summary>Een klok die we kunnen verzetten, zodat de app denkt dat het die dag is. Null = de echte tijd.</summary>
class SimClock : TimeProvider
{
    public DateTime? At { get; set; }
    public override DateTimeOffset GetUtcNow() => At is { } at ? new DateTimeOffset(at, TimeSpan.Zero) : base.GetUtcNow();
}
