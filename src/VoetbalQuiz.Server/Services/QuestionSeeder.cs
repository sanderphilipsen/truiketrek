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
        // Kaarten die al in een reeks zitten: hun spelden en clubs liggen vast (zie QuestionEngine.KaartOrder).
        var playedMaps = (await db.DailyQuestions.Where(q => q.Type == QuestionType.Kaart && q.QuizItemId != null)
            .Select(q => q.QuizItemId!.Value).ToListAsync()).ToHashSet();
        // Krantenartikels die al gespeeld zijn: pogingen onthouden de gaten op hun plaats (zie QuestionEngine.KrantGaps).
        var playedArticles = (await db.Attempts.Where(a => a.DailyQuestion.Type == QuestionType.Krant && a.DailyQuestion.QuizItemId != null)
            .Select(a => a.DailyQuestion.QuizItemId!.Value).Distinct().ToListAsync()).ToHashSet();
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

                // Een gespeelde kaart neemt enkel wijzigingen over die de antwoorden niet raken (bv. een capaciteit).
                // Een gespeeld artikel idem: nieuwe tekst mag, maar niet als de gaten wijzigen (dan blijft het oude staan).
                if (playedArticles.Contains(item.Id) && !QuestionEngine.KrantGaps(item.Data).SequenceEqual(QuestionEngine.KrantGaps(data)))
                    logger.LogWarning("Artikel {Key} is al gespeeld: de nieuwe gaten worden niet overgenomen.", key);
                else if (!playedMaps.Contains(item.Id) || QuestionEngine.KaartAnswers(item.Data).SequenceEqual(QuestionEngine.KaartAnswers(data))) item.Data = data;
                else logger.LogWarning("Kaart {Key} is al gespeeld: de nieuwe spelden worden niet overgenomen. Een gewijzigde kaart heeft een nieuwe sleutel nodig (land, competitie en seizoen), bv. een ander seizoen.", key);
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
                QuestionType.Volgorde => e.Deserialize<VolgordeData>(Json) is { Items: not null } d
                    ? (QuestionEngine.KeyFor(type, QuestionEngine.Serialize(d)), QuestionEngine.Serialize(d)) : (null, ""),
                QuestionType.Krant => e.Deserialize<KrantData>(Json) is { Text: not null } d
                    ? (d.Headline, QuestionEngine.Serialize(d with { Intro = d.Intro ?? "" })) : (null, ""),
                QuestionType.Detective => e.Deserialize<DetectiveData>(Json) is { Suspects: not null, Clues: not null } d
                    ? (d.Title, QuestionEngine.Serialize(d)) : (null, ""),
                // Kaart: enkel de inhoud (competitie, seizoen, spelden); trefwoorden en bronnen staan in Meta.
                QuestionType.Kaart => e.Deserialize<KaartData>(Json) is { Pins: not null } d
                    ? (QuestionEngine.KeyFor(type, QuestionEngine.Serialize(d)),
                       // Namen getrimd: ze staan als keuze in de lijst en komen zo terug in de gok.
                       // Het land in hoofdletters; een kaart zonder land is een kaart van België.
                       QuestionEngine.Serialize(d with
                       {
                           Country = d.Land,
                           Pins = d.Pins.Select(p => p with
                           {
                               Stadium = p.Stadium?.Trim()!, City = p.City?.Trim()!, Clubs = (p.Clubs ?? []).Select(c => c?.Trim()!).ToList(),
                           }).ToList(),
                       }))
                    : (null, ""),
                // Lijst: namen getrimd, zodat een rij en een naam altijd bij elkaar passen.
                QuestionType.Lijst => e.Deserialize<LijstData>(Json) is { Rows: not null, Names: not null } d
                    ? (d.Question, QuestionEngine.Serialize(d with
                    {
                        Rows = d.Rows.Select(r => r with { Answer = string.IsNullOrWhiteSpace(r.Answer) ? null : r.Answer.Trim() }).ToList(),
                        Names = d.Names.Select(n => new LijstNaam(n.Name?.Trim() ?? "", n.Aliases ?? [])).ToList(),
                    }))
                    : (null, ""),
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
