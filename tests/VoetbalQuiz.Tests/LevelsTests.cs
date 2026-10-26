using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Tests;

public class LevelsTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(99, 1)]
    [InlineData(100, 2)]
    [InlineData(299, 2)]
    [InlineData(300, 3)]
    [InlineData(600, 4)]
    [InlineData(4500, 10)]
    [InlineData(1_000_000, 141)] // geen maximum
    public void Level_follows_growing_xp_curve(int xp, int expected) =>
        Assert.Equal(expected, Levels.For(xp).Level);

    [Fact]
    public void Level_info_shows_progress_to_next_level()
    {
        var info = Levels.For(350); // level 3 start bij 300, level 4 bij 600
        Assert.Equal(3, info.Level);
        Assert.Equal(50, info.XpIntoLevel);
        Assert.Equal(300, info.XpForNextLevel);
    }

    [Theory]
    [InlineData(true, 100, true, true)]
    [InlineData(false, 30, true, true)]     // deelpunten
    [InlineData(false, 0, true, true)]      // fout gegokt
    [InlineData(false, 0, false, false)]    // gepast of tijd op
    public void Only_tried_questions_count_as_played(bool correct, int points, bool guessed, bool expected) =>
        Assert.Equal(expected, Levels.Tried(correct, points, guessed));

    [Theory]
    [InlineData(10, 10, 10, true)]
    [InlineData(10, 5, 10, true)]
    [InlineData(10, 4, 10, false)]
    [InlineData(10, 0, 10, false)]     // alles overgeslagen
    [InlineData(9, 9, 10, false)]      // niet afgewerkt
    public void Completed_day_needs_half_of_the_questions_tried(int answered, int tried, int size, bool expected) =>
        Assert.Equal(expected, Levels.CompletedDay(answered, tried, size));

    [Fact]
    public void Xp_rewards_playing_and_playing_well()
    {
        // 10 vragen gespeeld, samen 700 punten juist, volledige dag, gewonnen.
        var xp = Levels.Xp(new XpInput(10, 700, 1, 0, 1));
        Assert.Equal(100 + 140 + 50 + 100, xp);
    }

    [Fact]
    public void Every_avatar_has_a_unique_id() =>
        Assert.Equal(Avatars.All.Count, Avatars.All.Select(a => a.Id).Distinct().Count());
}
