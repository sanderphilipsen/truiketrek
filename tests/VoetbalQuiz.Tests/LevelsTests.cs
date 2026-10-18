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
