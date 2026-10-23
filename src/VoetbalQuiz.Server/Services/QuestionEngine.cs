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

    private static T Data<T>(IQuizQuestion q) => JsonSerializer.Deserialize<T>(q.QuizItem!.Data, Json)!;

    public static T Parse<T>(string data) => JsonSerializer.Deserialize<T>(data, Json)!;

    public static string Serialize<T>(T data) => JsonSerializer.Serialize(data, Json);

    /// <summary>Het juiste antwoord als tekst (voor overzichten en statistieken).</summary>
    public static string AnswerLabel(IQuizQuestion q) => q.Type switch
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
        QuestionType.Volgorde => string.Join(" → ", Data<VolgordeData>(q).Items.Select(i => i.Label)),
        QuestionType.Krant => Data<KrantData>(q).Headline,
        QuestionType.Detective => CulpritName(Data<DetectiveData>(q)),
        QuestionType.Kaart => Data<KaartData>(q) is var k
            ? $"{k.Competition} {k.Season}: {(IsStadiumPart(q) ? "de stadions" : "de clubs")}"
            : "",
        _ => "",
    };

    public static double TimeLimit(IQuizQuestion q) => q.Type switch
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
        QuestionType.Volgorde => QuestionRules.VolgordeSeconds,
        QuestionType.Krant => QuestionRules.KrantLimit(Krant(q).Gaps.Count),
        QuestionType.Detective => QuestionRules.DetectiveSeconds,
        QuestionType.Kaart => QuestionRules.KaartSeconds,
        _ => GameRules.TimeLimitSeconds,
    };

    /// <summary>Wat de speler nu ziet. Nooit het antwoord: dat komt pas in <see cref="Result"/>.</summary>
    public static (List<string> Hints, QuestionView View) Visible(IQuizQuestion q, AttemptState a, double elapsed)
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
                return ([], new QuestionView(d.Title, Lineup: Slots(d, q.Blanks, a.Found, revealAll: false),
                    MaxPoints: QuestionTypes.Get(QuestionType.Opstelling).MaxPoints));
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
            case QuestionType.Volgorde:
            {
                var d = Data<VolgordeData>(q);
                return ([], new QuestionView(d.Question, VolgordeShown(q).Select(i => d.Items[i].Label).ToList()));
            }
            case QuestionType.Krant:
            {
                var d = Data<KrantData>(q);
                var article = KrantText.Parse(d.Text);
                return ([], new QuestionView(d.Headline, Note: d.Intro, MaxPoints: QuestionTypes.Get(QuestionType.Krant).MaxPoints,
                    Found: article.Gaps.Count - KrantOpen(article, a), Total: article.Gaps.Count,
                    Article: KrantPieces(article, a, reveal: false), ArticleKind: d.Kind));
            }
            case QuestionType.Detective:
            {
                // Enkel de geopende verklaringen, nooit de dader of wie een verklaring uitsluit.
                var d = Data<DetectiveData>(q);
                var opened = DetectiveOpened(d, a);
                var wrong = a.Skipped.Count;
                return ([], new QuestionView(d.Title, Note: d.Intro, MaxPoints: QuestionTypes.Get(QuestionType.Detective).MaxPoints,
                    Step: opened, Steps: d.Clues.Count,
                    Detective: new DetectiveView(
                        d.Suspects.Select((s, i) => new DetectiveCard(s.Name, s.Card, a.Skipped.Contains(i))).ToList(),
                        d.Clues.Take(opened).Select(c => c.Text).ToList(), d.Clues.Count,
                        QuestionRules.DetectiveAccusations - wrong, QuestionRules.DetectivePoints(opened, wrong),
                        opened < d.Clues.Count ? QuestionRules.DetectivePoints(opened + 1, wrong) : null)));
            }
            case QuestionType.Kaart:
            {
                var d = Data<KaartData>(q);
                var stadiums = IsStadiumPart(q);
                var order = KaartOrder(q);
                var passed = Math.Min(a.Found.Count, order.Count);
                // Na afloop mag alles zichtbaar zijn (wat niet beantwoord werd, is fout); tijdens het spelen enkel de
                // beantwoorde spelden en de huidige: nooit het antwoord op de huidige, nooit waar de volgende staan.
                // Deel 1 noemt nooit een stadion (dat is de vraag van deel 2); deel 2 toont de clubs, want dat is zijn vraag.
                var over = a.AnsweredAt is not null;
                var pins = order.Select((pin, pos) => (Pin: d.Pins[pin], Pos: pos))
                    .Where(x => over || x.Pos <= passed)
                    .Select(x => x.Pos < passed || over
                        ? new KaartSpeld(x.Pin.Lat, x.Pin.Lon, KaartChoices(q, x.Pin), x.Pin.Clubs, KaartAnswer(q, x.Pin),
                            x.Pos < passed ? a.Picks.ElementAtOrDefault(x.Pos) ?? "" : null, x.Pos < passed ? a.Found[x.Pos] : 0)
                        : new KaartSpeld(x.Pin.Lat, x.Pin.Lon, KaartChoices(q, x.Pin), stadiums ? x.Pin.Clubs : null))
                    .ToList();
                var part = stadiums ? QuestionRules.KaartStadiumsPart : QuestionRules.KaartClubsPart;
                return ([], new QuestionView(stadiums ? $"{d.Competition} {d.Season}: in welk stadion speelt deze club thuis?" : d.Question,
                    KaartOptions(q, d),
                    MaxPoints: QuestionRules.KaartPartPoints,
                    Note: over || passed >= order.Count ? $"Deel {part} · {LandNaam(d)}" : $"Deel {part} · {LandNaam(d)} · speld {passed + 1} van {order.Count}",
                    Found: passed, Total: order.Count, Pins: pins, Part: part, Country: d.Land));
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
    public static int CounterCurrent(IQuizQuestion q, AttemptState a) =>
        Enumerable.Range(0, q.CounterPlayers.Count).FirstOrDefault(i => !a.Found.Contains(i) && !a.Skipped.Contains(i), -1);

    /// <summary>
    /// Op volgorde: in welke volgorde de kaartjes getoond worden (indexen in de juiste volgorde). Vast per vraag, zodat
    /// herladen niets verandert, en nooit al juist.
    /// </summary>
    public static List<int> VolgordeShown(IQuizQuestion q)
    {
        var count = Data<VolgordeData>(q).Items.Count;
        var rng = new Random(q.Id * 31 + 7);
        var shown = Enumerable.Range(0, count).ToList();
        for (var attempt = 0; attempt < 10 && shown.SequenceEqual(Enumerable.Range(0, count)); attempt++)
            shown = [.. Enumerable.Range(0, count).OrderBy(_ => rng.Next())];
        return shown;
    }

    /// <summary>
    /// Op volgorde: de gok "2,0,3,1" (de getoonde kaartjes in de aangetikte volgorde) als indexen in de juiste volgorde.
    /// Niet aangetikte kaartjes komen achteraan, in de getoonde volgorde. Null als de gok ongeldig is.
    /// </summary>
    public static List<int>? VolgordeOrder(IQuizQuestion q, string guess)
    {
        var shown = VolgordeShown(q);
        var tapped = new List<int>();
        foreach (var part in guess.Split(',', StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(part, out var i) || i < 0 || i >= shown.Count || tapped.Contains(i)) return null;
            tapped.Add(i);
        }
        if (tapped.Count == 0) return null;
        return [.. tapped.Concat(Enumerable.Range(0, shown.Count).Except(tapped)).Select(i => shown[i])];
    }

    private static KrantText.Article Krant(IQuizQuestion q) => KrantText.Parse(Data<KrantData>(q).Text);

    /// <summary>
    /// De Krant: de getallen die je al gokte, per gat. Bewaard als paren (gat, gok) in <see cref="AttemptState.Skipped"/>;
    /// een getal gok je maar één keer, anders zoek je het gewoon op en neer.
    /// </summary>
    public static Dictionary<int, int> KrantNumbers(AttemptState a)
    {
        var numbers = new Dictionary<int, int>();
        for (var i = 0; i + 1 < a.Skipped.Count; i += 2) numbers.TryAdd(a.Skipped[i], a.Skipped[i + 1]);
        return numbers;
    }

    /// <summary>De Krant: welk deel van de punten van gat <paramref name="i"/> je hebt (0..1).</summary>
    private static double KrantGapScore(KrantGap gap, int i, AttemptState a, Dictionary<int, int> numbers) =>
        gap.Number is { } answer
            ? numbers.TryGetValue(i, out var guess) ? QuestionRules.KrantNumberScore(guess, answer, gap.Tolerance) : 0
            : a.Found.Contains(i) ? 1 : 0;

    private static int KrantPointsOf(KrantText.Article article, AttemptState a)
    {
        var numbers = KrantNumbers(a);
        return QuestionRules.KrantPoints(article.Gaps.Select((g, i) => KrantGapScore(g, i, a, numbers)).Sum(), article.Gaps.Count);
    }

    /// <summary>De Krant: hoeveel gaten nog open zijn (een naam die nog niet gevonden is, een getal dat nog niet gegokt is).</summary>
    private static int KrantOpen(KrantText.Article article, AttemptState a)
    {
        var numbers = KrantNumbers(a);
        return article.Gaps.Where((g, i) => g.Number is null ? !a.Found.Contains(i) : !numbers.ContainsKey(i)).Count();
    }

    /// <summary>
    /// De Krant: de alinea's zoals de speler ze ziet. Een gat toont wat er al staat (een gevonden naam, een gegokt getal);
    /// pas met <paramref name="reveal"/> (na afloop) ook het juiste antwoord en de score.
    /// </summary>
    private static List<List<KrantPiece>> KrantPieces(KrantText.Article article, AttemptState a, bool reveal)
    {
        var numbers = KrantNumbers(a);
        return article.Paragraphs.Select(p => p.Select(s =>
        {
            if (s.Gap < 0) return new KrantPiece(s.Text);
            var gap = article.Gaps[s.Gap];
            var filled = gap.Number is null
                ? a.Found.Contains(s.Gap) ? gap.Answer : null
                : numbers.TryGetValue(s.Gap, out var guess) ? guess.ToString() : null;
            return new KrantPiece("", s.Gap, gap.Hint, gap.Number is not null, filled,
                reveal ? gap.Answer : null, reveal ? KrantGapScore(gap, s.Gap, a, numbers) : null);
        }).ToList()).ToList();
    }

    /// <summary>
    /// Detectivedag: hoeveel verklaringen open staan. Bewaard in <see cref="AttemptState.Choice"/> (null = enkel die van
    /// bij de start); de fout beschuldigde verdachten staan in <see cref="AttemptState.Skipped"/>, de juiste in Found.
    /// </summary>
    public static int DetectiveOpened(DetectiveData d, AttemptState a) =>
        Math.Clamp(a.Choice ?? QuestionRules.DetectiveOpenAtStart, Math.Min(QuestionRules.DetectiveOpenAtStart, d.Clues.Count), d.Clues.Count);

    private static string CulpritName(DetectiveData d) => d.Suspects.FirstOrDefault(s => s.Key == d.Culprit)?.Name ?? "";

    /// <summary>Detectivedag: per verdachte de eerste verklaring die hem uitsluit (index), of -1 (de dader).</summary>
    public static int DetectiveExcludedBy(DetectiveData d, string key) => d.Clues.FindIndex(c => c.Excludes.Contains(key));

    /// <summary>
    /// Kaart: in welke volgorde de spelden verschijnen (indexen in de data). Geschud, maar vast per vraag: voor iedereen
    /// die dezelfde daily speelt dezelfde volgorde, en herladen verandert niets. De twee delen zijn twee vragen (eigen id),
    /// dus deel 2 heeft een andere volgorde dan deel 1.
    /// Hangt af van het aantal spelden: daarom wijzigt de inhoud van een kaart niet meer zodra ze in een reeks zit
    /// (<see cref="Found"/> en <see cref="AttemptState.Picks"/> zijn posities in deze volgorde).
    /// </summary>
    public static List<int> KaartOrder(IQuizQuestion q)
    {
        var count = Data<KaartData>(q).Pins.Count;
        var rng = new Random(q.Id * 31 + 11);
        return [.. Enumerable.Range(0, count).OrderBy(_ => rng.Next())];
    }

    /// <summary>Kaart: de naam van het land van de kaart ("Frankrijk", "het Verenigd Koninkrijk").</summary>
    private static string LandNaam(KaartData d) => KaartProjection.Find(d.Land)?.Naam ?? d.Land;

    /// <summary>Kaart: deel 2 (bij de clubs het stadion zoeken)? Anders deel 1.</summary>
    private static bool IsStadiumPart(IQuizQuestion q) => q.Part == QuestionRules.KaartStadiumsPart;

    /// <summary>Kaart: alle clubs van de competitie, alfabetisch. De keuzelijst toont ze altijd allemaal, ook de geplaatste.</summary>
    public static List<string> KaartClubs(KaartData d) => Alphabetical(d.Pins.SelectMany(p => p.Clubs));

    /// <summary>Kaart: alle stadions van de kaart (het veld stadium), alfabetisch. De keuzelijst van deel 2.</summary>
    public static List<string> KaartStadiums(KaartData d) => Alphabetical(d.Pins.Select(p => p.Stadium));

    private static List<string> Alphabetical(IEnumerable<string> names) =>
        names.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.InvariantCultureIgnoreCase).ToList();

    private static List<string> KaartOptions(IQuizQuestion q, KaartData d) => IsStadiumPart(q) ? KaartStadiums(d) : KaartClubs(d);

    /// <summary>Kaart: hoeveel keuzelijsten een speld heeft: één per club in deel 1, één stadion in deel 2.</summary>
    private static int KaartChoices(IQuizQuestion q, KaartStadion pin) => IsStadiumPart(q) ? 1 : pin.Clubs.Count;

    /// <summary>Kaart: het juiste antwoord op een speld: de club(s) in deel 1, het stadion in deel 2.</summary>
    private static List<string> KaartAnswer(IQuizQuestion q, KaartStadion pin) => IsStadiumPart(q) ? [pin.Stadium] : pin.Clubs;

    /// <summary>Kaart: hoeveel er in totaal juist kan zijn: alle clubs (deel 1, een gedeeld stadion telt per club) of alle spelden (deel 2).</summary>
    private static int KaartTotal(IQuizQuestion q, KaartData d) => d.Pins.Sum(p => KaartChoices(q, p));

    /// <summary>
    /// Kaart: hoeveel van het antwoord ("Club Brugge|Cercle Brugge" in deel 1, een stadion in deel 2) bij deze speld hoort,
    /// en het antwoord zoals het in de keuzelijst staat. Overslaan = 0. Null als het antwoord ongeldig is: leeg, meer keuzes
    /// dan de speld heeft, of iets dat niet in de keuzelijst staat.
    /// </summary>
    private static (int Hits, string Pick)? KaartHits(IQuizQuestion q, KaartData d, KaartStadion pin, string answer)
    {
        if (answer == QuestionRules.KaartSkip) return (0, "");
        var chosen = answer.Split(QuestionRules.KaartSeparator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var options = KaartOptions(q, d);
        if (chosen.Length == 0 || chosen.Length > KaartChoices(q, pin)
            || chosen.Any(c => !options.Contains(c, StringComparer.OrdinalIgnoreCase))) return null;
        var picked = chosen.Select(c => options.First(o => string.Equals(o, c, StringComparison.OrdinalIgnoreCase))).ToList();
        // Twee keer dezelfde club kiezen bij een gedeeld stadion telt maar één keer.
        var right = KaartAnswer(q, pin);
        return (picked.Distinct(StringComparer.OrdinalIgnoreCase).Count(c => right.Contains(c, StringComparer.OrdinalIgnoreCase)),
            string.Join(QuestionRules.KaartSeparator, picked));
    }

    /// <summary>Kaart: "3:Genk" → speld 3 en het antwoord "Genk". Null zonder geldig nummer.</summary>
    private static (int Pin, string Answer)? KaartParse(string guess) =>
        guess.Split(QuestionRules.KaartPinPrefix, 2) is [var number, var answer] && int.TryParse(number, out var pin)
            ? (pin, answer.Trim())
            : null;

    /// <summary>Counter: de duidelijkste tip (de laatste) van een speler.</summary>
    private static string CounterClue(Player p) => p.Hints.LastOrDefault() ?? "";

    /// <summary>
    /// De uitkomst van een poging. Ignored: de poging telt niet (een kaartgok voor een andere speld dan de huidige, of een
    /// ongeldig antwoord): ze wijzigt niets en telt niet mee voor <see cref="QuestionRules.MaxGuesses"/>.
    /// </summary>
    public record Outcome(bool Done, bool Correct, int Points, bool Wrong, bool Ignored = false);

    /// <summary>Beoordeelt een poging. Getypte types mogen opnieuw proberen; keuzetypes zijn meteen klaar.</summary>
    public static Outcome Guess(IQuizQuestion q, AttemptState a, string guess, double elapsed)
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

            case QuestionType.Volgorde:
            {
                if (VolgordeOrder(q, guess) is not { } order) return new(false, false, 0, false);
                a.Found = order;
                var points = QuestionRules.VolgordePoints(order);
                return new(true, points == QuestionTypes.Get(QuestionType.Volgorde).MaxPoints, points, false);
            }

            case QuestionType.Krant:
            {
                // De gok is "gat|tekst": de speler kiest zelf welk gat hij invult. Fout kost niets (zoals bij een opstelling).
                var article = Krant(q);
                var before = KrantPointsOf(article, a);
                var bar = guess.IndexOf('|');
                if (bar < 0 || !int.TryParse(guess[..bar], out var i) || i < 0 || i >= article.Gaps.Count)
                    return new(false, false, before, false);
                var text = guess[(bar + 1)..].Trim();
                var gap = article.Gaps[i];
                if (gap.Number is null)
                {
                    if (a.Found.Contains(i) || !AnswerMatcher.IsMatch(text, gap.Answer, gap.Aliases)) return new(false, false, before, false);
                    a.Found = [.. a.Found, i];
                }
                else
                {
                    if (KrantNumbers(a).ContainsKey(i) || KrantText.ParseNumber(text) is not { } number) return new(false, false, before, false);
                    a.Skipped = [.. a.Skipped, i, number];
                }
                var points = KrantPointsOf(article, a);
                var done = KrantOpen(article, a) == 0;
                return new(done, done && points == QuestionTypes.Get(QuestionType.Krant).MaxPoints, points, false);
            }

            case QuestionType.Detective:
            {
                // "/getuige" opent de volgende verklaring; een getal beschuldigt die verdachte. Schrappen blijft bij de client.
                var d = Data<DetectiveData>(q);
                var opened = DetectiveOpened(d, a);
                if (guess == QuestionRules.DetectiveNextClue)
                {
                    if (opened < d.Clues.Count) a.Choice = opened + 1;
                    return new(false, false, 0, false);
                }
                if (!int.TryParse(guess, out var i) || i < 0 || i >= d.Suspects.Count || a.Skipped.Contains(i))
                    return new(false, false, 0, false);
                if (d.Suspects[i].Key == d.Culprit)
                {
                    a.Found = [i];
                    return new(true, true, QuestionRules.DetectivePoints(opened, a.Skipped.Count), false);
                }
                a.Skipped = [.. a.Skipped, i];
                return new(a.Skipped.Count >= QuestionRules.DetectiveAccusations, false, 0, true);
            }

            case QuestionType.Kaart:
            {
                // Found: per beantwoorde speld (in de geschudde volgorde) hoeveel juist was; Picks: wat de speler koos.
                // Een antwoord is definitief. Een gok voor een andere speld dan de huidige (dubbele tik) of een ongeldig
                // antwoord telt niet: niets wijzigt en het is geen poging.
                var d = Data<KaartData>(q);
                var order = KaartOrder(q);
                var total = KaartTotal(q, d);
                var points = QuestionRules.KaartPoints(a.Found.Sum(), total);
                if (a.Found.Count >= order.Count) return new(true, a.Found.Sum() == total, points, false);

                if (KaartParse(guess) is not { } g || g.Pin != a.Found.Count + 1
                    || KaartHits(q, d, d.Pins[order[a.Found.Count]], g.Answer) is not { } hit)
                    return new(false, false, points, false, Ignored: true);
                a.Found = [.. a.Found, hit.Hits];
                a.Picks = [.. a.Picks, hit.Pick];
                var done = a.Found.Count == order.Count;
                var right = a.Found.Sum();
                return new(done, done && right == total, QuestionRules.KaartPoints(right, total), false);
            }

            default:
                return new(false, false, 0, false);
        }
    }

    /// <summary>
    /// Derby (Trump booster): een kopie van een keuzevraag waarin <paramref name="choice"/> (optie of jaartal) het juiste antwoord is.
    /// Niet opgeslagen: enkel om opnieuw te beoordelen en het "officiële" antwoord te tonen.
    /// </summary>
    public static IQuizQuestion WithAnswer(IQuizQuestion q, int choice)
    {
        var data = q.Type switch
        {
            QuestionType.Snelvuur => Serialize(Data<SnelvuurData>(q) with { Correct = choice }),
            QuestionType.NietBij => Serialize(Data<NietBijData>(q) with { Odd = choice }),
            QuestionType.Jaar => Serialize(Data<JaarData>(q) with { Year = choice }),
            _ => q.QuizItem!.Data,
        };
        var item = q.QuizItem!;
        return new JudgedQuestion(q.Id, q.Number, q.Type,
            new QuizItem { Id = item.Id, Type = item.Type, Key = item.Key, Data = data, Difficulty = item.Difficulty });
    }

    /// <summary>De lichte kopie van <see cref="WithAnswer"/>: enkel een vraag uit de bank, zonder spelers of opstelling.</summary>
    private sealed record JudgedQuestion(int Id, int Number, QuestionType Type, QuizItem? QuizItem) : IQuizQuestion
    {
        public Player? Player => null;
        public List<int> Blanks { get; } = [];
        public List<Player> CounterPlayers { get; } = [];
        public int Part => 0;
    }

    /// <summary>Een getypte naam bij een type dat stap voor stap onthult (wissel, rugnummer, mannetje, kluts).</summary>
    private static Outcome Typed(string guess, string answer, IEnumerable<string> aliases, double elapsed, AttemptState a) =>
        AnswerMatcher.IsMatch(guess, answer, aliases)
            ? new(true, true, QuestionRules.RevealPointsAt(elapsed, QuestionRules.RevealSteps, a.WrongGuesses), false)
            : new(false, false, 0, true);

    /// <summary>Punten als de tijd om is of de speler stopt: een opstelling en een counter geven deelpunten.</summary>
    public static int PointsWhenStopped(IQuizQuestion q, AttemptState a) => q.Type switch
    {
        QuestionType.Opstelling => QuestionRules.OpstellingPoints(a.Found.Count, q.Blanks.Count),
        QuestionType.Counter => QuestionRules.CounterPoints(a.Found.Count),
        QuestionType.Krant => KrantPointsOf(Krant(q), a),
        QuestionType.Kaart => QuestionRules.KaartPoints(a.Found.Sum(), KaartTotal(q, Data<KaartData>(q))),
        _ => 0,
    };

    /// <summary>Wat er na afloop getoond wordt: het juiste antwoord en de uitleg.</summary>
    public static AnswerResult Result(IQuizQuestion q, AttemptState a)
    {
        // CloseIfExpired sluit een verlopen vraag af op precies StartedAt + de tijdslimiet; passen of antwoorden gebeurt eerder.
        var timedOut = !a.Correct && a.AnsweredAt is { } at && (at - a.StartedAt).TotalSeconds >= TimeLimit(q) - 0.001
                       // Kaart: wie alle spelden binnen de speling beantwoordde, liep niet tegen de tijd aan.
                       && (q.Type != QuestionType.Kaart || a.Found.Count < Data<KaartData>(q).Pins.Count);
        var baseResult = new AnswerResult(a.Correct, a.Points, AnswerLabel(q), a.WrongGuesses, [], TimedOut: timedOut);
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
            QuestionType.Volgorde => Data<VolgordeData>(q) is var v
                ? baseResult with
                {
                    // De juiste volgorde; ✅ waar jij hetzelfde kaartje op die plaats legde.
                    AllHints = v.Items.Select((item, i) =>
                        (a.Found.Count == 0 ? "" : a.Found.ElementAtOrDefault(i) == i ? "✅ " : "❌ ") + $"{item.Label} · {item.Value}").ToList(),
                    Explanation = a.Found.Count == 0 ? null : $"{a.Points} van de {QuestionTypes.Get(QuestionType.Volgorde).MaxPoints} punten: hoe dichter bij de juiste volgorde, hoe meer",
                    MyOrder = a.Found.Count == 0 ? null : a.Found,
                }
                : baseResult,
            QuestionType.Krant => Krant(q) is var k
                ? baseResult with
                {
                    FullArticle = KrantPieces(k, a, reveal: true),
                    Explanation = $"{k.Gaps.Where((g, i) => KrantGapScore(g, i, a, KrantNumbers(a)) >= 1).Count()} van de {k.Gaps.Count} gaten juist",
                }
                : baseResult,
            QuestionType.Detective => Data<DetectiveData>(q) is var d
                ? baseResult with
                {
                    AllHints = d.Clues.Select(c => c.Text).ToList(),
                    Explanation = d.Solution,
                    Dossier = d.Suspects.Select((s, i) =>
                    {
                        var clue = DetectiveExcludedBy(d, s.Key);
                        return new DetectiveVerdict(s.Name, s.Card, s.Key == d.Culprit, a.Skipped.Contains(i) || a.Found.Contains(i),
                            clue < 0 ? null : clue + 1, clue < 0 ? null : d.Clues[clue].Explain?.GetValueOrDefault(s.Key));
                    }).ToList(),
                }
                : baseResult,
            QuestionType.Kaart => Data<KaartData>(q) is var k
                ? baseResult with
                {
                    // Elke speld in de volgorde waarin ze verscheen: ✅ juist, 🟡 deels (gedeeld stadion), ❌ fout, ⌛ niet aan
                    // toe gekomen; bij een fout ook wat de speler koos. Deel 1 noemt geen stadion (dat vraagt deel 2).
                    AllHints = KaartOrder(q).Select((pin, pos) => KaartLine(q, k.Pins[pin], pos, a)).ToList(),
                    Explanation = IsStadiumPart(q)
                        ? $"{a.Found.Sum()} van de {KaartTotal(q, k)} stadions juist"
                        : $"{a.Found.Sum()} van de {KaartTotal(q, k)} clubs juist geplaatst",
                    MyPicks = a.Picks.Count == 0 ? null : a.Picks,
                }
                : baseResult,
            _ => baseResult,
        };
    }

    /// <summary>
    /// Kaart: één regel van het resultaat, bv. "❌ Genk · Genk (jij: Standard)" in deel 1 of
    /// "✅ Club Brugge en Cercle Brugge · Jan Breydelstadion" in deel 2.
    /// </summary>
    private static string KaartLine(IQuizQuestion q, KaartStadion p, int pos, AttemptState a)
    {
        var clubs = string.Join(" en ", p.Clubs);
        // Deel 1 toont de stad, behalve als die al in de clubnaam zit ("Kortrijk · Kortrijk" zegt niets extra).
        var where = IsStadiumPart(q) ? $" · {p.Stadium}"
            : p.Clubs.Any(c => c.Contains(p.City, StringComparison.OrdinalIgnoreCase)) ? ""
            : $" · {p.City}";
        if (pos >= a.Found.Count) return $"⌛ {clubs}{where}";
        var hits = a.Found[pos];
        var mark = hits == KaartChoices(q, p) ? "✅ " : hits > 0 ? "🟡 " : "❌ ";
        var pick = a.Picks.ElementAtOrDefault(pos) ?? "";
        var mine = hits == KaartChoices(q, p) ? ""
            : pick.Length == 0 ? " (overgeslagen)"
            : $" (jij: {pick.Replace(QuestionRules.KaartSeparator.ToString(), " en ")})";
        return $"{mark}{clubs}{where}{mine}";
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
        QuestionType.Volgorde => Parse<VolgordeData>(data) is var d ? (d.Question, string.Join(" → ", d.Items.Select(i => i.Label))) : ("", ""),
        QuestionType.Krant => Parse<KrantData>(data) is var d ? (d.Headline, $"{KrantText.Parse(d.Text).Gaps.Count} gaten") : ("", ""),
        QuestionType.Detective => Parse<DetectiveData>(data) is var d ? (d.Title, CulpritName(d)) : ("", ""),
        QuestionType.Kaart => Parse<KaartData>(data) is var d
            ? ($"{d.Land} · {d.Competition} {d.Season} ({d.Pins?.Count ?? 0} spelden)", string.Join(", ", (d.Pins ?? []).SelectMany(p => p.Clubs ?? [])))
            : ("", ""),
        _ => ("", ""),
    };

    /// <summary>Unieke sleutel van een vraag (zelfde als in de JSON-seed).</summary>
    public static string KeyFor(QuestionType type, string data) => type switch
    {
        QuestionType.Emoji => Parse<EmojiData>(data) is var d ? $"{d.Emojis}|{d.Answer}" : "",
        QuestionType.Carriere or QuestionType.Mannetje => Describe(type, data).Answer,
        QuestionType.Volgorde => Describe(type, data) is var v ? $"{v.Label}|{v.Answer}" : "",
        // Eén kaart per land, competitie en seizoen ("BE|Jupiler Pro League|2026-27"). Een kaart die al in een reeks zit,
        // verandert niet meer van spelden of clubs (zie AdminService.SaveQuizItemAsync en QuestionSeeder): een gewijzigde kaart
        // krijgt een nieuwe sleutel (bv. een ander seizoen), en de oude verdwijnt dan uit de bestanden en gaat op afgewezen.
        QuestionType.Kaart => Parse<KaartData>(data) is var d ? $"{d.Land}|{d.Competition}|{d.Season}" : "",
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
                QuestionType.Volgorde => Parse<VolgordeData>(data) is var d
                    ? Check(!string.IsNullOrWhiteSpace(d.Question), "Vul de opdracht in.")
                        .Concat(Check(d.Items.Count == QuestionRules.VolgordeCards
                            && d.Items.All(i => !string.IsNullOrWhiteSpace(i.Label) && !string.IsNullOrWhiteSpace(i.Value)),
                            $"Er zijn {QuestionRules.VolgordeCards} kaartjes nodig, elk met een tekst en een waarde."))
                        .Concat(Check(d.Items.Select(i => i.Label.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == d.Items.Count,
                            "Twee kaartjes hebben dezelfde tekst."))
                        .Concat(Check(d.Items.Select(i => i.Value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == d.Items.Count,
                            "Twee kaartjes hebben dezelfde waarde: dan is de volgorde niet eenduidig.")).ToList()
                    : [],
                QuestionType.Krant => Parse<KrantData>(data) is var d && KrantText.Parse(d.Text) is var k
                    ? Check(!string.IsNullOrWhiteSpace(d.Headline), "Geef het artikel een kop.")
                        .Concat(Check(d.Kind is "verslag" or "interview" or "dossier", "Kies de soort: verslag, interview of dossier."))
                        .Concat(k.Problems)
                        .Concat(Check(k.Gaps.Count == QuestionRules.KrantGaps,
                            $"Een artikel heeft precies {QuestionRules.KrantGaps} gaten (nu {k.Gaps.Count})."))
                        .Concat(Check(k.Gaps.All(g => !string.IsNullOrWhiteSpace(g.Hint)), "Geef elk gat een hint (bv. [[Vertonghen : speler]])."))
                        .Concat(KrantLeaks(d, k)).ToList()
                    : [],
                QuestionType.Detective => DetectiveProblems(Parse<DetectiveData>(data)).ToList(),
                QuestionType.Kaart => Parse<KaartData>(data) is var d ? KaartProblems(d) : [],
                _ => ["Dit type hoort niet in de vragenbank."],
            };
        }
        catch (JsonException)
        {
            return ["De inhoud is ongeldig."];
        }
    }

    private static List<string> KaartProblems(KaartData d)
    {
        var pins = d.Pins ?? [];
        var clubs = pins.SelectMany(p => p.Clubs ?? []).Select(c => c.Trim()).ToList();
        // Binnen de kaart zelf (LandKaart: de speld valt nooit van de kaart) én binnen de grens van het land (KaartGrenzen,
        // met een kleine marge voor stadions aan de kust): Rijsel op de kaart van België of een tekenfout in de lengtegraad
        // valt zo af.
        var land = KaartProjection.Find(d.Land);
        List<string> outside = land is null ? [] : pins
            .Where(p => !(land.Contains(p.Lat, p.Lon) && KaartGrenzen.Contains(land.Code, p.Lat, p.Lon)))
            .Select(p => p.Stadium).ToList();
        return Check(!string.IsNullOrWhiteSpace(d.Question), "Vul de vraag in.")
            .Concat(Check(!string.IsNullOrWhiteSpace(d.Competition) && !string.IsNullOrWhiteSpace(d.Season), "Vul de competitie en het seizoen in."))
            .Concat(Check(KaartProjection.Find(d.Land) is not null,
                $"Onbekend land '{d.Land}': kies een van {string.Join(", ", KaartProjection.Landen.Select(l => l.Code))}."))
            .Concat(Check(pins.Count == QuestionRules.KaartPins, $"Een kaart heeft precies {QuestionRules.KaartPins} spelden (nu {pins.Count})."))
            .Concat(Check(pins.All(p => !string.IsNullOrWhiteSpace(p.Stadium) && p.Clubs is { Count: > 0 } && p.Clubs.All(c => !string.IsNullOrWhiteSpace(c))),
                "Elke speld heeft een stadion en minstens één club."))
            // Een naam is een keuze in de lijst en gaat zo terug in de gok: geen scheidingsteken, geen spaties rond.
            .Concat(Check(pins.SelectMany(p => (p.Clubs ?? []).Append(p.Stadium ?? ""))
                    .All(n => !n.Contains(QuestionRules.KaartSeparator) && n == n.Trim()),
                $"Een club- of stadionnaam bevat '{QuestionRules.KaartSeparator}' of begint of eindigt met een spatie."))
            .Concat(Check(outside.Count == 0,
                $"Buiten {land?.Naam}: {string.Join(", ", outside)}. Controleer de coördinaten."))
            .Concat(Check(clubs.Distinct(StringComparer.OrdinalIgnoreCase).Count() == clubs.Count,
                "Een club staat bij twee spelden: elke club hoort bij één stadion."))
            // Deel 2 kiest het stadion uit een lijst: twee spelden met dezelfde naam zijn niet uit elkaar te houden.
            .Concat(Check(pins.Select(p => p.Stadium?.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == pins.Count,
                "Twee spelden hebben dezelfde stadionnaam.")).ToList();
    }

    private static IEnumerable<string> Check(bool ok, string problem) => ok ? [] : [problem];

    /// <summary>
    /// Detectivedag: de zaak moet waterdicht zijn. Na alle verklaringen blijft precies de dader over, geen verklaring sluit
    /// hem uit en elke verklaring sluit iemand uit. Na de verklaringen van bij de start blijven er minstens twee over.
    /// </summary>
    private static IEnumerable<string> DetectiveProblems(DetectiveData d)
    {
        var suspects = d.Suspects ?? [];
        var clues = d.Clues ?? [];
        var keys = suspects.Select(s => s.Key?.Trim() ?? "").ToList();
        if (string.IsNullOrWhiteSpace(d.Title)) yield return "Geef de zaak een titel.";
        if (string.IsNullOrWhiteSpace(d.Intro)) yield return "Schrijf het verhaaltje.";
        if (string.IsNullOrWhiteSpace(d.Solution)) yield return "Schrijf de uitleg over de dader voor het dossier.";
        if (suspects.Count is < QuestionRules.DetectiveMinSuspects or > QuestionRules.DetectiveMaxSuspects)
            yield return $"Een zaak heeft {QuestionRules.DetectiveMinSuspects} tot {QuestionRules.DetectiveMaxSuspects} verdachten (nu {suspects.Count}).";
        if (keys.Any(string.IsNullOrEmpty) || suspects.Any(s => string.IsNullOrWhiteSpace(s.Name)))
            yield return "Elke verdachte heeft een sleutel en een naam.";
        if (keys.Distinct().Count() != keys.Count) yield return "Twee verdachten hebben dezelfde sleutel.";
        if (!keys.Contains(d.Culprit)) { yield return "De dader staat niet bij de verdachten."; yield break; }
        if (clues.Count is < QuestionRules.DetectiveMinClues or > QuestionRules.DetectiveMaxClues)
            yield return $"Een zaak heeft {QuestionRules.DetectiveMinClues} tot {QuestionRules.DetectiveMaxClues} verklaringen (nu {clues.Count}).";

        for (var i = 0; i < clues.Count; i++)
        {
            var c = clues[i];
            var excludes = c.Excludes ?? [];
            if (string.IsNullOrWhiteSpace(c.Text)) yield return $"Verklaring {i + 1} heeft geen tekst.";
            if (excludes.Count == 0) yield return $"Verklaring {i + 1} sluit niemand uit.";
            if (excludes.Contains(d.Culprit)) yield return $"Verklaring {i + 1} sluit de dader zelf uit.";
            foreach (var unknown in excludes.Where(k => !keys.Contains(k)))
                yield return $"Verklaring {i + 1} sluit \"{unknown}\" uit, maar die staat niet bij de verdachten.";
        }

        var excluded = clues.SelectMany(c => c.Excludes ?? []).ToHashSet();
        var left = keys.Where(k => !excluded.Contains(k)).ToList();
        if (left.Count != 1)
            yield return $"Na alle verklaringen blijven er {left.Count} verdachten over ({string.Join(", ", left)}); dat moet enkel de dader zijn.";

        var leftAtStart = keys.Count - clues.Take(QuestionRules.DetectiveOpenAtStart).SelectMany(c => c.Excludes ?? []).Distinct().Count();
        if (leftAtStart < 2) yield return "Na de verklaringen van bij de start is de dader al gekend; maak die vager.";
    }

    /// <summary>
    /// De Krant: antwoorden die de speler al ziet vóór hij begint, in de kop, het intro, een hint of de gewone tekst.
    /// Telt als los woord ("69" niet in "1969"); getallen van één cijfer niet in de gewone tekst (een "3-2" mag).
    /// </summary>
    private static IEnumerable<string> KrantLeaks(KrantData d, KrantText.Article k)
    {
        static bool Has(string text, string word) =>
            System.Text.RegularExpressions.Regex.IsMatch(text, $@"(?<!\w){System.Text.RegularExpressions.Regex.Escape(word)}(?!\w)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        var top = $"{d.Headline} {d.Intro}";
        var plain = string.Join(" ", k.Paragraphs.SelectMany(p => p).Where(s => s.Gap < 0).Select(s => s.Text));
        foreach (var gap in k.Gaps)
            foreach (var answer in gap.Aliases.Prepend(gap.Answer).Where(a => a.Length > 0))
            {
                if (Has(top, answer)) yield return $"\"{answer}\" staat al in de kop of het intro.";
                else if (Has(gap.Hint, answer)) yield return $"De hint van een gat verraadt \"{answer}\".";
                else if ((gap.Number is null || answer.Length > 1) && Has(plain, answer))
                    yield return $"\"{answer}\" staat ook als gewone tekst in het artikel.";
            }
    }
}
