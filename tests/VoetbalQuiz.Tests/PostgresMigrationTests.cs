using DotNet.Testcontainers.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Testcontainers.PostgreSql;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Tests;

/// <summary>
/// Rooktest tegen een echte PostgreSQL (in een container): alle migraties op een lege database, de seeders zoals bij het
/// opstarten (Program.cs) en een daily die gespeeld wordt. De andere tests draaien op SQLite en zien de migraties en
/// PostgreSQL-eigen zaken (int-arrays, gefilterde unieke indexen, concurrency tokens) nooit.
/// Zonder Docker (of Podman) wordt de test overgeslagen.
/// </summary>
public sealed class PostgresMigrationTests
{
    /// <summary>Zonder database: het model (voor PostgreSQL) moet overeenkomen met de laatste migratie.</summary>
    [Fact]
    public void The_model_has_no_changes_without_a_migration()
    {
        var options = new DbContextOptionsBuilder<QuizDbContext>().UseNpgsql("Host=localhost;Database=niet-gebruikt").Options;
        using var db = new QuizDbContext(options);
        Assert.False(db.Database.HasPendingModelChanges(), "Het model wijkt af van de laatste migratie: maak een migratie aan.");
    }

    [DockerFact]
    public async Task All_migrations_apply_to_an_empty_database_and_a_daily_can_be_played()
    {
        await using var container = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await container.StartAsync();

        var options = new DbContextOptionsBuilder<QuizDbContext>().UseNpgsql(container.GetConnectionString()).Options;
        await using var db = new QuizDbContext(options);

        await db.Database.MigrateAsync();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.False(db.Database.HasPendingModelChanges(), "Het model wijkt af van de laatste migratie: maak een migratie aan.");

        var env = ServerContent.Environment();
        await new PlayerSeeder(db, env, NullLogger<PlayerSeeder>.Instance).SeedAsync();
        await new QuestionSeeder(db, env, NullLogger<QuestionSeeder>.Instance).SeedAsync();
        Assert.True(await db.Players.CountAsync() >= GameRules.QuestionsPerDay);

        var user = new AppUser { UserName = "sander" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var clock = new FakeTimeProvider(TestDb.DefaultNow);
        var quiz = new QuizService(db, clock, new ResultsService(db, clock, new MemoryCache(new MemoryCacheOptions())));
        var challenge = await quiz.GetOrCreateChallengeAsync(quiz.Today);
        Assert.NotEmpty(challenge.Questions);

        await quiz.StartAsync(user.Id, 1);
        var state = await quiz.GiveUpAsync(user.Id, 1);
        Assert.Equal(QuestionStatus.Answered, state.Status);
        Assert.Equal(1, (await quiz.GetOverviewAsync(user.Id)).Questions.Count(q => q.Status == QuestionStatus.Answered));
    }
}

/// <summary>Een test die een container nodig heeft: overgeslagen als er geen Docker (of Podman) bereikbaar is.</summary>
public sealed class DockerFactAttribute : FactAttribute
{
    private static readonly Lazy<bool> Available = new(() =>
    {
        try
        {
            return TestcontainersSettings.OS.DockerEndpointAuthConfig is not null;
        }
        catch (Exception)
        {
            return false;
        }
    });

    public DockerFactAttribute()
    {
        if (!Available.Value)
            Skip = "Geen Docker of Podman bereikbaar: start Docker (of zet DOCKER_HOST) om de PostgreSQL-rooktest te draaien.";
    }
}
