using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Tests;

/// <summary>
/// Vangnet voor de server-autoriteit: wat <see cref="QuestionEngine.Visible"/> tijdens het spelen toont, bevat nooit het
/// antwoord. Draait over de echte vragenbank en spelersdatabase, voor elk vraagtype en op meerdere momenten van de vraag.
/// Een nieuw vraagtype zonder geheimen in <see cref="Secrets"/> laat deze test falen.
/// </summary>
public sealed class NoLeakTests : IDisposable
{
    private readonly TestDb _t = new();

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Per vraagtype: de teksten die pas na afloop zichtbaar mogen zijn. Bij Snelvuur en Wie hoort er niet bij? is het
    /// antwoord een index en staan alle opties gewoon op het scherm; daar valt geen tekst te verbergen.
    /// </summary>
    private static IEnumerable<string> Secrets(IQuizQuestion q) => q.Type switch
    {
        QuestionType.WieBenIk or QuestionType.Kluts => [q.Player!.Name],
        QuestionType.Counter => q.CounterPlayers.Select(p => p.Name),
        QuestionType.Snelvuur => [],
        QuestionType.NietBij => [Data<NietBijData>(q).Explanation],
        QuestionType.Carriere => [Data<CarriereData>(q).Answer],
        QuestionType.Jaar => [Data<JaarData>(q).Year.ToString()],
        QuestionType.Emoji => [Data<EmojiData>(q).Answer, Data<EmojiData>(q).Explanation],
        QuestionType.Wissel => [Data<WisselData>(q).Answer],
        QuestionType.Rugnummer => [Data<RugnummerData>(q).Answer],
        QuestionType.Mannetje => [Data<MannetjeData>(q).Answer],
        QuestionType.Fiche => Data<FicheData>(q) is var f ? [f.Answer, .. f.Aliases] : [],
        QuestionType.Opstelling => q.Blanks.Select(i => Data<OpstellingData>(q).Players[i].Name),
        // Een waarde die de opdracht zelf al noemt ("sinds 2004") verraadt niet welk kaartje erbij hoort.
        QuestionType.Volgorde => Data<VolgordeData>(q) is var v ? v.Items.Select(i => i.Value).Where(x => !Contains(v.Question, x)) : [],
        QuestionType.Krant => KrantText.Parse(Data<KrantData>(q).Text).Gaps.Where(g => g.Number is null).Select(g => g.Answer),
        QuestionType.Detective => Data<DetectiveData>(q) is var d
            ? d.Clues.Skip(QuestionEngine.DetectiveOpened(d, new FreshAttempt())).Select(c => c.Text).Append(d.Solution)
            : [],
        // Deel 1 noemt nooit een stadion; in deel 2 staan clubs en stadions op het scherm, daar bewaakt Answer het antwoord.
        QuestionType.Kaart => q.Part == QuestionRules.KaartStadiumsPart ? [] : Data<KaartData>(q).Pins.Select(p => p.Stadium),
        QuestionType.Lijst => Data<LijstData>(q).Names.Select(n => n.Name),
        _ => throw new InvalidOperationException($"Geen geheimen voor {q.Type}: voeg ze toe aan {nameof(NoLeakTests)}.{nameof(Secrets)}."),
    };

    [Fact]
    public async Task Visible_never_shows_the_answer_while_playing()
    {
        await new PlayerSeeder(_t.Db, ServerContent.Environment(), NullLogger<PlayerSeeder>.Instance).SeedAsync();
        await new QuestionSeeder(_t.Db, ServerContent.Environment(), NullLogger<QuestionSeeder>.Instance).SeedAsync();
        var players = await _t.Db.Players.Where(p => p.Status == PlayerStatus.Approved).OrderBy(p => p.Id).ToListAsync();
        var items = await _t.Db.QuizItems.Where(i => i.Status == PlayerStatus.Approved).ToListAsync();

        var leaks = new List<string>();
        foreach (var info in QuestionTypes.All)
        {
            var questions = Questions(info, players, items).ToList();
            Assert.True(questions.Count > 0, $"{info.Type}: geen goedgekeurde inhoud om te controleren.");
            foreach (var q in questions)
                leaks.AddRange(Leaks(q));
        }

        Assert.True(leaks.Count == 0, $"{leaks.Count} lekken:\n" + string.Join("\n", leaks.Take(20)));
    }

