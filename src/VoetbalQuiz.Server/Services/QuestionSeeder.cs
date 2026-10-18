using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Services;

/// <summary>
/// Leest de vragenbank uit Data/questions/*.json (één bestand per type) en zet de standaard weekplanning klaar.
/// Net als bij spelers: een vraag die in het admin-paneel aangepast werd, wordt niet meer overschreven.
/// </summary>
public class QuestionSeeder(QuizDbContext db, IWebHostEnvironment env, ILogger<QuestionSeeder> logger)
{
    private record Meta(string? Status, List<string>? Sources, List<string>? Tags, int? Difficulty, string? ReviewNote, DateOnly? CheckedAt);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task SeedAsync()
    {
        await SeedScheduleAsync();

        var folder = Path.Combine(env.ContentRootPath, "Data", "questions");
        if (!Directory.Exists(folder)) return;

        var existing = await db.QuizItems.ToDictionaryAsync(i => (i.Type, i.Key));
        int added = 0, updated = 0, kept = 0, skipped = 0, retired = 0;
        var seen = new HashSet<(QuestionType, string)>();
        var readTypes = new HashSet<QuestionType>();

        foreach (var info in QuestionTypes.All.Where(t => t.FileName.Length > 0))
        {
            var file = Path.Combine(folder, info.FileName + ".json");
            if (!File.Exists(file)) continue;

            List<JsonElement> items;
            try
            {
                items = JsonSerializer.Deserialize<List<JsonElement>>(await File.ReadAllTextAsync(file), Json) ?? [];
                readTypes.Add(info.Type);
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "Ongeldige JSON in {File}", file);
                continue;
            }

            foreach (var element in items)
            {
                var (key, data) = Normalize(info.Type, element);
                if (key is null || QuestionEngine.Problems(info.Type, data).Count > 0)
                {
                    skipped++;
                    continue;
                }
                seen.Add((info.Type, key));
                var meta = element.Deserialize<Meta>(Json)!;

                if (!existing.TryGetValue((info.Type, key), out var item))
                {
                    item = new QuizItem { Type = info.Type, Key = key, Data = data };
                    db.QuizItems.Add(item);
                    existing[(info.Type, key)] = item;
                    added++;
                }
                else if (item.EditedInAdmin)
                {
                    kept++;
                    continue;
                }
                else
                {
                    updated++;
                }

                item.Data = data;
                item.Difficulty = Difficulty.Clamp(meta.Difficulty);
                item.Status = meta.Status?.ToLowerInvariant() switch
                {
                    "approved" => PlayerStatus.Approved,
                    "rejected" => PlayerStatus.Rejected,
                    _ => PlayerStatus.Review,
                };
                item.Sources = meta.Sources ?? [];
                item.Tags = meta.Tags ?? [];
                item.ReviewNote = meta.ReviewNote;
                item.CheckedAt = meta.CheckedAt;
            }
        }

        // Vragen die uit de bestanden verdwenen (vervangen of geschrapt) gaan uit de quiz, tenzij een admin ze beheert.
        // We verwijderen ze niet: zo blijven oude dagen en statistieken kloppen.
        // Enkel voor bestanden die correct ingelezen werden: een half geschreven bestand haalt niets uit de quiz.
        foreach (var item in existing.Values.Where(i => readTypes.Contains(i.Type) && !i.EditedInAdmin
                                                        && i.Status != PlayerStatus.Rejected && !seen.Contains((i.Type, i.Key))))
        {
            item.Status = PlayerStatus.Rejected;
            item.ReviewNote = "Niet meer in de vragenbestanden (vervangen of geschrapt).";
            retired++;
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Vragenbank geladen: {Added} nieuw, {Updated} bijgewerkt, {Kept} behouden (admin), {Retired} uit de quiz gehaald, {Skipped} overgeslagen",
            added, updated, kept, retired, skipped);
    }

    /// <summary>Haalt de inhoud uit een JSON-element (zonder status/bronnen) en bepaalt de sleutel.</summary>
    private static (string? Key, string Data) Normalize(QuestionType type, JsonElement e)
    {
        try
        {
            return type switch
            {
                QuestionType.Snelvuur => e.Deserialize<SnelvuurData>(Json) is { } d ? (d.Question, QuestionEngine.Serialize(d)) : (null, ""),
                QuestionType.Carriere => e.Deserialize<CarriereData>(Json) is { } d
                    ? (d.Answer, QuestionEngine.Serialize(d with { Aliases = d.Aliases ?? [] })) : (null, ""),
                QuestionType.NietBij => e.Deserialize<NietBijData>(Json) is { } d ? (d.Prompt, QuestionEngine.Serialize(d)) : (null, ""),
                QuestionType.Jaar => e.Deserialize<JaarData>(Json) is { } d ? (d.Event, QuestionEngine.Serialize(d)) : (null, ""),
                QuestionType.Emoji => e.Deserialize<EmojiData>(Json) is { } d
                    ? ($"{d.Emojis}|{d.Answer}", QuestionEngine.Serialize(d with { Aliases = d.Aliases ?? [] })) : (null, ""),
                QuestionType.Opstelling => e.Deserialize<OpstellingData>(Json) is { } d
                    ? (d.Title, QuestionEngine.Serialize(d with { Players = d.Players.Select(p => p with { Aliases = p.Aliases ?? [] }).ToList() }))
                    : (null, ""),
                QuestionType.Wissel => e.Deserialize<WisselData>(Json) is { } d
                    ? (QuestionEngine.KeyFor(type, QuestionEngine.Serialize(d)), QuestionEngine.Serialize(d with { Aliases = d.Aliases ?? [] })) : (null, ""),
                QuestionType.Rugnummer => e.Deserialize<RugnummerData>(Json) is { } d
                    ? (QuestionEngine.KeyFor(type, QuestionEngine.Serialize(d)), QuestionEngine.Serialize(d with { Aliases = d.Aliases ?? [] })) : (null, ""),
                QuestionType.Mannetje => e.Deserialize<MannetjeData>(Json) is { } d
                    ? (d.Answer, QuestionEngine.Serialize(d with { Aliases = d.Aliases ?? [], Clues = d.Clues ?? [] })) : (null, ""),
                _ => (null, ""),
            };
        }
        catch (JsonException)
        {
            return (null, "");
        }
    }

    /// <summary>De eerste keer: elke weekdag een vast vraagtype.</summary>
    private async Task SeedScheduleAsync()
    {
        if (await db.DailySchedule.AnyAsync()) return;
        foreach (var day in QuestionTypes.Week)
            db.DailySchedule.Add(new DailyScheduleDay { Day = day, Type = QuestionTypes.DefaultFor(day) });
        await db.SaveChangesAsync();
    }
}
