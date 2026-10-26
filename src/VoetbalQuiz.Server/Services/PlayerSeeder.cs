using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Services;

/// <summary>
/// Leest alle spelers uit Data/players/*.json en zet ze in de database.
/// Bestaande spelers (zelfde naam) worden bijgewerkt, behalve als ze in het admin-paneel aangepast zijn:
/// dan is de database de bron van waarheid.
/// </summary>
public class PlayerSeeder(QuizDbContext db, IWebHostEnvironment env, ILogger<PlayerSeeder> logger)
{
    private record PlayerJson(
        string Name, List<string>? Answers, string? Country, string? Competition,
        string? Era, int? Difficulty, List<string> Hints,
        string? Status, List<string>? Sources, List<string>? Tags, string? ReviewNote, DateOnly? CheckedAt, DateOnly? AsOf);

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task SeedAsync()
    {
        var folder = Path.Combine(env.ContentRootPath, "Data", "players");
        if (!Directory.Exists(folder))
        {
            logger.LogWarning("Geen spelersmap gevonden op {Folder}", folder);
            return;
        }

        var existing = await db.Players.ToDictionaryAsync(p => p.Name);
        var seen = new HashSet<string>();
        int added = 0, updated = 0, skipped = 0, kept = 0;

        foreach (var file in Directory.EnumerateFiles(folder, "*.json").Order())
        {
            List<PlayerJson>? items;
            try
            {
                await using var stream = File.OpenRead(file);
                items = await JsonSerializer.DeserializeAsync<List<PlayerJson>>(stream, JsonOptions);
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "Ongeldige JSON in {File}", file);
                continue;
            }

            foreach (var item in items ?? [])
            {
                if (string.IsNullOrWhiteSpace(item.Name) || item.Hints is not { Count: GameRules.HintCount }
                    || !seen.Add(item.Name))
                {
                    skipped++;
                    continue;
                }

                if (!existing.TryGetValue(item.Name, out var player))
                {
                    player = new Player { Name = item.Name };
                    db.Players.Add(player);
                    added++;
                }
                else if (player.EditedInAdmin)
                {
                    kept++;
                    continue;
                }
                else
                {
                    updated++;
                }

                player.Answers = item.Answers ?? [];
                player.Country = item.Country ?? "";
                player.Competition = item.Competition ?? "";
                player.Era = item.Era ?? "modern";
                player.Difficulty = Difficulty.Clamp(item.Difficulty);
                player.Hints = item.Hints;
                player.Status = item.Status?.ToLowerInvariant() switch
                {
                    "approved" => PlayerStatus.Approved,
                    "rejected" => PlayerStatus.Rejected,
                    _ => PlayerStatus.Review,
                };
                player.Sources = item.Sources ?? [];
                player.Tags = item.Tags ?? [];
                player.ReviewNote = item.ReviewNote;
                player.CheckedAt = item.CheckedAt;
                player.AsOf = item.AsOf;
            }
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Spelers geladen: {Added} nieuw, {Updated} bijgewerkt, {Kept} behouden (admin), {Skipped} overgeslagen",
            added, updated, kept, skipped);
    }
}