    private static IEnumerable<IQuizQuestion> Questions(QuestionTypeInfo info, List<Player> players, List<QuizItem> items)
    {
        if (info.FromPlayers)
        {
            if (info.Type == QuestionType.Counter)
            {
                for (var i = 0; i + 5 <= players.Count; i += 5)
                    yield return new Question(i + 1, info.Type, CounterPlayers: players.GetRange(i, 5));
            }
            else
            {
                foreach (var p in players) yield return new Question(p.Id, info.Type, Player: p);
            }
            yield break;
        }

        foreach (var item in items.Where(i => i.Type == info.Type))
        {
            if (info.Type == QuestionType.Kaart)
            {
                yield return new Question(item.Id, info.Type, QuizItem: item, Part: QuestionRules.KaartClubsPart);
                yield return new Question(item.Id + 100_000, info.Type, QuizItem: item, Part: QuestionRules.KaartStadiumsPart);
            }
            else
            {
                yield return new Question(item.Id, info.Type, QuizItem: item,
                    Blanks: info.Type == QuestionType.Opstelling ? QuestionEngine.PickBlanks(item) : null);
            }
        }
    }

    /// <summary>Wat er lekt bij een verse poging: bij de start, halverwege en net voor de tijd om is.</summary>
    private static IEnumerable<string> Leaks(IQuizQuestion q)
    {
        var secrets = Secrets(q).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
        var limit = QuestionEngine.TimeLimit(q);
        foreach (var elapsed in new[] { 0, limit / 2, limit - 0.5 })
        {
            var (hints, view) = QuestionEngine.Visible(q, new FreshAttempt(), elapsed);
            var shown = JsonSerializer.Serialize(new { hints, view }, Json);
            var where = $"{q.Type} #{q.QuizItem?.Key ?? q.Player?.Name ?? q.Id.ToString()} na {elapsed:0.#} s";

            foreach (var secret in secrets.Where(s => Contains(shown, s)))
                yield return $"{where}: \"{secret}\"";

            if (view.Pins?.Any(p => p.Answer is not null) == true) yield return $"{where}: een speld toont zijn antwoord";
            if (view.Article?.SelectMany(p => p).Any(p => p.Answer is not null) == true) yield return $"{where}: een gat toont zijn antwoord";
            if (view.Rows?.Any(r => r.Mine is not null) == true) yield return $"{where}: een rij toont wie ze vond";
            if (view.Fiche?.Skip(view.Step).Any(v => v.Value is not null) == true) yield return $"{where}: een dicht veld van het fiche toont zijn waarde";
        }
    }

    /// <summary>Als los woord, zodat bv. het jaartal 1986 niet in 19860 gevonden wordt.</summary>
    private static bool Contains(string shown, string secret) =>
        Regex.IsMatch(shown, $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(secret)}(?![\p{{L}}\p{{N}}])", RegexOptions.IgnoreCase);

    private static T Data<T>(IQuizQuestion q) => QuestionEngine.Parse<T>(q.QuizItem!.Data);

    private sealed record Question(
        int Id, QuestionType Type, Player? Player = null, QuizItem? QuizItem = null, List<int>? Blanks = null,
        List<Player>? CounterPlayers = null, int Part = 0) : IQuizQuestion
    {
        public int Number => 1;
        List<int> IQuizQuestion.Blanks => Blanks ?? [];
        List<Player> IQuizQuestion.CounterPlayers => CounterPlayers ?? [];
    }

    /// <summary>Een poging die net begonnen is: nog niets gegokt of gevonden.</summary>
    private sealed class FreshAttempt : AttemptState;

    public void Dispose() => _t.Dispose();
}
