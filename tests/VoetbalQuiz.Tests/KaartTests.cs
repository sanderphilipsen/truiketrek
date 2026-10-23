using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Tests;

/// <summary>
/// De kaartdag: één kaart in twee delen van elk 500 punten. Deel 1: per speld de club(s) kiezen. Deel 2: bij de club(s)
/// het stadion kiezen. De spelden komen één voor één.
/// </summary>
public sealed class KaartTests : IDisposable
{
    private readonly TestDb _t = new();
    private readonly QuizDbContext _db;
    private readonly FakeTimeProvider _clock;
    private readonly QuizService _quiz;
    private readonly string _me;
    private readonly string _other;

    // Vijf spelden, waarvan één gedeeld stadion: zes clubs in totaal.
    private static readonly KaartData Map = new("Eerste klasse: welke club speelt hier thuis?", "Testcompetitie", "2026-27",
    [
        new("Jan Breydelstadion", "Brugge", 51.1931, 3.1806, ["Club Brugge", "Cercle Brugge"]),
        new("Lotto Park", "Anderlecht", 50.8342, 4.2983, ["Anderlecht"]),
        new("Bosuilstadion", "Antwerpen", 51.2325, 4.4722, ["Antwerp"]),
        new("Cegeka Arena", "Genk", 51.0051, 5.5333, ["Genk"]),
        new("Stade Maurice Dufrasne", "Luik", 50.6101, 5.5434, ["Standard"]),
    ]);

    private const int TotalClubs = 6, TotalPins = 5;

    public KaartTests()
    {
        (_db, _clock) = (_t.Db, _t.Clock);

        for (var i = 1; i <= 15; i++)
            _db.Players.Add(new Player { Name = $"Speler {i}", Status = PlayerStatus.Approved, Hints = [.. Enumerable.Range(1, 5).Select(h => $"Tip {h}")] });
        for (var i = 0; i < 12; i++)
            Add(QuestionType.Snelvuur, $"V{i}", new SnelvuurData($"Vraag {i}?", ["A", "B", "C", "D"], 2));
        Add(QuestionType.Kaart, "Testcompetitie|2026-27", Map);

        _me = _t.AddUser("sander");
        _other = _t.AddUser("jan");
        _db.SaveChanges();
        _quiz = _t.Quiz;
    }

    private void Add<T>(QuestionType type, string key, T data) =>
        _db.QuizItems.Add(new QuizItem { Type = type, Key = key, Data = QuestionEngine.Serialize(data), Status = PlayerStatus.Approved });

    private QuizItem MapItem => _db.QuizItems.Single(i => i.Type == QuestionType.Kaart);

    /// <summary>17 oktober 2026 is een zaterdag: vandaag een kaartdag.</summary>
    private Task PlanKaartAsync() => _t.Admin.SaveScheduleAsync([new ScheduleDay(DayOfWeek.Saturday, QuestionType.Kaart)]);

    private static KaartSpeld Current(QuestionState s) => s.View!.Pins!.Single(p => p.IsCurrent);

    private static bool Stadiums(QuestionState s) => s.View!.Part == QuestionRules.KaartStadiumsPart;

    /// <summary>Het stadion in de data achter een speld op het scherm (de coördinaten zijn uniek).</summary>
    private static KaartStadion Stadium(KaartSpeld pin, KaartData? map = null) =>
        (map ?? Map).Pins.Single(p => p.Lat == pin.Lat && p.Lon == pin.Lon);

    /// <summary>De gok voor de huidige speld, met haar nummer erbij, zoals de app hem stuurt.</summary>
    private static string Guess(QuestionState s, string answer) => QuestionRules.KaartGuess(s.View!.Found + 1, answer);

    /// <summary>Het juiste antwoord op de huidige speld: de club(s) in deel 1, het stadion in deel 2.</summary>
    private static string Right(QuestionState s, KaartData? map = null)
    {
        var pin = Stadium(Current(s), map);
        return Guess(s, Stadiums(s) ? pin.Stadium : string.Join(QuestionRules.KaartSeparator, pin.Clubs));
    }

    /// <summary>Een antwoord dat zeker fout is.</summary>
    private static string Wrong(QuestionState s, KaartData? map = null)
    {
        var pin = Stadium(Current(s), map);
        var other = (map ?? Map).Pins.First(p => p != pin);
        return Guess(s, Stadiums(s) ? other.Stadium : other.Clubs[0]);
    }

    private async Task<QuestionState> PlayAsync(int number, Func<QuestionState, string> answer, int? specialId = null)
    {
        var state = await _quiz.StartAsync(_me, number, specialId);
        while (state.Result is null) state = await _quiz.GuessAsync(_me, number, answer(state), specialId);
        return state;
    }

    // ---------- Regels ----------

    [Theory]
    [InlineData(6, 6, 500)]
    [InlineData(5, 6, 416)]  // naar beneden afgerond
    [InlineData(17, 18, 472)] // de echte eerste klasse: 18 clubs
    [InlineData(1, 3, 166)]
    [InlineData(0, 6, 0)]
    [InlineData(0, 0, 0)]
    public void Points_are_five_hundred_times_the_share_right_rounded_down(int right, int total, int points) =>
        Assert.Equal(points, QuestionRules.KaartPoints(right, total));

