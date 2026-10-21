using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Tests;

/// <summary>
/// Stap 3.6: het admin-paneel weet of een speler of vraag nu in het spel zit (de daily van vandaag of een lopende special),
/// zodat het bij opslaan kan waarschuwen. Vandaag is zaterdag 17 oktober 2026 (<see cref="TestDb.DefaultNow"/>).
/// </summary>
public sealed class AdminInPlayTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 10, 17);
    private readonly TestDb _t = new();

    private Player Player(string name)
    {
        var p = new Player { Name = name, Status = PlayerStatus.Approved, Hints = ["1", "2", "3", "4", "5"] };
        _t.Db.Players.Add(p);
        return p;
    }

    private QuizItem Item(string key)
    {
        var i = new QuizItem { Type = QuestionType.Snelvuur, Key = key, Data = "{}", Status = PlayerStatus.Approved };
        _t.Db.QuizItems.Add(i);
        return i;
    }

    private static SpecialEvent Special(string name, DateOnly start, DateOnly end) =>
        new() { Theme = name.ToLowerInvariant(), Name = name, StartDate = start, EndDate = end };

    private void Challenge(DateOnly date, SpecialEvent? special, params DailyQuestion[] questions)
    {
        var number = 1;
        foreach (var q in questions) q.Number = number++;
        _t.Db.DailyChallenges.Add(new DailyChallenge { Date = date, SpecialEvent = special, Questions = [.. questions] });
    }

    [Fact]
    public async Task Players_and_questions_in_todays_daily_or_a_running_special_are_flagged()
    {
        var inDaily = Player("In de daily");
        var inCounter = Player("In een counter van de special");
        var yesterday = Player("Gisteren");
        var free = Player("Nergens");
        var itemToday = Item("vandaag");
        var itemOldSpecial = Item("oude special");
        var itemBoth = Item("beide");
        _t.Db.SaveChanges();

        var running = Special("Derby Special", Today.AddDays(-2), Today.AddDays(3)); // loopt over meerdere dagen
        var finished = Special("Oude Special", Today.AddDays(-10), Today.AddDays(-5));
        Challenge(Today, null,
            new DailyQuestion { Type = QuestionType.WieBenIk, PlayerId = inDaily.Id },
            new DailyQuestion { Type = QuestionType.Snelvuur, QuizItemId = itemToday.Id },
            new DailyQuestion { Type = QuestionType.Snelvuur, QuizItemId = itemBoth.Id });
        Challenge(Today.AddDays(-1), null, new DailyQuestion { Type = QuestionType.WieBenIk, PlayerId = yesterday.Id });
        Challenge(running.StartDate, running,
            new DailyQuestion { Type = QuestionType.Counter, CounterPlayerIds = [free.Id + 100, inCounter.Id] },
            new DailyQuestion { Type = QuestionType.Snelvuur, QuizItemId = itemBoth.Id });
        Challenge(finished.StartDate, finished, new DailyQuestion { Type = QuestionType.Snelvuur, QuizItemId = itemOldSpecial.Id });
        _t.Db.SaveChanges();

        Assert.Equal("de daily van vandaag", (await _t.Admin.GetAsync(inDaily.Id)).InPlay);
        Assert.Equal("de special Derby Special", (await _t.Admin.GetAsync(inCounter.Id)).InPlay);
        Assert.Null((await _t.Admin.GetAsync(yesterday.Id)).InPlay);
        Assert.Null((await _t.Admin.GetAsync(free.Id)).InPlay);

        Assert.Equal("de daily van vandaag", (await _t.Admin.QuizItemAsync(itemToday.Id)).InPlay);
        Assert.Equal("de daily van vandaag en de special Derby Special", (await _t.Admin.QuizItemAsync(itemBoth.Id)).InPlay);
        Assert.Null((await _t.Admin.QuizItemAsync(itemOldSpecial.Id)).InPlay);
    }

    public void Dispose() => _t.Dispose();
}
