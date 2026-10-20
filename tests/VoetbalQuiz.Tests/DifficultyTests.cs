using System.Text.Json;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Tests;

public class DifficultyTests
{
    [Theory]
    [InlineData(new double[] { 1, 1, 1 }, 1.0)]
    [InlineData(new double[] { 1, 3, 5 }, 3.0)]
    [InlineData(new double[] { 3, 3, 3, 5 }, 3.5)] // 3,5
    [InlineData(new double[] { 3, 3, 3, 3, 5 }, 3.5)] // 3,4 → halve ster naar boven
    [InlineData(new double[] { 3, 3, 3, 3, 3, 3, 5 }, 3.5)] // 3,29 → 3,5
    [InlineData(new double[] { 1, 1, 1, 1, 1, 1, 1, 1, 3 }, 1.0)] // 1,22 → 1
    [InlineData(new double[] { 5, 5 }, 5.0)]
    [InlineData(new double[0], 0.0)]
    public void Average_rounds_to_half_stars(double[] values, double expected) =>
        Assert.Equal(expected, Difficulty.Average(values));

    [Theory]
    [InlineData(null, 3)]
    [InlineData(0, 1)]
    [InlineData(7, 5)]
    [InlineData(4, 4)]
    public void Clamp_keeps_difficulty_between_one_and_five(int? value, int expected) =>
        Assert.Equal(expected, Difficulty.Clamp(value));

    [Theory]
    [InlineData(10, new[] { 1, 2, 2, 3, 3, 3, 3, 4, 4, 5 })]
    [InlineData(5, new[] { 1, 2, 3, 4, 5 })]
    [InlineData(6, new[] { 1, 2, 3, 3, 4, 5 })]
    [InlineData(2, new[] { 3, 3 })]
    [InlineData(0, new int[0])]
    public void Plan_has_one_warm_up_one_closer_and_mostly_three_stars(int questions, int[] expected) =>
        Assert.Equal(expected, Difficulty.Plan(questions));

    [Fact]
    public void Stars_shows_five_positions() => Assert.Equal("★★★★☆", Difficulty.Stars(4));

    /// <summary>Alle spelers en vragen in de seed gebruiken de schaal van 1 tot 5.</summary>
    [Fact]
    public void Seed_uses_the_five_star_scale()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "VoetbalQuiz.slnx"))) dir = dir.Parent;
        var data = Path.Combine(dir!.FullName, "src", "VoetbalQuiz.Server", "Data");

        var wrong = Directory.GetFiles(Path.Combine(data, "players"), "*.json")
            .Concat(Directory.GetFiles(Path.Combine(data, "questions"), "*.json"))
            .SelectMany(file => JsonDocument.Parse(File.ReadAllText(file)).RootElement.EnumerateArray()
                .Where(item => item.TryGetProperty("difficulty", out var d) && d.GetInt32() is < Difficulty.Min or > Difficulty.Max)
                .Select(item => $"{Path.GetFileName(file)}: {item}"))
            .ToList();

        Assert.Empty(wrong);
    }
}
