using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Tests;

public class GameRulesTests
{
    [Theory]
    [InlineData(0.0, 100)]
    [InlineData(6.9, 100)]
    [InlineData(7.4, 100)] // netwerkspeling
    [InlineData(7.6, 90)]
    [InlineData(14.6, 80)]
    [InlineData(21.6, 70)]
    [InlineData(28.6, 60)]
    [InlineData(35.6, 50)]
    [InlineData(45.4, 50)]
    [InlineData(45.6, 0)]
    public void Points_drop_per_hint(double seconds, int expected) =>
        Assert.Equal(expected, GameRules.PointsForAnswer(seconds, wrongGuesses: 0));

    [Theory]
    [InlineData(0, 100)]
    [InlineData(1, 95)]
    [InlineData(3, 85)]
    [InlineData(30, 0)]
    public void Wrong_guesses_cost_points(int wrong, int expected) =>
        Assert.Equal(expected, GameRules.PointsAt(1, wrong));

    [Theory]
    [InlineData(0.0, 1)]
    [InlineData(7.0, 2)]
    [InlineData(27.9, 4)]
    [InlineData(28.0, 5)]
    [InlineData(60, 5)]
    public void Hints_appear_every_interval(double seconds, int expected) =>
        Assert.Equal(expected, GameRules.VisibleHints(seconds));
}

public class AnswerMatcherTests
{
    private static readonly string[] Aliases = ["De Bruyne", "KDB"];

    [Theory]
    [InlineData("Kevin De Bruyne")]
    [InlineData("kevin de bruyne")]
    [InlineData("de bruyne")]
    [InlineData("Debruyne")]
    [InlineData("Kevin De Bruine")]
    [InlineData("KDB")]
    public void Accepts_name_aliases_and_small_typos(string guess) =>
        Assert.True(AnswerMatcher.IsMatch(guess, "Kevin De Bruyne", Aliases));

    [Theory]
    [InlineData("")]
    [InlineData("Kevin")]
    [InlineData("Eden Hazard")]
    [InlineData("KDE")]
    public void Rejects_wrong_answers(string guess) =>
        Assert.False(AnswerMatcher.IsMatch(guess, "Kevin De Bruyne", Aliases));

    [Theory]
    [InlineData("Onana")]
    [InlineData("onnana")]
    [InlineData("Amadou Onana")]
    [InlineData("Amadu Onana")]
    public void Accepts_surname_without_alias(string guess) =>
        Assert.True(AnswerMatcher.IsMatch(guess, "Amadou Onana", []));

    [Theory]
    [InlineData("Amadou")]
    [InlineData("Ojana Amadou")]
    public void Rejects_first_name_alone(string guess) =>
        Assert.False(AnswerMatcher.IsMatch(guess, "Amadou Onana", []));

    [Theory]
    [InlineData("Toure", "Yaya Touré", "Yaya")]
    [InlineData("hazard", "Thorgan Hazard", "Thorgan")]
    [InlineData("lukaku", "Romelu Lukaku", "Romelu")]
    public void Surname_always_counts_even_when_ambiguous(string guess, string name, string alias) =>
        Assert.True(AnswerMatcher.IsMatch(guess, name, [alias]));

    [Fact]
    public void Ignores_accents() =>
        Assert.True(AnswerMatcher.IsMatch("Sergio Aguero", "Sergio Agüero", []));
}
