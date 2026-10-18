using System.Text.Json;
using VoetbalQuiz.Server.Data;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Services;

/// <summary>
/// Alles wat per vraagtype verschilt: hoe lang een vraag duurt, wat de speler te zien krijgt,
/// hoe een antwoord beoordeeld wordt en wat er na afloop getoond wordt.
/// </summary>
public static class QuestionEngine
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static T Data<T>(DailyQuestion q) => JsonSerializer.Deserialize<T>(q.QuizItem!.Data, Json)!;

    public static T Parse<T>(string data) => JsonSerializer.Deserialize<T>(data, Json)!;

    public static string Serialize<T>(T data) => JsonSerializer.Serialize(data, Json);

    /// <summary>Het juiste antwoord als tekst (voor overzichten en statistieken).</summary>
    public static string AnswerLabel(DailyQuestion q) => q.Type switch
    {
        QuestionType.WieBenIk => q.Player!.Name,
        QuestionType.Snelvuur => Data<SnelvuurData>(q) is var d ? d.Options[d.Correct] : "",
        QuestionType.Carriere => Data<CarriereData>(q).Answer,
        QuestionType.NietBij => Data<NietBijData>(q) is var d ? d.Options[d.Odd] : "",
        QuestionType.Jaar => Data<JaarData>(q).Year.ToString(),
        QuestionType.Emoji => Data<EmojiData>(q).Answer,
        QuestionType.Opstelling => Data<OpstellingData>(q).Title,
        QuestionType.Wissel => Data<WisselData>(q).Answer,
        QuestionType.Rugnummer => Data<RugnummerData>(q).Answer,
        QuestionType.Mannetje => Data<MannetjeData>(q).Answer,
        QuestionType.Kluts => q.Player!.Name,
        QuestionType.Counter => $"Counter ({q.CounterPlayers.Count} spelers)",
        _ => "",
    };

    public static double TimeLimit(DailyQuestion q) => q.Type switch
    {
        QuestionType.WieBenIk => GameRules.TimeLimitSeconds,
        QuestionType.Snelvuur => QuestionRules.SnelvuurSeconds,
        QuestionType.Carriere => QuestionRules.CarriereLimit(Data<CarriereData>(q).Clubs.Count),
        QuestionType.NietBij => QuestionRules.NietBijSeconds,
        QuestionType.Jaar => QuestionRules.JaarSeconds,
        QuestionType.Emoji => QuestionRules.EmojiSeconds,
        QuestionType.Opstelling => QuestionRules.OpstellingSeconds,
        QuestionType.Wissel or QuestionType.Rugnummer or QuestionType.Mannetje or QuestionType.Kluts =>
            QuestionRules.RevealLimit(QuestionRules.RevealSteps),
        QuestionType.Counter => QuestionRules.CounterSeconds,
        _ => GameRules.TimeLimitSeconds,
    };

    /// <summary>Wat de speler nu ziet. Nooit het antwoord: dat komt pas in <see cref="Result"/>.</summary>
    public static (List<string> Hints, QuestionView View) Visible(DailyQuestion q, Attempt a, double elapsed)
    {
        switch (q.Type)
        {
            case QuestionType.WieBenIk:
                return (q.Player!.Hints.Take(GameRules.VisibleHints(elapsed)).ToList(), new QuestionView());
            case QuestionType.Snelvuur:
            {
                var d = Data<SnelvuurData>(q);
                return ([], new QuestionView(d.Question, d.Options));
            }
            case QuestionType.Carriere:
            {
                var d = Data<CarriereData>(q);
                var step = QuestionRules.CarriereVisible(elapsed, d.Clubs.Count);
                return (d.Clubs.Take(step).ToList(),
                    new QuestionView("Welke speler had deze carrière?", Step: step, Steps: d.Clubs.Count));
            }
            case QuestionType.NietBij:
            {
                var d = Data<NietBijData>(q);
                return ([], new QuestionView(d.Prompt, d.Options));
            }
            case QuestionType.Jaar:
                return ([], new QuestionView(Data<JaarData>(q).Event));
            case QuestionType.Emoji:
            {
                var d = Data<EmojiData>(q);
                return ([], new QuestionView(d.Emojis, Note: d.Kind == "club" ? "Welke club is dit?" : "Welke speler is dit?"));
            }
            case QuestionType.Opstelling:
            {
                var d = Data<OpstellingData>(q);
                return ([], new QuestionView(d.Title, Lineup: Slots(d, q.Blanks, a.Found, revealAll: false), MaxPoints: 200));
            }
            case QuestionType.Wissel:
            {
                var step = Step(elapsed);
                return (WisselFacts(Data<WisselData>(q)).Take(step).ToList(),
                    new QuestionView("Welke speler maakte deze transfer?", Step: step, Steps: QuestionRules.RevealSteps));
            }
            case QuestionType.Rugnummer:
            {
                var d = Data<RugnummerData>(q);
                var step = Step(elapsed);
                return (RugnummerFacts(d).Take(step - 1).ToList(),
                    new QuestionView("Wie droeg dit rugnummer?", Step: step, Steps: QuestionRules.RevealSteps, Shirt: d.Number));
            }
            case QuestionType.Mannetje:
            {
                var d = Data<MannetjeData>(q);
                var step = Step(elapsed);
                return (d.Clues.Take(Math.Max(0, step - 3)).ToList(),
                    new QuestionView("Wie is dit mannetje?", Step: step, Steps: QuestionRules.RevealSteps, Avatar: MannetjeAvatar(d.Avatar, step)));
            }
            case QuestionType.Kluts:
            {
                var step = Step(elapsed);
                return ([], new QuestionView("Wie zit er in deze kluts?", Step: step, Steps: QuestionRules.RevealSteps,
                    Letters: Scramble(q.Player!.Name, step - 1, q.Id * 31 + step)));
            }
            case QuestionType.Counter:
            {
                var current = CounterCurrent(q, a);
                var total = q.CounterPlayers.Count;
                return ([], new QuestionView(current < 0 ? null : CounterClue(q.CounterPlayers[current]),
                    Note: current < 0 ? null : $"Speler {current + 1} van {total}",
                    MaxPoints: QuestionTypes.Get(QuestionType.Counter).MaxPoints, Found: a.Found.Count, Total: total));
            }
            default:
                return ([], new QuestionView());
        }
    }

    private static List<LineupSlot> Slots(OpstellingData d, List<int> blanks, List<int> found, bool revealAll) =>
        d.Players.Select((p, i) =>
        {
            var blank = blanks.Contains(i);
            return new LineupSlot(p.Position, !blank || found.Contains(i) || revealAll ? p.Name : null, blank);
        }).ToList();

    private static int Step(double elapsed) => QuestionRules.RevealStep(elapsed, QuestionRules.RevealSteps);

    private static List<string> WisselFacts(WisselData d) =>
        [$"📅 {d.When}", $"💶 {d.Fee}", $"🛫 Van: {d.From}", $"🛬 Naar: {d.To}", $"💬 {d.Extra}"];

    /// <summary>Het rugnummer staat op het shirt; deze tips komen erbij.</summary>
    private static List<string> RugnummerFacts(RugnummerData d) =>
        [$"🧭 {d.Position}", $"🌍 {d.Country}", $"🏟️ {d.Club}", $"💬 {d.Extra}"];

    /// <summary>
    /// Mannetje: stap 1 is een donker silhouet (de echte kleuren gaan niet eens mee), stap 2 de kleuren (wazig getoond),
    /// vanaf stap 3 scherp met rugnummer. Altijd als code, zodat de naam van de avatar nooit meegaat.
    /// </summary>
    public static string MannetjeAvatar(string avatar, int step)
    {
        var a = Avatars.Get(avatar);
        a = step switch
        {
            1 => a with { Skin = Avatars.Skins.Length - 1, HairColor = 0, Shirt = 3, Accent = 3, Number = 0, Headband = a.Headband >= 0 ? 3 : -1, Earring = Earring.None },
            2 => a with { Number = 0 },
            _ => a,
        };
        return Avatars.Encode(a);
    }

    /// <summary>
    /// Kluts in de zestien: de letters van elk woord door elkaar. De eerste <paramref name="fixedPerWord"/> letters
    /// van elk woord staan al juist; leestekens (zoals ' en -) blijven altijd op hun plaats.
    /// </summary>
    public static List<List<LetterTile>> Scramble(string name, int fixedPerWord, int seed)
    {
        var rng = new Random(seed);
        return name.ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(word =>
        {
            var letters = word.Select(c => c.ToString()).ToArray();
            var isLetter = word.Select(char.IsLetter).ToArray();
            var letterIndexes = Enumerable.Range(0, letters.Length).Where(i => isLetter[i]).ToList();
            var fixedIndexes = letterIndexes.Take(Math.Max(0, fixedPerWord)).ToHashSet();
            var open = letterIndexes.Where(i => !fixedIndexes.Contains(i)).ToList();
            var original = open.Select(i => letters[i]).ToList();

            var shuffled = original;
            // Een paar keer proberen, zodat het woord niet toevallig al juist staat.
            for (var attempt = 0; attempt < 10; attempt++)
            {
                shuffled = [.. original.OrderBy(_ => rng.Next())];
                if (!shuffled.SequenceEqual(original)) break;
            }

            var result = (string[])letters.Clone();
            for (var k = 0; k < open.Count; k++) result[open[k]] = shuffled[k];
            return result.Select((l, i) => new LetterTile(l, !isLetter[i] || fixedIndexes.Contains(i))).ToList();
        }).ToList();
    }

    /// <summary>Counter: de speler die aan de beurt is (nog niet geraden of overgeslagen), of -1 als ze allemaal gepasseerd zijn.</summary>
    public static int CounterCurrent(DailyQuestion q, Attempt a) =>
        Enumerable.Range(0, q.CounterPlayers.Count).FirstOrDefault(i => !a.Found.Contains(i) && !a.Skipped.Contains(i), -1);

    /// <summary>Counter: de duidelijkste tip (de laatste) van een speler.</summary>
    private static string CounterClue(Player p) => p.Hints.LastOrDefault() ?? "";

    public record Outcome(bool Done, bool Correct, int Points, bool Wrong);

    /// <summary>Beoordeelt een poging. Getypte types mogen opnieuw proberen; keuzetypes zijn meteen klaar.</summary>
    public static Outcome Guess(DailyQuestion q, Attempt a, string guess, double elapsed)
    {
        switch (q.Type)
        {
            case QuestionType.WieBenIk:
                return AnswerMatcher.IsMatch(guess, q.Player!.Name, q.Player.Answers)
                    ? new(true, true, GameRules.PointsForAnswer(elapsed, a.WrongGuesses), false)
                    : new(false, false, 0, true);

            case QuestionType.Carriere:
            {
                var d = Data<CarriereData>(q);
                return AnswerMatcher.IsMatch(guess, d.Answer, d.Aliases)
                    ? new(true, true, QuestionRules.CarrierePoints(elapsed, d.Clubs.Count, a.WrongGuesses), false)
                    : new(false, false, 0, true);
            }

            case QuestionType.Emoji:
            {
                var d = Data<EmojiData>(q);
                return AnswerMatcher.IsMatch(guess, d.Answer, d.Aliases)
                    ? new(true, true, QuestionRules.EmojiPoints(elapsed, a.WrongGuesses), false)
                    : new(false, false, 0, true);
            }

            case QuestionType.Wissel:
            {
                var d = Data<WisselData>(q);
                return Typed(guess, d.Answer, d.Aliases, elapsed, a);
            }

            case QuestionType.Rugnummer:
            {
                var d = Data<RugnummerData>(q);
                return Typed(guess, d.Answer, d.Aliases, elapsed, a);
            }

            case QuestionType.Mannetje:
            {
                var d = Data<MannetjeData>(q);
                return Typed(guess, d.Answer, d.Aliases, elapsed, a);
            }

            case QuestionType.Kluts:
                return Typed(guess, q.Player!.Name, q.Player.Answers, elapsed, a);

            case QuestionType.Counter:
            {
                var current = CounterCurrent(q, a);
                var points = QuestionRules.CounterPoints(a.Found.Count);
                if (current < 0) return new(true, a.Found.Count == q.CounterPlayers.Count, points, false);

                var p = q.CounterPlayers[current];
                if (guess == QuestionRules.CounterSkip) a.Skipped = [.. a.Skipped, current];
                else if (AnswerMatcher.IsMatch(guess, p.Name, p.Answers)) a.Found = [.. a.Found, current];
                else return new(false, false, points, false); // geen straf: het is een snelheidsronde

                var done = CounterCurrent(q, a) < 0;
                return new(done, done && a.Found.Count == q.CounterPlayers.Count, QuestionRules.CounterPoints(a.Found.Count), false);
            }

            case QuestionType.Snelvuur:
            case QuestionType.NietBij:
            {
                if (!int.TryParse(guess, out var choice) || choice is < 0 or > 3) return new(false, false, 0, false);
                a.Choice = choice;
                var correct = q.Type == QuestionType.Snelvuur ? Data<SnelvuurData>(q).Correct : Data<NietBijData>(q).Odd;
                var limit = TimeLimit(q);
                return choice == correct ? new(true, true, QuestionRules.SpeedPoints(elapsed, limit), false) : new(true, false, 0, false);
            }

            case QuestionType.Jaar:
            {
                if (!int.TryParse(guess, out var year) || year is < QuestionRules.MinYear or > QuestionRules.MaxYear)
                    return new(false, false, 0, false);
                a.Choice = year;
                var target = Data<JaarData>(q).Year;
                return new(true, year == target, QuestionRules.JaarPoints(year, target), false);
            }

            case QuestionType.Opstelling:
            {
                var d = Data<OpstellingData>(q);
                var hit = q.Blanks.Where(i => !a.Found.Contains(i))
                    .FirstOrDefault(i => AnswerMatcher.IsMatch(guess, d.Players[i].Name, d.Players[i].Aliases), -1);
                if (hit < 0) return new(false, false, 0, false); // geen straf: het is zoeken naar namen
                a.Found = [.. a.Found, hit];
                var all = a.Found.Count == q.Blanks.Count;
                return new(all, all, QuestionRules.OpstellingPoints(a.Found.Count, q.Blanks.Count), false);
            }

            default:
                return new(false, false, 0, false);
        }
    }

    /// <summary>Een getypte naam bij een type dat stap voor stap onthult (wissel, rugnummer, mannetje, kluts).</summary>
    private static Outcome Typed(string guess, string answer, IEnumerable<string> aliases, double elapsed, Attempt a) =>
        AnswerMatcher.IsMatch(guess, answer, aliases)
            ? new(true, true, QuestionRules.RevealPointsAt(elapsed, QuestionRules.RevealSteps, a.WrongGuesses), false)
            : new(false, false, 0, true);

    /// <summary>Punten als de tijd om is of de speler stopt: een opstelling en een counter geven deelpunten.</summary>
    public static int PointsWhenStopped(DailyQuestion q, Attempt a) => q.Type switch
    {
        QuestionType.Opstelling => QuestionRules.OpstellingPoints(a.Found.Count, q.Blanks.Count),
        QuestionType.Counter => QuestionRules.CounterPoints(a.Found.Count),
        _ => 0,
    };

    /// <summary>Wat er na afloop getoond wordt: het juiste antwoord en de uitleg.</summary>
    public static AnswerResult Result(DailyQuestion q, Attempt a)
    {
        var baseResult = new AnswerResult(a.Correct, a.Points, AnswerLabel(q), a.WrongGuesses, []);
        return q.Type switch
        {
            QuestionType.WieBenIk => baseResult with { AllHints = q.Player!.Hints },
            QuestionType.Carriere => baseResult with { AllHints = Data<CarriereData>(q).Clubs },
            QuestionType.Snelvuur => baseResult with { CorrectIndex = Data<SnelvuurData>(q).Correct, MyChoice = a.Choice },
            QuestionType.NietBij => Data<NietBijData>(q) is var n
                ? baseResult with { CorrectIndex = n.Odd, MyChoice = a.Choice, Explanation = n.Explanation }
                : baseResult,
            QuestionType.Jaar => baseResult with { CorrectYear = Data<JaarData>(q).Year, MyYear = a.Choice },
            QuestionType.Emoji => baseResult with { Explanation = Data<EmojiData>(q).Explanation },
            QuestionType.Opstelling => baseResult with
            {
                FullLineup = Slots(Data<OpstellingData>(q), q.Blanks, a.Found, revealAll: true),
                Explanation = $"{a.Found.Count} van de {q.Blanks.Count} namen gevonden",
            },
            QuestionType.Wissel => baseResult with { AllHints = WisselFacts(Data<WisselData>(q)) },
            QuestionType.Rugnummer => Data<RugnummerData>(q) is var r
                ? baseResult with { AllHints = [$"🔢 Rugnummer {r.Number}", .. RugnummerFacts(r)] }
                : baseResult,
            QuestionType.Mannetje => baseResult with { AllHints = Data<MannetjeData>(q).Clues },
            QuestionType.Kluts => baseResult with { AllHints = q.Player!.Hints },
            QuestionType.Counter => baseResult with
            {
                AllHints = q.CounterPlayers.Select((p, i) =>
                    (a.Found.Contains(i) ? "✅ " : a.Skipped.Contains(i) ? "⏭️ " : "⌛ ") + p.Name).ToList(),
                Explanation = $"{a.Found.Count} van de {q.CounterPlayers.Count} spelers geraden",
            },
            _ => baseResult,
        };
    }

    /// <summary>Opstelling: kies welke plaatsen ingevuld moeten worden (de keeper blijft zichtbaar).</summary>
    public static List<int> PickBlanks(QuizItem item)
    {
        var d = Parse<OpstellingData>(item.Data);
        return Enumerable.Range(0, d.Players.Count)
            .Where(i => d.Players[i].Position != "K")
            .OrderBy(_ => Random.Shared.Next())
            .Take(QuestionRules.OpstellingBlanks)
            .Order()
            .ToList();
    }

    /// <summary>Korte omschrijving van een vraag uit de bank, voor lijsten in het admin-paneel.</summary>
    public static (string Label, string Answer) Describe(QuestionType type, string data) => type switch
    {
        QuestionType.Snelvuur => Parse<SnelvuurData>(data) is var d ? (d.Question, d.Options.ElementAtOrDefault(d.Correct) ?? "") : ("", ""),
        QuestionType.Carriere => Parse<CarriereData>(data) is var d ? (string.Join(" → ", d.Clubs.Select(c => c.Split(" (")[0])), d.Answer) : ("", ""),
        QuestionType.NietBij => Parse<NietBijData>(data) is var d ? (d.Prompt, d.Options.ElementAtOrDefault(d.Odd) ?? "") : ("", ""),
        QuestionType.Jaar => Parse<JaarData>(data) is var d ? (d.Event, d.Year.ToString()) : ("", ""),
        QuestionType.Emoji => Parse<EmojiData>(data) is var d ? (d.Emojis, d.Answer) : ("", ""),
        QuestionType.Opstelling => Parse<OpstellingData>(data) is var d ? (d.Title, d.Team) : ("", ""),
        QuestionType.Wissel => Parse<WisselData>(data) is var d ? ($"{d.From} → {d.To} ({d.When})", d.Answer) : ("", ""),
        QuestionType.Rugnummer => Parse<RugnummerData>(data) is var d ? ($"#{d.Number} – {d.Club}", d.Answer) : ("", ""),
        QuestionType.Mannetje => Parse<MannetjeData>(data) is var d ? (string.Join(" · ", d.Clues), d.Answer) : ("", ""),
        _ => ("", ""),
    };

    /// <summary>Unieke sleutel van een vraag (zelfde als in de JSON-seed).</summary>
    public static string KeyFor(QuestionType type, string data) => type switch
    {
        QuestionType.Emoji => Parse<EmojiData>(data) is var d ? $"{d.Emojis}|{d.Answer}" : "",
        QuestionType.Carriere or QuestionType.Mannetje => Describe(type, data).Answer,
        _ => Describe(type, data).Label,
    };

    /// <summary>Controles op de inhoud van een vraag uit de bank (naast de bron-eis bij goedkeuren).</summary>
    public static List<string> Problems(QuestionType type, string data)
    {
        try
        {
            return type switch
            {
                QuestionType.Snelvuur => Parse<SnelvuurData>(data) is var d
                    ? Check(!string.IsNullOrWhiteSpace(d.Question), "Vul de vraag in.")
                        .Concat(Check(d.Options.Count == 4 && d.Options.All(o => !string.IsNullOrWhiteSpace(o)), "Er zijn 4 antwoordopties nodig."))
                        .Concat(Check(d.Correct is >= 0 and <= 3, "Duid het juiste antwoord aan.")).ToList()
                    : [],
                QuestionType.Carriere => Parse<CarriereData>(data) is var d
                    ? Check(!string.IsNullOrWhiteSpace(d.Answer), "Vul de speler in.")
                        .Concat(Check(d.Clubs.Count(c => !string.IsNullOrWhiteSpace(c)) >= 3, "Geef minstens 3 clubs.")).ToList()
                    : [],
                QuestionType.NietBij => Parse<NietBijData>(data) is var d
                    ? Check(!string.IsNullOrWhiteSpace(d.Prompt), "Vul de vraag in.")
                        .Concat(Check(d.Options.Count == 4 && d.Options.All(o => !string.IsNullOrWhiteSpace(o)), "Er zijn 4 opties nodig."))
                        .Concat(Check(d.Odd is >= 0 and <= 3, "Duid de vreemde eend aan.")).ToList()
                    : [],
                QuestionType.Jaar => Parse<JaarData>(data) is var d
                    ? Check(!string.IsNullOrWhiteSpace(d.Event), "Beschrijf de gebeurtenis.")
                        .Concat(Check(d.Year is >= QuestionRules.MinYear and <= QuestionRules.MaxYear, $"Het jaar moet tussen {QuestionRules.MinYear} en {QuestionRules.MaxYear} liggen."))
                        .Concat(Check(!d.Event.Contains(d.Year.ToString()), "Het jaartal mag niet in de tekst staan.")).ToList()
                    : [],
                QuestionType.Emoji => Parse<EmojiData>(data) is var d
                    ? Check(!string.IsNullOrWhiteSpace(d.Emojis), "Vul de emoji's in.")
                        .Concat(Check(!string.IsNullOrWhiteSpace(d.Answer), "Vul het antwoord in.")).ToList()
                    : [],
                QuestionType.Opstelling => Parse<OpstellingData>(data) is var d
                    ? Check(!string.IsNullOrWhiteSpace(d.Title), "Geef de wedstrijd een titel.")
                        .Concat(Check(d.Players.Count == 11 && d.Players.All(p => !string.IsNullOrWhiteSpace(p.Name)), "Een basiself heeft 11 spelers.")).ToList()
                    : [],
                QuestionType.Wissel => Parse<WisselData>(data) is var d
                    ? Check(!string.IsNullOrWhiteSpace(d.Answer), "Vul de speler in.")
                        .Concat(Check(new[] { d.When, d.Fee, d.From, d.To, d.Extra }.All(x => !string.IsNullOrWhiteSpace(x)),
                            "Vul wanneer, bedrag, van, naar en de extra tip in.")).ToList()
                    : [],
                QuestionType.Rugnummer => Parse<RugnummerData>(data) is var d
                    ? Check(!string.IsNullOrWhiteSpace(d.Answer), "Vul de speler in.")
                        .Concat(Check(d.Number is >= 1 and <= 99, "Het rugnummer moet tussen 1 en 99 liggen."))
                        .Concat(Check(new[] { d.Position, d.Country, d.Club, d.Extra }.All(x => !string.IsNullOrWhiteSpace(x)),
                            "Vul positie, land, club en de extra tip in.")).ToList()
                    : [],
                QuestionType.Mannetje => Parse<MannetjeData>(data) is var d
                    ? Check(!string.IsNullOrWhiteSpace(d.Answer), "Vul de speler in.")
                        .Concat(Check(Avatars.IsValid(d.Avatar), "Kies een geldige avatar."))
                        .Concat(Check(d.Clues.Count(c => !string.IsNullOrWhiteSpace(c)) >= 2, "Geef minstens 2 tips.")).ToList()
                    : [],
                _ => ["Dit type hoort niet in de vragenbank."],
            };
        }
        catch (JsonException)
        {
            return ["De inhoud is ongeldig."];
        }
    }

    private static IEnumerable<string> Check(bool ok, string problem) => ok ? [] : [problem];
}
