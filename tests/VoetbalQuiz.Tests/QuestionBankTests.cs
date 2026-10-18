using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Tests;

/// <summary>Leest de echte vragenbank (Data/questions) in: geen enkele vraag mag stilletjes overgeslagen worden.</summary>
public sealed class QuestionBankTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly QuizDbContext _db;
    private readonly string _serverRoot;

    public QuestionBankTests()
    {
        _connection.Open();
        _db = new QuizDbContext(new DbContextOptionsBuilder<QuizDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "VoetbalQuiz.slnx"))) dir = dir.Parent;
        _serverRoot = Path.Combine(dir!.FullName, "src", "VoetbalQuiz.Server");
    }

    [Fact]
    public async Task Every_question_in_the_bank_is_valid_and_loaded()
    {
        await new QuestionSeeder(_db, new Env(_serverRoot), NullLogger<QuestionSeeder>.Instance).SeedAsync();

        foreach (var info in QuestionTypes.All.Where(t => t.FileName.Length > 0))
        {
            var file = Path.Combine(_serverRoot, "Data", "questions", info.FileName + ".json");
            if (!File.Exists(file)) continue;
            var inFile = JsonSerializer.Deserialize<List<JsonElement>>(await File.ReadAllTextAsync(file))!.Count;
            var loaded = await _db.QuizItems.CountAsync(i => i.Type == info.Type);
            Assert.True(inFile == loaded, $"{info.FileName}.json: {inFile} vragen in het bestand, {loaded} ingeladen (ongeldig of dubbel).");
        }

        var unknownTags = (await _db.QuizItems.ToListAsync()).SelectMany(i => i.Tags).Distinct()
            .Where(t => PlayerTags.All.All(x => x.Key != t)).ToList();
        Assert.Empty(unknownTags);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
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
}
