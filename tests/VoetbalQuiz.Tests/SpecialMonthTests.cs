using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Tests;

/// <summary>
/// Stap 3.7: een special hoort bij de maand van haar startdatum (beslissing van de eigenaar), in het maandklassement én
/// in de academie (<see cref="MonthRules.Days"/>). Hier een special die van 29 september tot 2 oktober loopt.
/// </summary>
public sealed class SpecialMonthTests : IDisposable
{
    private static readonly DateOnly Start = new(2026, 9, 29), End = new(2026, 10, 2);
    private readonly TestDb _t = new(new DateTimeOffset(2026, 9, 30, 10, 0, 0, TimeSpan.Zero));
    private readonly string _me, _other;

    public SpecialMonthTests()
    {
        for (var i = 1; i <= 15; i++)
            _t.Db.Players.Add(new Player { Name = $"Speler {i}", Hints = [.. Enumerable.Range(1, 5).Select(h => $"Tip {h} van speler {i}")] });
        _t.Db.SaveChanges();
        _me = _t.AddUser("sander");
        _other = _t.AddUser("jan");
    }

    [Fact]
    public async Task A_special_over_two_months_counts_in_the_month_it_starts_in_the_ranking_and_the_academy()
    {
        var special = new SpecialEvent
        {
            Theme = "eigen", Name = "Grensgeval", StartDate = Start, EndDate = End,
            PlayerIds = [.. _t.Db.Players.Select(p => p.Id)], CreatedAt = _t.Clock.GetUtcNow().UtcDateTime,
        };
        _t.Db.SpecialEvents.Add(special);
        await _t.Db.SaveChangesAsync();

        // Jan speelt de special terwijl ze loopt: de eerste vraag juist, de rest gepast.
        var challenge = await _t.Quiz.GetOrCreateSpecialChallengeAsync(special.Id);
        foreach (var q in challenge.Questions.OrderBy(q => q.Number))
        {
            await _t.Quiz.StartAsync(_other, q.Number, special.Id);
            if (q.Number == 1) await _t.Quiz.GuessAsync(_other, q.Number, q.Player!.Name, special.Id);
            else await _t.Quiz.GiveUpAsync(_other, q.Number, special.Id);
        }

        _t.Clock.Advance(TimeSpan.FromDays(17)); // 17 oktober: de special is afgelopen

        var september = await _t.Ranking.ComputeAsync(2026, 9);
        var jan = Assert.Single(september, e => e.UserName == _other);
        Assert.True(jan.Specials > 0);
        Assert.True(jan.Bonus > 0); // podium van de special, ook in september
        Assert.DoesNotContain(await _t.Ranking.ComputeAsync(2026, 10), e => e.UserName == _other);

        var inSeptember = Assert.Single((await _t.Training.GetArchiveAsync(_me, 2026, 9)).Days, d => d.Special is not null);
        Assert.Equal(special.Id, inSeptember.Special!.Id);
        Assert.Equal(Start, inSeptember.Date);
        Assert.DoesNotContain((await _t.Training.GetArchiveAsync(_me, 2026, 10)).Days, d => d.Special is not null);
    }

    public void Dispose() => _t.Dispose();
}