    [Fact]
    public void Kaart_is_two_parts_of_five_hundred_points()
    {
        var info = QuestionTypes.Get(QuestionType.Kaart);
        Assert.Equal((2, 500, "kaart"), (info.PerDay, info.MaxPoints, info.FileName));
        Assert.Equal(QuestionTypes.DayMaxPoints, info.PerDay * info.MaxPoints);
        Assert.Equal(180, QuestionRules.KaartSeconds);
        // De uitleg volgt de regels (geen vaste "2 minuten" meer).
        Assert.Contains($"{(int)QuestionRules.KaartSeconds / 60} minuten", info.HowTo);
        Assert.Contains($"{QuestionRules.KaartPartPoints} punten", info.HowTo);
        Assert.Equal(QuestionType.Kaart, Enum.GetValues<QuestionType>()[^1]); // achteraan in de enum
        Assert.True(QuestionRules.KaartMaxPins < GameRules.MaxGuessesPerQuestion);
    }

    // ---------- Twee delen ----------

    [Fact]
    public async Task A_kaartdag_is_two_questions_on_the_same_map()
    {
        await PlanKaartAsync();
        var challenge = await _quiz.GetOrCreateChallengeAsync(_quiz.Today);
        Assert.Equal(2, challenge.Questions.Count);
        Assert.All(challenge.Questions, q => Assert.Equal((QuestionType.Kaart, MapItem.Id), (q.Type, q.QuizItemId)));
        Assert.Equal([1, 2], challenge.Questions.OrderBy(q => q.Number).Select(q => q.Part));
        Assert.Equal(1000, (await _quiz.GetOverviewAsync(_me)).MaxPoints);

        var part1 = await PlayAsync(1, s => Right(s));
        Assert.True(part1.Result!.Correct);
        Assert.Equal(500, part1.Result.Points);

        var start2 = await _quiz.StartAsync(_me, 2);
        Assert.Equal(QuestionRules.KaartStadiumsPart, start2.View!.Part);
        Assert.Equal(QuestionRules.KaartSeconds, start2.TimeLimit);
        Assert.Equal(Map.Pins.Select(p => p.Stadium).Order(StringComparer.InvariantCultureIgnoreCase), start2.View.Options);
        var part2 = await PlayAsync(2, s => Right(s));
        Assert.True(part2.Result!.Correct);
        Assert.Equal(500, part2.Result.Points);
        Assert.Equal(1000, (await _quiz.GetOverviewAsync(_me)).TotalPoints);
    }

    [Fact]
    public async Task Part_one_asks_the_clubs_one_pin_at_a_time()
    {
        await PlanKaartAsync();
        var state = await _quiz.StartAsync(_me, 1);
        Assert.Equal(QuestionRules.KaartClubsPart, state.View!.Part);
        Assert.Equal(TotalPins, state.View.Total);
        Assert.Single(state.View.Pins!);
        Assert.Equal(["Anderlecht", "Antwerp", "Cercle Brugge", "Club Brugge", "Genk", "Standard"], state.View.Options);

        for (var n = 1; n <= TotalPins; n++)
        {
            Assert.Equal($"Deel 1 · speld {n} van {TotalPins}", state.View!.Note);
            state = await _quiz.GuessAsync(_me, 1, Right(state));
            if (n == TotalPins) break;
            Assert.Equal(n + 1, state.View!.Pins!.Count);
            var answered = state.View.Pins[n - 1];
            Assert.True(answered.IsRight);
            Assert.Equal(Stadium(answered).Clubs, answered.Answer);
        }
        Assert.Equal(500, state.Result!.Points);
        Assert.All(state.Result.AllHints, h => Assert.StartsWith("✅", h));
    }

    [Fact]
    public async Task Part_one_never_names_a_stadium_and_never_shows_the_next_pins()
    {
        await PlanKaartAsync();
        var state = await _quiz.StartAsync(_me, 1);
        while (state.Result is null)
        {
            var current = Current(state);
            Assert.Null(current.Answer);
            Assert.Null(current.Clubs);
            var json = JsonSerializer.Serialize(state);
            // Geen coördinaten van een speld die nog moet komen.
            foreach (var pin in Map.Pins.Except(state.View!.Pins!.Select(p => Stadium(p))))
                Assert.DoesNotContain(pin.Lat.ToString(CultureInfo.InvariantCulture), json);
            // Deel 1 noemt nooit een stadion: dat is de vraag van deel 2.
            Assert.All(Map.Pins, p => Assert.DoesNotContain(p.Stadium, json));
            state = await _quiz.GuessAsync(_me, 1, Wrong(state));
        }
        // Ook het resultaat van deel 1 niet.
        Assert.All(Map.Pins, p => Assert.DoesNotContain(p.Stadium, JsonSerializer.Serialize(state)));
    }

    [Fact]
    public async Task Part_two_shows_the_clubs_but_never_which_stadium_or_the_next_pins()
    {
        await PlanKaartAsync();
        await PlayAsync(1, s => Right(s));
        var state = await _quiz.StartAsync(_me, 2);
        while (state.Result is null)
        {
            var current = Current(state);
            Assert.Equal(1, current.Choices);
            Assert.Equal(Stadium(current).Clubs, current.Clubs); // de club staat bij de speld: dat is de vraag
            Assert.Null(current.Answer);
            Assert.Equal(state.View!.Found + 1, state.View.Pins!.Count);
            var json = JsonSerializer.Serialize(state);
            foreach (var pin in Map.Pins.Except(state.View.Pins.Select(p => Stadium(p))))
                Assert.DoesNotContain(pin.Lat.ToString(CultureInfo.InvariantCulture), json);
            state = await _quiz.GuessAsync(_me, 2, Right(state));
            if (state.Result is null) Assert.Equal([Stadium(current).Stadium], state.View!.Pins!.Single(p => p.Lat == current.Lat).Answer);
        }
    }

