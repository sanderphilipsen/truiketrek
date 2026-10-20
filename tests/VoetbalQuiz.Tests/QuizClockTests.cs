using Microsoft.Extensions.Time.Testing;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Tests;

/// <summary>De quizdag wisselt om middernacht Belgische tijd, in de zomer (UTC+2) en in de winter (UTC+1).</summary>
public class QuizClockTests
{
    [Theory]
    [InlineData("2026-10-17T21:59:59Z", "2026-10-17")] // zomertijd: 23:59:59 in België
    [InlineData("2026-10-17T22:00:00Z", "2026-10-18")] // middernacht in België, nog 22:00 in UTC
    [InlineData("2026-12-31T22:59:59Z", "2026-12-31")] // wintertijd
    [InlineData("2026-12-31T23:00:00Z", "2027-01-01")]
    public void The_day_changes_at_midnight_in_Belgium(string utc, string expected)
    {
        var clock = new FakeTimeProvider(DateTimeOffset.Parse(utc));
        Assert.Equal(DateOnly.Parse(expected), QuizClock.Today(clock));
        Assert.Equal(DateOnly.Parse(expected), QuizDay.DateOf(DateTimeOffset.Parse(utc)));
    }

    [Fact]
    public void Database_times_are_shown_in_Belgian_time()
    {
        var utc = new DateTime(2026, 10, 17, 22, 30, 0, DateTimeKind.Unspecified); // zo komt het uit de database
        Assert.Equal(0, QuizClock.ToBrussels(utc).Hour);
        Assert.Equal(new DateOnly(2026, 10, 18), QuizClock.ToBrusselsDate(utc));
        Assert.Equal(23, QuizClock.ToBrussels(new DateTime(2026, 12, 1, 22, 0, 0)).Hour);
    }

    [Fact]
    public async Task A_new_daily_starts_at_Belgian_midnight()
    {
        using var t = new TestDb(new DateTimeOffset(2026, 10, 17, 21, 59, 0, TimeSpan.Zero));
        for (var i = 1; i <= 15; i++)
            t.Db.Players.Add(new VoetbalQuiz.Server.Data.Player { Name = $"Speler {i}", Hints = ["Tip"] });
        await t.Db.SaveChangesAsync();

        var before = await t.Quiz.GetOrCreateChallengeAsync(t.Quiz.Today);
        t.Clock.Advance(TimeSpan.FromMinutes(1));
        var after = await t.Quiz.GetOrCreateChallengeAsync(t.Quiz.Today);

        Assert.Equal(new DateOnly(2026, 10, 17), before.Date);
        Assert.Equal(new DateOnly(2026, 10, 18), after.Date);
        Assert.NotEqual(before.Id, after.Id);
    }
}
