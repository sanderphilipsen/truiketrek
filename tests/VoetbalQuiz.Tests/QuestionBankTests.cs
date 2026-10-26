using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Tests;

/// <summary>Leest de echte vragenbank (Data/questions) in: geen enkele vraag mag stilletjes overgeslagen worden.</summary>
public sealed class QuestionBankTests : IDisposable
{
    private readonly TestDb _t = new();
    private readonly QuizDbContext _db;

    public QuestionBankTests()
    {
        _db = _t.Db;
    }

    [Fact]
    public async Task Every_question_in_the_bank_is_valid_and_loaded()
    {
        await new QuestionSeeder(_db, ServerContent.Environment(), NullLogger<QuestionSeeder>.Instance).SeedAsync();

        foreach (var info in QuestionTypes.All.Where(t => t.FileName.Length > 0))
        {
            var file = Path.Combine(ServerContent.Root, "Data", "questions", info.FileName + ".json");
            if (!File.Exists(file)) continue;
            var inFile = JsonSerializer.Deserialize<List<JsonElement>>(await File.ReadAllTextAsync(file))!.Count;
            var loaded = await _db.QuizItems.CountAsync(i => i.Type == info.Type);
            Assert.True(inFile == loaded, $"{info.FileName}.json: {inFile} vragen in het bestand, {loaded} ingeladen (ongeldig of dubbel).");
        }

        var unknownTags = (await _db.QuizItems.ToListAsync()).SelectMany(i => i.Tags).Distinct()
            .Where(t => PlayerTags.All.All(x => x.Key != t)).ToList();
        Assert.Empty(unknownTags);
    }

    [Fact]
    public async Task Peildatum_comes_from_the_seed_and_a_fiche_keeps_its_own()
    {
        await new QuestionSeeder(_db, ServerContent.Environment(), NullLogger<QuestionSeeder>.Instance).SeedAsync();

        // Bij een spelersfiche is de peildatum van de vraag altijd die op het fiche.
        var fiches = await _db.QuizItems.Where(i => i.Type == QuestionType.Fiche).ToListAsync();
        Assert.NotEmpty(fiches);
        Assert.All(fiches, f => Assert.Equal(QuestionEngine.Parse<FicheData>(f.Data).AsOf, f.AsOf));
        Assert.Contains(fiches, f => f.AsOf is not null);
    }

    public void Dispose() => _t.Dispose();
}