    [Fact]
    public async Task Everyone_gets_the_same_order_per_part_and_part_two_has_its_own()
    {
        await PlanKaartAsync();
        var mine = await _quiz.StartAsync(_me, 1);
        var theirs = await _quiz.StartAsync(_other, 1);
        Assert.Equal(Current(mine), Current(theirs));
        Assert.Equal(Current(mine), Current(await _quiz.GetStateAsync(_me, 1)));

        var questions = (await _quiz.GetOrCreateChallengeAsync(_quiz.Today)).Questions.OrderBy(q => q.Number).ToList();
        Assert.Equal(QuestionEngine.KaartOrder(questions[0]), QuestionEngine.KaartOrder(questions[0]));
        Assert.NotEqual(QuestionEngine.KaartOrder(questions[0]), QuestionEngine.KaartOrder(questions[1]));
        Assert.All(questions, q => Assert.Equal(Enumerable.Range(0, TotalPins), QuestionEngine.KaartOrder(q).Order()));
    }

    [Fact]
    public async Task A_shared_stadium_counts_every_right_club_in_part_one_and_is_one_stadium_in_part_two()
    {
        await PlanKaartAsync();
        var state = await PlayAsync(1, s =>
        {
            var clubs = Stadium(Current(s)).Clubs;
            // Eén van de twee juist (en dezelfde club twee keer kiezen telt maar één keer).
            return clubs.Count > 1 ? Guess(s, $"{clubs[0]}|{clubs[0]}") : Right(s);
        });
        Assert.False(state.Result!.Correct);
        Assert.Equal(QuestionRules.KaartPoints(TotalClubs - 1, TotalClubs), state.Result.Points);
        var shared = state.View!.Pins!.Single(p => p.Choices == 2);
        Assert.Equal(1, shared.Hits);
        Assert.Single(state.Result.AllHints, h => h.StartsWith("🟡") && h.Contains("jij: Club Brugge en Club Brugge"));

        var part2 = await _quiz.StartAsync(_me, 2);
        Assert.Equal(TotalPins, part2.View!.Total);
        Assert.All(part2.View.Pins!, p => Assert.Equal(1, p.Choices));
    }

    // ---------- Pogingen ----------

    [Fact]
    public async Task Each_guess_carries_the_pin_number_and_a_double_tap_counts_once()
    {
        await PlanKaartAsync();
        var start = await _quiz.StartAsync(_me, 1);
        var guess = Right(start);

        var once = await _quiz.GuessAsync(_me, 1, guess);
        var twice = await _quiz.GuessAsync(_me, 1, guess); // dezelfde gok (speld 1) nog eens: de huidige is nu speld 2
        Assert.Equal(1, once.View!.Found);
        Assert.Equal(1, twice.View!.Found);
        Assert.Equal(Current(once), Current(twice));

        // Een nummer vooruit, zonder nummer, of een ander nummer: niets.
        foreach (var bad in new[] { QuestionRules.KaartGuess(3, "Genk"), "Genk", "x:Genk", QuestionRules.KaartSkip })
            Assert.Equal(1, (await _quiz.GuessAsync(_me, 1, bad)).View!.Found);
        Assert.Equal(1, _db.Attempts.Single(a => a.UserId == _me).Guesses);
    }

    [Fact]
    public async Task Invalid_answers_change_nothing_and_do_not_count_as_attempts()
    {
        await PlanKaartAsync();
        var start = await _quiz.StartAsync(_me, 1);
        var first = Current(start);

        // Onbekende club, te veel clubs, leeg: niets gebeurt, dezelfde speld blijft, en het is geen poging.
        var tooMany = string.Join(QuestionRules.KaartSeparator, Enumerable.Repeat("Genk", first.Choices + 1));
        for (var i = 0; i < GameRules.MaxGuessesPerQuestion + 5; i++)
            foreach (var bad in new[] { "Real Madrid", tooMany, "|" })
            {
                var same = await _quiz.GuessAsync(_me, 1, Guess(start, bad));
                Assert.Equal(QuestionStatus.InProgress, same.Status);
                Assert.Equal(0, same.View!.Found);
            }
        Assert.Equal(0, _db.Attempts.Single(a => a.UserId == _me).Guesses);

        var skipped = await _quiz.GuessAsync(_me, 1, Guess(start, QuestionRules.KaartSkip));
        Assert.Equal(0, skipped.View!.Pins![0].Hits);
        Assert.Equal("", skipped.View.Pins[0].Pick);
        Assert.NotEqual(first, Current(skipped));

        while (skipped.Result is null) skipped = await _quiz.GuessAsync(_me, 1, Right(skipped));
        Assert.Equal(QuestionRules.KaartPoints(TotalClubs - Stadium(first).Clubs.Count, TotalClubs), skipped.Result.Points);
        Assert.StartsWith("❌", skipped.Result.AllHints[0]);
        Assert.EndsWith("(overgeslagen)", skipped.Result.AllHints[0]);
    }

    [Fact]
    public async Task A_wrong_answer_shows_what_you_chose()
    {
        await PlanKaartAsync();
        var state = await _quiz.StartAsync(_me, 1);
        var wrong = Wrong(state);
        state = await _quiz.GuessAsync(_me, 1, wrong);
        var answered = state.View!.Pins![0];
        Assert.Equal(wrong.Split(QuestionRules.KaartPinPrefix)[1], answered.Pick);
        while (state.Result is null) state = await _quiz.GuessAsync(_me, 1, Right(state));
        Assert.Contains($"(jij: {answered.Pick})", state.Result.AllHints[0]);
        Assert.Equal(TotalPins, state.Result.MyPicks!.Count);
    }

    // ---------- Tijd ----------

    [Fact]
    public async Task The_server_stops_a_part_after_its_time_and_the_rest_is_wrong()
    {
        await PlanKaartAsync();
        var state = await _quiz.StartAsync(_me, 1);
        state = await _quiz.GuessAsync(_me, 1, Right(state));
        var rightClubs = state.View!.Pins![0].Hits!.Value;

        // Binnen de speling telt een antwoord nog.
        _clock.Advance(TimeSpan.FromSeconds(QuestionRules.KaartSeconds + GameRules.LatencyGraceSeconds / 2));
        state = await _quiz.GuessAsync(_me, 1, Right(state));
        rightClubs += state.View!.Pins![1].Hits!.Value;
        Assert.Null(state.Result);

        // Daarna niet meer: het deel sluit met de punten van wat al juist stond.
        _clock.Advance(TimeSpan.FromSeconds(GameRules.LatencyGraceSeconds));
        var late = await _quiz.GuessAsync(_me, 1, Right(state));
        Assert.True(late.Result!.TimedOut);
        Assert.Equal(QuestionRules.KaartPoints(rightClubs, TotalClubs), late.Result.Points);
        Assert.Equal(3, late.Result.AllHints.Count(h => h.StartsWith("⌛")));
        Assert.Equal(TotalPins, late.View!.Pins!.Count);
        Assert.Equal(3, late.View.Pins.Count(p => p.Hits == 0));

        // Deel 2 heeft een eigen klok: die start pas als de speler eraan begint.
        _clock.Advance(TimeSpan.FromMinutes(10));
        var part2 = await _quiz.StartAsync(_me, 2);
        Assert.True(part2.ElapsedSeconds < 1);
        Assert.Null(part2.Result);
    }

    [Fact]
    public async Task Stopping_keeps_the_points_already_earned()
    {
        await PlanKaartAsync();
        var state = await _quiz.StartAsync(_me, 1);
        state = await _quiz.GuessAsync(_me, 1, Right(state));
        var hits = state.View!.Pins![0].Hits!.Value;
        var stopped = await _quiz.GiveUpAsync(_me, 1);
        Assert.Equal(QuestionRules.KaartPoints(hits, TotalClubs), stopped.Result!.Points);
    }

    [Fact]
    public async Task Eighteen_clubs_like_the_real_first_division()
    {
        // 17 spelden, één gedeeld stadion: 18 clubs, zoals de echte eerste klasse.
        var big = new KaartData("Eerste klasse?", "Groot", "2026-27",
            [.. Enumerable.Range(0, 17).Select(i => new KaartStadion($"Stadion {i}", $"Stad {i}", 50.2 + i * 0.07, 3.0 + i * 0.15,
                i == 0 ? ["Club 0", "Club 0b"] : [$"Club {i}"]))]);
        Assert.Empty(QuestionEngine.Problems(QuestionType.Kaart, QuestionEngine.Serialize(big)));
        MapItem.Status = PlayerStatus.Rejected;
        Add(QuestionType.Kaart, "Groot|2026-27", big);
        _db.SaveChanges();
        await PlanKaartAsync();

        var missed = false;
        var state = await PlayAsync(1, s =>
        {
            if (missed || Current(s).Choices > 1) return Right(s, big);
            missed = true;
            return Wrong(s, big);
        });
        Assert.Equal(472, state.Result!.Points); // floor(500 × 17 / 18)
        Assert.Equal("17 van de 18 clubs juist geplaatst", state.Result.Explanation);
        Assert.Contains(Commentary.ForResult(state.Result, 500, 1), Commentary.Almost);
    }

    [Fact]
    public void Partial_scores_get_commentary_that_fits_the_share()
    {
        Assert.Contains(Commentary.ForResult(false, QuestionRules.KaartPoints(3, 18), 500, 1), Commentary.Partial);
        Assert.Contains(Commentary.ForResult(false, QuestionRules.KaartPoints(17, 18), 500, 1), Commentary.Almost);
    }

    [Fact]
    public async Task All_wrong_is_a_miss_not_a_pass()
    {
        await PlanKaartAsync();
        var state = await PlayAsync(1, s => Wrong(s));
        Assert.Equal(0, state.Result!.Points);
        Assert.Contains(Commentary.ForResult(state.Result, 500, 1), Commentary.Missed);

        // Zonder één antwoord stoppen is wel passen.
        await _quiz.StartAsync(_me, 2);
        var stopped = await _quiz.GiveUpAsync(_me, 2);
        Assert.Equal("Gepast.", Commentary.ForResult(stopped.Result!, 500, 1));
    }

    // ---------- De kaart ligt vast zodra ze gespeeld wordt ----------

    [Fact]
    public async Task A_played_map_keeps_its_pins_but_its_metadata_can_change()
    {
        await PlanKaartAsync();
        await _quiz.GetOrCreateChallengeAsync(_quiz.Today);
        var item = MapItem;
        var edit = new QuizItemEdit(item.Id, QuestionType.Kaart, item.Data, 4, ["https://example.org"], [], PlayerStatus.Approved, null, null, null, 0);

        var fewer = QuestionEngine.Serialize(Map with { Pins = Map.Pins.Skip(1).ToList() });
        await Assert.ThrowsAsync<QuizException>(() => _t.Admin.SaveQuizItemAsync(edit with { Data = fewer }, "sander"));

        await _t.Admin.SaveQuizItemAsync(edit, "sander"); // zelfde spelden, andere moeilijkheid: mag
        Assert.Equal(4, MapItem.Difficulty);
    }

    [Fact]
    public async Task The_seeder_does_not_overwrite_the_pins_of_a_played_map()
    {
        await PlanKaartAsync();
        await _quiz.GetOrCreateChallengeAsync(_quiz.Today);
        var before = MapItem.Data;

        var root = Directory.CreateTempSubdirectory("kaart-seed-").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "Data", "questions"));
            var changed = Map with { Pins = Map.Pins.Skip(1).ToList() };
            var json = JsonSerializer.Serialize(new[]
            {
                new { changed.Question, changed.Competition, changed.Season, changed.Pins, difficulty = 4, status = "approved", sources = new[] { "https://example.org" } },
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            await File.WriteAllTextAsync(Path.Combine(root, "Data", "questions", "kaart.json"), json);

            await new QuestionSeeder(_db, new Env(root), NullLogger<QuestionSeeder>.Instance).SeedAsync();
            Assert.Equal(before, MapItem.Data);       // de spelden liggen vast
            Assert.Equal(4, MapItem.Difficulty);      // de rest wordt wel bijgewerkt
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class Env(string root) : IWebHostEnvironment
    {
        public string ContentRootPath { get; set; } = root;
        public string WebRootPath { get; set; } = root;
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "VoetbalQuiz.Server";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }

    // ---------- Enkel goedgekeurde kaarten ----------

    [Fact]
    public async Task Only_approved_maps_are_played()
    {
        MapItem.Status = PlayerStatus.Review;
        _db.SaveChanges();

        await PlanKaartAsync();
        var daily = await _quiz.GetOrCreateChallengeAsync(_quiz.Today);
        Assert.DoesNotContain(daily.Questions, q => q.Type == QuestionType.Kaart); // terugval op "Wie ben ik?"

        var special = new SpecialEdit(0, "eigen", "Kaartspecial", "🗺️", "", _quiz.Today, _quiz.Today, null, [], [QuestionType.Kaart]);
        var error = await Assert.ThrowsAsync<QuizException>(() => _t.Admin.SaveSpecialAsync(special));
        Assert.Contains("Kaart", error.Message);
    }

    // ---------- De solo-regel ----------

    [Fact]
    public void Kaart_is_a_solo_type_that_never_joins_a_mix()
    {
        // Kaart volgt dezelfde regel als De Krant (QuestionTypeInfo.Alone): alleen de hele dag, in een mix valt ze weg.
        Assert.True(QuestionTypes.Get(QuestionType.Kaart).Alone);
        Assert.Equal(new Dictionary<QuestionType, int> { [QuestionType.Kaart] = 2 }, QuestionTypes.Mix([QuestionType.Kaart]));
        Assert.Equal(new Dictionary<QuestionType, int> { [QuestionType.WieBenIk] = 10 }, QuestionTypes.Mix([QuestionType.WieBenIk, QuestionType.Kaart]));
        Assert.DoesNotContain(QuestionType.Kaart, QuestionTypes.Mix([QuestionType.Snelvuur, QuestionType.Kaart], 7).Keys);
        Assert.Equal([QuestionType.Snelvuur], QuestionTypes.ResolveDay([QuestionType.Snelvuur, QuestionType.Kaart], []));
        Assert.Equal([QuestionType.Snelvuur], QuestionTypes.ResolveSpecial([QuestionType.Snelvuur, QuestionType.Kaart], []));
        // Staat de kaart uit, dan blijven de andere types; een dag met enkel de kaart wordt een mix zonder kaart.
        Assert.Equal([QuestionType.Snelvuur], QuestionTypes.ResolveDay([QuestionType.Snelvuur, QuestionType.Kaart], [QuestionType.Kaart]));
        Assert.DoesNotContain(QuestionType.Kaart, QuestionTypes.ResolveDay([QuestionType.Kaart], [QuestionType.Kaart]));
        // Ook de terugval "mix van alles wat aan staat" neemt de kaart niet op (anders werd die mix enkel de kaart).
        Assert.DoesNotContain(QuestionType.Kaart, QuestionTypes.ResolveDay([QuestionType.Snelvuur], [QuestionType.Snelvuur]));
        // Een special met enkel een solo-type terwijl dat uit staat: "Wie ben ik?", nooit toch de kaart of De Krant.
        Assert.Equal([QuestionType.WieBenIk], QuestionTypes.ResolveSpecial([QuestionType.Kaart], [QuestionType.Kaart]));
        Assert.Equal([QuestionType.WieBenIk], QuestionTypes.ResolveSpecial([QuestionType.Krant], [QuestionType.Krant]));
        // Andere types blijven bij die terugval wel "toch de gekozen types".
        Assert.Equal([QuestionType.Snelvuur], QuestionTypes.ResolveSpecial([QuestionType.Snelvuur], [QuestionType.Snelvuur]));

        Assert.NotNull(QuestionTypes.AloneProblem([QuestionType.Kaart, QuestionType.Snelvuur]));
        Assert.NotNull(QuestionTypes.AloneProblem([QuestionType.Krant, QuestionType.Snelvuur]));
        Assert.NotNull(QuestionTypes.AloneProblem([QuestionType.Krant, QuestionType.Kaart]));
        Assert.Null(QuestionTypes.AloneProblem([QuestionType.Kaart]));
        Assert.Null(QuestionTypes.AloneProblem([QuestionType.Snelvuur, QuestionType.Jaar]));
    }

    [Fact]
    public async Task Admin_refuses_kaart_in_a_mix_or_in_a_special_with_other_types()
    {
        await Assert.ThrowsAsync<QuizException>(() => _t.Admin.SaveScheduleAsync(
            [new ScheduleDay(DayOfWeek.Saturday, QuestionType.Kaart, [QuestionType.Kaart, QuestionType.Snelvuur])]));

        var special = new SpecialEdit(0, "eigen", "Kaartspecial", "🗺️", "", _quiz.Today, _quiz.Today, null, [],
            [QuestionType.Kaart, QuestionType.Snelvuur]);
        await Assert.ThrowsAsync<QuizException>(() => _t.Admin.SaveSpecialAsync(special));

        var id = await _t.Admin.SaveSpecialAsync(special with { Types = [QuestionType.Kaart] });
        var challenge = await _quiz.GetOrCreateSpecialChallengeAsync(id);
        Assert.Equal([1, 2], challenge.Questions.Where(q => q.Type == QuestionType.Kaart).Select(q => q.Part).Order());
        var state = await _quiz.StartAsync(_me, 1, id);
        Assert.Equal(TotalPins, state.View!.Total);
    }

    [Fact]
    public async Task A_kaart_special_falls_back_to_who_am_i_when_kaart_is_switched_off()
    {
        var players = _db.Players.Select(p => p.Id).ToList();
        var id = await _t.Admin.SaveSpecialAsync(new SpecialEdit(0, "eigen", "Kaartspecial", "🗺️", "", _quiz.Today, _quiz.Today,
            null, players, [QuestionType.Kaart]));
        await _t.Admin.SetTypeEnabledAsync(QuestionType.Kaart, false);

        var challenge = await _quiz.GetOrCreateSpecialChallengeAsync(id);
        Assert.All(challenge.Questions, q => Assert.Equal(QuestionType.WieBenIk, q.Type));
    }

    [Fact]
    public async Task At_least_one_type_besides_kaart_stays_on()
    {
        foreach (var t in QuestionTypes.All.Select(i => i.Type).Where(t => t is not QuestionType.WieBenIk and not QuestionType.Kaart))
            await _t.Admin.SetTypeEnabledAsync(t, false);
        // De kaart staat nog aan, maar is geen terugval: "Wie ben ik?" kan dus niet uit.
        await Assert.ThrowsAsync<QuizException>(() => _t.Admin.SetTypeEnabledAsync(QuestionType.WieBenIk, false));
        await _t.Admin.SetTypeEnabledAsync(QuestionType.Kaart, false); // de kaart zelf mag wel uit
    }

    [Fact]
    public async Task A_missed_kaartdag_can_be_replayed_in_the_academy_with_the_same_pins()
    {
        await PlanKaartAsync();
        var day = _quiz.Today;
        var real = await _quiz.StartAsync(_other, 1);
        _clock.Advance(TimeSpan.FromDays(1));

        var run = await _t.Training.StartRunAsync(_me, day);
        var total = 0;
        for (var n = 1; n <= QuestionRules.KaartParts; n++)
        {
            var state = await _t.Training.StartAsync(_me, run.Id, n);
            if (n == 1) Assert.Equal(Current(real), Current(state));
            while (state.Result is null) state = await _t.Training.GuessAsync(_me, run.Id, n, Right(state));
            total += state.Result.Points;
        }
        Assert.Equal(1000, total);
    }

    [Fact]
    public async Task Kaart_does_not_count_for_the_allrounder_badge()
    {
        await PlanKaartAsync();
        await PlayAsync(1, s => Right(s));
        var profile = await _t.Progress.GetProfileAsync(_me, "sander");
        var mixable = QuestionTypes.All.Count(t => !t.Alone);
        Assert.Contains(profile.Badges, b => b.Id == "allrounder" && b.Progress == $"0/{mixable}");
    }

    [Fact]
    public void Every_solo_type_counts_as_a_full_day_of_questions()
    {
        // De Krant (1 vraag) en de kaart (2 vragen) tellen elk samen als een volle dag; een gewone vraag telt als één.
        Assert.Equal(GameRules.QuestionsPerDay, QuestionTypes.PlayedWeight(QuestionType.Krant) * QuestionTypes.Get(QuestionType.Krant).PerDay);
        Assert.Equal(GameRules.QuestionsPerDay, QuestionTypes.PlayedWeight(QuestionType.Kaart) * QuestionTypes.Get(QuestionType.Kaart).PerDay);
        Assert.Equal(1, QuestionTypes.PlayedWeight(QuestionType.Snelvuur));
        Assert.All(QuestionTypes.All.Where(t => t.Alone), t => Assert.True(QuestionRules.PartialScores(t.Type)));
        Assert.True(QuestionRules.PartialScores(QuestionType.Opstelling) && QuestionRules.PartialScores(QuestionType.Counter));
    }

    // ---------- Niet online ----------

    [Fact]
    public async Task Kaart_cannot_be_played_online()
    {
        Assert.DoesNotContain(QuestionType.Kaart, await _t.Picker.ActiveTypesAsync());

        var matches = _t.Matches();
        await Assert.ThrowsAsync<QuizException>(() => matches.CreateAsync(_me, "sander", new CreateMatchRequest(null, 4, [QuestionType.Kaart])));
        await Assert.ThrowsAsync<QuizException>(() =>
            matches.CreateAsync(_me, "sander", new CreateMatchRequest(null, 4, [QuestionType.Snelvuur, QuestionType.Kaart])));
    }

    // ---------- Inhoud ----------

    [Fact]
    public void Kaart_checks_its_content()
    {
        static List<string> Problems(KaartData d) => QuestionEngine.Problems(QuestionType.Kaart, QuestionEngine.Serialize(d));

        Assert.Empty(Problems(Map));
        Assert.NotEmpty(Problems(Map with { Pins = Map.Pins.Take(QuestionRules.KaartMinPins - 1).ToList() }));    // te weinig spelden
        Assert.NotEmpty(Problems(Map with                                                                           // te veel spelden
        {
            Pins = [.. Enumerable.Range(0, QuestionRules.KaartMaxPins + 1).Select(i => new KaartStadion($"S{i}", "Gent", 51.0, 3.0 + i * 0.05, [$"C{i}"]))],
        }));
        Assert.NotEmpty(Problems(Map with { Pins = [.. Map.Pins, new("Elders", "Parijs", 48.85, 2.35, ["PSG"])] })); // buiten de kaart
        Assert.NotEmpty(Problems(Map with { Pins = [.. Map.Pins, new("Noord", "Breda", 51.59, 4.78, ["NAC"])] }));   // net boven de kaart
        Assert.NotEmpty(Problems(Map with { Pins = [.. Map.Pins, new("Tweede", "Gent", 51.01, 3.73, ["Genk"])] }));  // club bij twee spelden
        Assert.NotEmpty(Problems(Map with { Pins = [.. Map.Pins, new("Lotto Park", "Gent", 51.01, 3.73, ["Gent"])] })); // stadionnaam dubbel
        Assert.NotEmpty(Problems(Map with { Pins = [.. Map.Pins, new("Leeg", "Gent", 51.01, 3.73, [])] }));         // speld zonder club
        Assert.NotEmpty(Problems(Map with { Competition = "" }));

        Assert.Equal("Testcompetitie|2026-27", QuestionEngine.KeyFor(QuestionType.Kaart, QuestionEngine.Serialize(Map)));
    }

    [Fact]
    public void The_map_and_the_content_check_use_the_same_edges()
    {
        Assert.Equal((0, 0), KaartProjection.Project(KaartProjection.MaxLat, KaartProjection.MinLon));
        var (x, y) = KaartProjection.Project(KaartProjection.MinLat, KaartProjection.MaxLon);
        Assert.Equal(KaartProjection.Width, x, 6);
        Assert.Equal(KaartProjection.Height, y, 6);
        Assert.True(KaartProjection.Contains(KaartProjection.MaxLat, KaartProjection.MinLon));
        Assert.False(KaartProjection.Contains(KaartProjection.MaxLat + 0.01, 4));
    }

    // ---------- Laatste ronde: XP, tijd, namen, gelijktijdige gokken ----------

    [Fact]
    public async Task Partial_points_of_a_kaartdag_count_for_xp_like_points_on_a_normal_day()
    {
        await PlanKaartAsync();
        await PlayAsync(1, s => Right(s));                                 // 500, juist
        var missed = false;
        var part2 = await PlayAsync(2, s => { if (missed) return Right(s); missed = true; return Wrong(s); });
        Assert.False(part2.Result!.Correct);
        Assert.Equal(400, part2.Result.Points);                             // 4 van de 5 stadions

        // 900 punten, waarvan 400 uit een deel dat niet volledig juist is: alles telt voor "Juiste antwoorden",
        // net als 900 punten uit juiste antwoorden op een gewone dag (900 / 5 = 180 XP).
        var input = (await _t.Results.XpInputsAsync())[_me];
        Assert.Equal(900, input.CorrectPoints);
        Assert.Equal(900 / Levels.CorrectPointsPerXp, Levels.Breakdown(input).Single(p => p.Label == "Juiste antwoorden").Xp);
        // "Gespeelde vragen": de twee delen tellen samen als een volle dag, net als een gewone dag van 10 vragen.
        Assert.Equal(GameRules.QuestionsPerDay, input.QuestionsPlayed);
        Assert.Equal(GameRules.QuestionsPerDay * Levels.XpPerQuestion, Levels.Breakdown(input).Single(p => p.Label == "Gespeelde vragen").Xp);
    }

    [Fact]
    public async Task Partial_points_of_a_lineup_and_a_counter_count_for_xp_but_a_close_year_does_not()
    {
        var day = new DailyChallenge
        {
            Date = _quiz.Today,
            Questions =
            [
                new DailyQuestion { Number = 1, Type = QuestionType.Opstelling },
                new DailyQuestion { Number = 2, Type = QuestionType.Counter },
                new DailyQuestion { Number = 3, Type = QuestionType.Jaar },
            ],
        };
        _db.DailyChallenges.Add(day);
        var at = _clock.GetUtcNow().UtcDateTime;
        foreach (var (q, points) in day.Questions.Zip(new[] { 133, 60, 75 }))
            _db.Attempts.Add(new Attempt { UserId = _other, DailyQuestion = q, StartedAt = at, AnsweredAt = at, Points = points, Correct = false });
        _db.SaveChanges();

        Assert.True(QuestionRules.PartialScores(QuestionType.Opstelling) && QuestionRules.PartialScores(QuestionType.Counter));
        Assert.False(QuestionRules.PartialScores(QuestionType.Jaar));
        Assert.Equal(133 + 60, (await _t.Results.XpInputsAsync())[_other].CorrectPoints);
        Assert.Equal(3, (await _t.Results.XpInputsAsync())[_other].QuestionsPlayed); // gespeelde vragen ongewijzigd
    }

    [Fact]
    public async Task The_day_overview_shows_a_partial_score_as_partly_right()
    {
        await PlanKaartAsync();
        await PlayAsync(1, s => Stadium(Current(s)).Clubs.Count > 1 ? Wrong(s) : Right(s));
        var summary = (await _quiz.GetOverviewAsync(_me)).Questions.Single(q => q.Number == 1);
        Assert.Equal((false, QuestionType.Kaart), (summary.Correct, summary.Type));
        Assert.True(summary.Partial);
        Assert.False(new QuestionSummary(1, QuestionStatus.Answered, 75, false, "1986", QuestionType.Jaar).Partial);
    }

    [Fact]
    public async Task Answering_every_pin_within_the_grace_is_not_a_timeout()
    {
        await PlanKaartAsync();
        var state = await _quiz.StartAsync(_me, 1);
        for (var n = 1; n < TotalPins; n++) state = await _quiz.GuessAsync(_me, 1, Right(state));
        _clock.Advance(TimeSpan.FromSeconds(QuestionRules.KaartSeconds + GameRules.LatencyGraceSeconds / 2));
        state = await _quiz.GuessAsync(_me, 1, Wrong(state));
        Assert.False(state.Result!.TimedOut);
        Assert.DoesNotContain(state.Result.AllHints, h => h.StartsWith("⌛"));
    }

    [Fact]
    public async Task Two_simultaneous_valid_guesses_for_the_same_pin_count_once()
    {
        await PlanKaartAsync();
        var start = await _quiz.StartAsync(_me, 1);

        // Een tweede verzoek dat tegelijk binnenkomt, werkt met een oudere kopie van de poging.
        using var other = _t.NewContext();
        var stale = _t.NewQuiz(other);
        await stale.GetStateAsync(_me, 1);

        await _quiz.GuessAsync(_me, 1, Right(start));
        var dropped = await stale.GuessAsync(_me, 1, Right(start));
        Assert.Equal(1, dropped.View!.Found);

        await _db.Entry(_db.Attempts.Single(a => a.UserId == _me)).ReloadAsync();
        var attempt = _db.Attempts.Single(a => a.UserId == _me);
        Assert.Equal((1, 1, 1), (attempt.Guesses, attempt.Found.Count, attempt.Picks.Count));
    }

    [Fact]
    public void Names_cannot_contain_the_separator_or_surrounding_spaces()
    {
        static List<string> Problems(KaartData d) => QuestionEngine.Problems(QuestionType.Kaart, QuestionEngine.Serialize(d));
        KaartData With(KaartStadion extra) => Map with { Pins = [.. Map.Pins, extra] };

        Assert.NotEmpty(Problems(With(new("Ghelamco", "Gent", 51.01, 3.73, ["KAA|Gent"]))));
        Assert.NotEmpty(Problems(With(new("Ghelamco|Arena", "Gent", 51.01, 3.73, ["Gent"]))));
        Assert.NotEmpty(Problems(With(new("Ghelamco", "Gent", 51.01, 3.73, [" Gent"]))));
        Assert.NotEmpty(Problems(With(new("Ghelamco ", "Gent", 51.01, 3.73, ["Gent"]))));
        Assert.Empty(Problems(With(new("Ghelamco", "Gent", 51.01, 3.73, ["Gent"]))));
    }

    [Fact]
    public async Task The_seeder_trims_names()
    {
        var root = Directory.CreateTempSubdirectory("kaart-trim-").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "Data", "questions"));
            var untidy = Map with
            {
                Season = "2027-28",
                Pins = [.. Map.Pins.Select(p => p with { Stadium = $" {p.Stadium} ", Clubs = [.. p.Clubs.Select(c => c + " ")] })],
            };
            var json = JsonSerializer.Serialize(new[] { new { untidy.Question, untidy.Competition, untidy.Season, untidy.Pins, status = "approved" } },
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            await File.WriteAllTextAsync(Path.Combine(root, "Data", "questions", "kaart.json"), json);

            await new QuestionSeeder(_db, new Env(root), NullLogger<QuestionSeeder>.Instance).SeedAsync();
            var item = _db.QuizItems.Single(i => i.Type == QuestionType.Kaart && i.Key == "Testcompetitie|2027-28");
            var data = QuestionEngine.Parse<KaartData>(item.Data);
            Assert.Equal(Map.Pins.Select(p => p.Stadium), data.Pins.Select(p => p.Stadium));
            Assert.Equal(Map.Pins.SelectMany(p => p.Clubs), data.Pins.SelectMany(p => p.Clubs));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task The_result_leaves_out_the_city_when_it_is_in_the_club_name()
    {
        await PlanKaartAsync();
        var state = await PlayAsync(1, s => Right(s));
        Assert.Contains("✅ Genk", state.Result!.AllHints);                       // niet "Genk · Genk"
        Assert.Contains("✅ Standard · Luik", state.Result.AllHints);
    }

    public void Dispose() => _t.Dispose();
}
