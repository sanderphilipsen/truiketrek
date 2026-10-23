namespace VoetbalQuiz.Shared;

public enum QuestionType { WieBenIk, Snelvuur, Carriere, NietBij, Jaar, Emoji, Opstelling, Wissel, Rugnummer, Mannetje, Kluts, Counter, Volgorde, Krant, Detective }

/// <summary>
/// Vaste eigenschappen per vraagtype. Een dag is altijd maximaal 1000 punten waard.
/// FromPlayers: de vragen komen uit de spelersdatabase (zoals "Wie ben ik?"), niet uit de vragenbank.
/// Alone: het type vult een dag op zich (zoals De Krant, de rubriek "De Zondag"). Het komt nooit in een mix of online.
/// </summary>
public record QuestionTypeInfo(
    QuestionType Type, string Name, string Icon, string HowTo, int PerDay, int MaxPoints, string FileName, bool FromPlayers = false,
    bool Alone = false);

public static class QuestionTypes
{
    public static readonly List<QuestionTypeInfo> All =
    [
        new(QuestionType.WieBenIk, "Wie ben ik?", "🕵️", "Een speler beschrijft zichzelf in 5 tips. Typ zijn naam.", 10, 100, "", FromPlayers: true),
        new(QuestionType.Snelvuur, "Snelvuur", "⚡", "Kies het juiste antwoord uit 4. Je hebt 15 seconden.", 10, 100, "snelvuur"),
        new(QuestionType.Carriere, "Carrièrepad", "🧭", "De clubs verschijnen één voor één. Wie is de speler?", 10, 100, "carriere"),
        new(QuestionType.NietBij, "Wie hoort er niet bij?", "🧩", "Tik op de vreemde eend. Je hebt 15 seconden.", 10, 100, "niet-bij"),
        new(QuestionType.Jaar, "Raad het jaar", "📅", "Kies het jaartal. Hoe dichter, hoe meer punten.", 10, 100, "jaar"),
        new(QuestionType.Emoji, "Emoji-raadsel", "😀", "Welke speler of club zit er in deze emoji's?", 10, 100, "emoji"),
        new(QuestionType.Opstelling, "Opstelling", "📋", "Vul de ontbrekende spelers van een bekende basiself aan.", 5, 200, "opstelling"),
        new(QuestionType.Wissel, "Wissel", "🔄", "Een spraakmakende transfer, feit per feit. Wie maakte de overstap?", 10, 100, "wissel"),
        new(QuestionType.Rugnummer, "Rugnummer", "🔢", "Een rugnummer, dan positie, land en club. Wie droeg dit shirt?", 10, 100, "rugnummer"),
        new(QuestionType.Mannetje, "Mannetje", "👤", "Van zwart silhouet tot volledig mannetje. Herken jij de speler?", 10, 100, "mannetje"),
        new(QuestionType.Kluts, "Kluts in de zestien", "🌀", "De letters van een naam liggen door elkaar. Om de paar seconden valt er één juist.", 10, 100, "", FromPlayers: true),
        new(QuestionType.Counter, "Counter", "🏃", "Zoveel mogelijk spelers in 60 seconden, met één tip per speler. Raden of doorspelen!", 5, 200, "", FromPlayers: true),
        new(QuestionType.Volgorde, "Op volgorde", "🔀", "Tik de kaartjes aan in de juiste volgorde. Hoe dichter, hoe meer punten.", 10, 100, "volgorde"),
        new(QuestionType.Krant, "De Zondag", "📰", "Elke zondag vier krantenartikels met elk vijf gaten. Tik op een gat en vul het in, in de volgorde die je wil.", 4, 250, "krant",
            Alone: true),
        new(QuestionType.Detective, "Detectivedag", "🔎", "Wie is de dader? Open de getuigen één voor één, schrap verdachten en wijs de dader aan. Hoe minder getuigen, hoe meer punten.",
            1, 1000, "detective", Alone: true),
    ];

    public static QuestionTypeInfo Get(QuestionType type) => All.First(t => t.Type == type);

    /// <summary>
    /// Hoeveel vragen van elk type een mix krijgt: om beurten één vraag per type, zolang de dag onder
    /// 1000 punten en 10 vragen blijft. Een opstelling (200 punten) telt dus voor twee gewone vragen.
    /// </summary>
    public static Dictionary<QuestionType, int> Mix(IEnumerable<QuestionType> types)
    {
        var list = Playable(types);
        var mix = list.ToDictionary(t => t, _ => 0);
        int points = 0, count = 0;
        for (var added = true; added;)
        {
            added = false;
            foreach (var t in list)
            {
                var max = Get(t).MaxPoints;
                if (count >= GameRules.QuestionsPerDay || points + max > DayMaxPoints) continue;
                mix[t]++;
                points += max;
                count++;
                added = true;
            }
        }
        return mix.Where(m => m.Value > 0).ToDictionary(m => m.Key, m => m.Value);
    }

    /// <summary>
    /// Een mix met een vast aantal vragen (een online partijtje): om beurten één vraag per type,
    /// zonder puntenplafond want online telt niet voor de daglimiet.
    /// </summary>
    public static Dictionary<QuestionType, int> Mix(IEnumerable<QuestionType> types, int questions)
    {
        var list = Playable(types);
        return list.Select((t, i) => (t, n: questions / list.Count + (i < questions % list.Count ? 1 : 0)))
            .Where(x => x.n > 0).ToDictionary(x => x.t, x => x.n);
    }

    /// <summary>
    /// De types die samen kunnen: een type dat alleen een dag vult (<see cref="QuestionTypeInfo.Alone"/>) valt weg zodra er
    /// een ander type bij is: zo'n type speel je enkel als dag op zich (bv. De Zondag, of een special met enkel dat type).
    /// </summary>
    private static List<QuestionType> Playable(IEnumerable<QuestionType> types)
    {
        var list = types.Distinct().ToList();
        if (list.Any(t => !Get(t).Alone)) list = list.Where(t => !Get(t).Alone).ToList();
        else if (list.Count > 1) list = list.Take(1).ToList();
        return list.Count == 0 ? [QuestionType.WieBenIk] : list;
    }

    public const int DayMaxPoints = 1000;
    /// <summary>
    /// Wat een dag werkelijk wordt: de geplande types zonder de uitgeschakelde. Blijft er niets over
    /// (het type van die dag staat uit), dan wordt het een mix van alle types die nog aan staan.
    /// </summary>
    public static List<QuestionType> ResolveDay(IEnumerable<QuestionType> planned, ICollection<QuestionType> disabled)
    {
        var types = planned.Where(t => !disabled.Contains(t)).Distinct().Order().ToList();
        if (types.Count == 0) types = All.Select(i => i.Type).Where(t => !disabled.Contains(t)).ToList();
        return types.Count == 0 ? [QuestionType.WieBenIk] : types;
    }

    /// <summary>
    /// De types van een dag volgens de weekplanning: de mix als er meer dan één type gekozen is, anders het ene type
    /// (geen planning = <see cref="DefaultFor"/>), en dan zoals <see cref="ResolveDay"/>.
    /// </summary>
    public static List<QuestionType> ResolveSchedule(
        QuestionType? type, IReadOnlyCollection<QuestionType>? mixTypes, DayOfWeek day, ICollection<QuestionType> disabled) =>
        day == ZondagDay ? ResolveDay([QuestionType.Krant], disabled)
            : ResolveDay(mixTypes is { Count: > 1 } ? mixTypes : [type ?? DefaultFor(day)], disabled);

    /// <summary>
    /// De vaste rubriek "De Zondag": de daily van elke zondag is altijd De Krant, wat de planning ook zegt. Staat het type
    /// uit, dan wordt het een mix van de andere types (zoals bij <see cref="ResolveDay"/>).
    /// </summary>
    public const DayOfWeek ZondagDay = DayOfWeek.Sunday;

    /// <summary>
    /// De types van een special, zonder de uitgeschakelde. Geen keuze = "Wie ben ik?"; staan ze allemaal uit, dan toch
    /// de gekozen types (een special wordt niet plots een mix van iets anders).
    /// </summary>
    public static List<QuestionType> ResolveSpecial(IReadOnlyCollection<QuestionType> types, ICollection<QuestionType> disabled)
    {
        IReadOnlyCollection<QuestionType> chosen = types.Count > 0 ? types : [QuestionType.WieBenIk];
        var enabled = chosen.Where(t => !disabled.Contains(t)).Distinct().Order().ToList();
        return enabled.Count > 0 ? enabled : chosen.Distinct().ToList();
    }

    /// <summary>"🕵️ ⚡ 📅" voor een mix.</summary>
    public static string Icons(IEnumerable<QuestionType> types) => string.Join(" ", types.Select(t => Get(t).Icon));
    /// <summary>"⚡ Snelvuur" of "🎲 Mix 🕵️ ⚡ 📅" voor de banner van een dag.</summary>
    public static string DayLabel(DailyOverview day) =>
        day.IsMix ? $"🎲 Mix {Icons(day.Types!)}" : $"{Get(day.Type).Icon} {Get(day.Type).Name}";

    /// <summary>Hetzelfde zonder iconen ("Snelvuur" of "Mix"), voor de rustige daily-tegel op Home.</summary>
    public static string DayName(DailyOverview day) => day.IsMix ? "Mix" : Get(day.Type).Name;

    /// <summary>Enkel de naam van een lijst types, zonder iconen: "Snelvuur" of "Mix"; leeg = "Wie ben ik?".</summary>
    public static string Name(IReadOnlyCollection<QuestionType>? types) => types switch
    {
        { Count: > 1 } => "Mix",
        { Count: 1 } => Get(types.First()).Name,
        _ => Get(QuestionType.WieBenIk).Name,
    };

    /// <summary>Zelfde label voor een lijst types (bv. van een online partijtje); leeg = "Wie ben ik?".</summary>
    public static string Label(IReadOnlyCollection<QuestionType>? types) => types switch
    {
        { Count: > 1 } => $"🎲 Mix {Icons(types)}",
        { Count: 1 } => $"{Get(types.First()).Icon} {Get(types.First()).Name}",
        _ => Label([QuestionType.WieBenIk]),
    };

    /// <summary>Standaard weekplanning (aanpasbaar in het admin-paneel).</summary>
    public static QuestionType DefaultFor(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => QuestionType.WieBenIk,
        DayOfWeek.Tuesday => QuestionType.Snelvuur,
        DayOfWeek.Wednesday => QuestionType.Carriere,
        DayOfWeek.Thursday => QuestionType.NietBij,
        DayOfWeek.Friday => QuestionType.Emoji,
        DayOfWeek.Saturday => QuestionType.Jaar,
        _ => QuestionType.Krant, // zondag: de vaste rubriek "De Zondag"
    };

    public static readonly DayOfWeek[] Week =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];

    public static string DayName(DayOfWeek d) => d switch
    {
        DayOfWeek.Monday => "Maandag",
        DayOfWeek.Tuesday => "Dinsdag",
        DayOfWeek.Wednesday => "Woensdag",
        DayOfWeek.Thursday => "Donderdag",
        DayOfWeek.Friday => "Vrijdag",
        DayOfWeek.Saturday => "Zaterdag",
        _ => "Zondag",
    };
}

/// <summary>Timing en puntentelling van de nieuwe vraagtypes. "Wie ben ik?" gebruikt <see cref="GameRules"/>.</summary>
public static class QuestionRules
{
    /// <summary>Geen enkele vraag geeft minder tijd dan dit.</summary>
    public const double MinSeconds = 15;

    public const double SnelvuurSeconds = 15;
    public const double NietBijSeconds = 15;
    public const double JaarSeconds = 15;
    public const double EmojiSeconds = 15;
    public const double OpstellingSeconds = 60;
    public const double CounterSeconds = 60;
    public const double VolgordeSeconds = 30;

    /// <summary>Op volgorde: zoveel kaartjes per vraag.</summary>
    public const int VolgordeCards = 4;

    /// <summary>
    /// Op volgorde: zo lang vóór het einde stuurt de app een onafgewerkte volgorde zelf door. Binnen de speling van de
    /// server (<see cref="GameRules.LatencyGraceSeconds"/>), zodat ze ook bij een trage verbinding nog meetelt.
    /// </summary>
    public const double VolgordeAutoSendSeconds = GameRules.LatencyGraceSeconds - 0.1;

    /// <summary>Counter: zoveel spelers per vraag, elk goed voor <see cref="CounterPointsPerPlayer"/> punten.</summary>
    public const int CounterPlayers = 10;
    public const int CounterPointsPerPlayer = 20;

    /// <summary>Counter: deze "gok" betekent doorspelen naar de volgende speler (niemand typt dit als naam).</summary>
    public const string CounterSkip = "/volgende";

    /// <summary>Wissel, rugnummer, mannetje en kluts onthullen in 5 stappen, net als de clubs van een carrièrepad.</summary>
    public const int RevealSteps = 5;

    /// <summary>Carrièrepad: elke zoveel seconden een club erbij, daarna nog even tijd.</summary>
    public const double CarriereClubSeconds = 4;
    public const double CarriereExtraSeconds = 6;

    /// <summary>De Krant: zoveel seconden per gat, voor één klok over het hele artikel.</summary>
    public const double KrantSecondsPerGap = 20;
    public const double KrantMinSeconds = 60;

    /// <summary>De Krant: elk artikel heeft precies zoveel gaten (4 artikels × 5 gaten × 50 punten = 1000).</summary>
    public const int KrantGaps = 5;

    /// <summary>
    /// De Krant: zoveel pogingen voor het hele artikel. Een foute naam kost niets, dus je probeert er meer dan bij één
    /// vraag; het blijft een rem op scripts die namen afvuren.
    /// </summary>
    public const int KrantMaxGuesses = 100;

    /// <summary>Detectivedag: één klok voor de hele zaak.</summary>
    public const double DetectiveSeconds = 180;

    /// <summary>Detectivedag: zoveel verklaringen staan open bij de start (met nul kan je enkel gokken).</summary>
    public const int DetectiveOpenAtStart = 2;

    /// <summary>Detectivedag: elke verklaring na de eerste <see cref="DetectiveOpenAtStart"/> kost zoveel punten, tot dit minimum.</summary>
    public const int DetectivePointsPerClue = 150;
    public const int DetectiveMinPoints = 250;

    /// <summary>Detectivedag: zoveel keer mag je iemand beschuldigen. Na een foute beschuldiging halveren de punten.</summary>
    public const int DetectiveAccusations = 2;

    /// <summary>Detectivedag: zoveel verdachten en verklaringen heeft een zaak.</summary>
    public const int DetectiveMinSuspects = 6;
    public const int DetectiveMaxSuspects = 8;
    public const int DetectiveMinClues = 4;
    public const int DetectiveMaxClues = 8;

    /// <summary>Detectivedag: deze "gok" roept de volgende getuige op; een getal is een beschuldiging (de verdachte).</summary>
    public const string DetectiveNextClue = "/getuige";

    /// <summary>Opstelling: zoveel namen van de elf moet je aanvullen.</summary>
    public const int OpstellingBlanks = 6;

    public const int MinYear = 1970;
    public const int MaxYear = 2026;

    /// <summary>Spelers van vóór 2005 hebben dit tijdperk (<c>Player.Era</c>); de andere zijn "modern".</summary>
    public const string ClassicEra = "classic";

    /// <summary>Een daily (of special) heeft maximaal zoveel klassiekers bij de spelersvragen.</summary>
    public const int MaxClassicPerDay = 2;

    /// <summary>
    /// Types waarbij het antwoord het onderwerp is (een speler of club): die komt nooit twee keer voor in één reeks
    /// (bv. een feit- én een rebus-raadsel over dezelfde speler). Bij de andere types mag een antwoord terugkomen.
    /// </summary>
    public static bool AnswerIsSubject(QuestionType type) =>
        type is QuestionType.Emoji or QuestionType.Carriere or QuestionType.Wissel or QuestionType.Rugnummer or QuestionType.Mannetje;

    /// <summary>Juist antwoord: 100 bij een bliksemsnel antwoord, 50 op de valreep.</summary>
    public static int SpeedPoints(double elapsed, double limit) =>
        (int)Math.Round(50 + 50 * Math.Clamp(1 - Math.Max(0, elapsed - GameRules.LatencyGraceSeconds) / limit, 0, 1));

    /// <summary>
    /// Types die stap voor stap onthullen (carrièrepad, wissel, rugnummer, mannetje, kluts):
    /// elke <see cref="CarriereClubSeconds"/> seconden een stap erbij, daarna nog even tijd.
    /// </summary>
    public static bool Reveals(QuestionType type) =>
        type is QuestionType.Carriere or QuestionType.Wissel or QuestionType.Rugnummer or QuestionType.Mannetje or QuestionType.Kluts;

    public static int RevealStep(double elapsed, int steps) =>
        Math.Clamp(1 + (int)Math.Floor(Math.Max(0, elapsed) / CarriereClubSeconds), 1, steps);

    public static double RevealLimit(int steps) => Math.Max(MinSeconds, steps * CarriereClubSeconds + CarriereExtraSeconds);

    /// <summary>100 bij de eerste stap, 10 minder per extra stap, minimum 50; min strafpunten.</summary>
    public static int RevealPoints(int step, int wrongGuesses) =>
        Math.Max(0, Math.Max(50, 100 - (step - 1) * 10) - wrongGuesses * GameRules.WrongGuessPenalty);

    public static int RevealPointsAt(double elapsed, int steps, int wrongGuesses) =>
        RevealPoints(RevealStep(Math.Max(0, elapsed - GameRules.LatencyGraceSeconds), steps), wrongGuesses);

    public static int CarriereVisible(double elapsed, int clubs) => RevealStep(elapsed, clubs);

    public static double CarriereLimit(int clubs) => RevealLimit(clubs);

    /// <summary>100 bij de eerste club, 10 minder per extra club, minimum 50; min strafpunten.</summary>
    public static int CarrierePoints(double elapsed, int clubs, int wrongGuesses) => RevealPointsAt(elapsed, clubs, wrongGuesses);

    public static int CounterPoints(int found) => found * CounterPointsPerPlayer;

    /// <summary>Zoveel pogingen mag je op één vraag doen; daarna stopt ze (zie <see cref="GameRules.MaxGuessesPerQuestion"/>).</summary>
    public static int MaxGuesses(QuestionType type) => type == QuestionType.Krant ? KrantMaxGuesses : GameRules.MaxGuessesPerQuestion;

    public static double KrantLimit(int gaps) => Math.Max(KrantMinSeconds, gaps * KrantSecondsPerGap);

    /// <summary>
    /// De Krant, een getal: welk deel van de punten van het gat. Exact 1, één stap ernaast 0,75, twee 0,5, drie 0,25
    /// (zoals "Raad het jaar"). Een stap is <paramref name="tolerance"/> (bv. 1 minuut, of 5 bij een bedrag in miljoen).
    /// </summary>
    public static double KrantNumberScore(int guess, int answer, int tolerance)
    {
        var steps = (int)Math.Ceiling(Math.Abs((long)guess - answer) / (double)Math.Max(1, tolerance));
        return steps switch { 0 => 1, 1 => 0.75, 2 => 0.5, 3 => 0.25, _ => 0 };
    }

    /// <summary>De Krant: het maximum verdeeld over de gaten; <paramref name="score"/> is de som van de delen per gat.</summary>
    public static int KrantPoints(double score, int gaps) =>
        gaps == 0 ? 0 : (int)Math.Round(QuestionTypes.Get(QuestionType.Krant).MaxPoints * score / gaps);

    /// <summary>
    /// Detectivedag: wat de dader aanwijzen nu oplevert. 1000 met enkel de verklaringen van bij de start, 150 minder per
    /// extra verklaring (minimum 250), de helft na een foute beschuldiging.
    /// </summary>
    public static int DetectivePoints(int opened, int wrongAccusations)
    {
        if (wrongAccusations >= DetectiveAccusations) return 0;
        var points = Math.Max(DetectiveMinPoints,
            QuestionTypes.Get(QuestionType.Detective).MaxPoints - Math.Max(0, opened - DetectiveOpenAtStart) * DetectivePointsPerClue);
        return wrongAccusations == 0 ? points : points / 2;
    }

    public static int EmojiPoints(double elapsed, int wrongGuesses) =>
        Math.Max(0, SpeedPoints(elapsed, EmojiSeconds) - wrongGuesses * GameRules.WrongGuessPenalty);

    /// <summary>Exact juist 100, één jaar ernaast 75, twee 50, drie 25.</summary>
    public static int JaarPoints(int guess, int year) => Math.Abs(guess - year) switch
    {
        0 => 100,
        1 => 75,
        2 => 50,
        3 => 25,
        _ => 0,
    };

    /// <summary>Opstelling: het maximum (alle namen gevonden) naar rato van de gevonden namen.</summary>
    public static int OpstellingPoints(int found, int blanks) =>
        blanks == 0 ? 0 : (int)Math.Round((double)QuestionTypes.Get(QuestionType.Opstelling).MaxPoints * found / blanks);

    /// <summary>
    /// Op volgorde: het maximum naar rato van de paren die in de juiste volgorde staan. <paramref name="order"/> is de
    /// gekozen volgorde als indexen in de juiste volgorde (0 hoort eerst). Twee buren omwisselen kost zo maar één paar.
    /// </summary>
    public static int VolgordePoints(IReadOnlyList<int> order)
    {
        var pairs = order.Count * (order.Count - 1) / 2;
        if (pairs == 0) return 0;
        var right = 0;
        for (var i = 0; i < order.Count; i++)
            for (var j = i + 1; j < order.Count; j++)
                if (order[i] < order[j]) right++;
        return (int)Math.Round((double)QuestionTypes.Get(QuestionType.Volgorde).MaxPoints * right / pairs);
    }
}

// ---------- Inhoud van de vragenbank (zoals in Data/questions/*.json) ----------

public record SnelvuurData(string Question, List<string> Options, int Correct);
public record CarriereData(string Answer, List<string> Aliases, List<string> Clubs);
public record NietBijData(string Prompt, List<string> Options, int Odd, string Explanation);
public record JaarData(string Event, int Year);
public record EmojiData(string Emojis, string Answer, List<string> Aliases, string Kind, string Explanation);
public record LineupPlayer(string Name, List<string> Aliases, string Position);
public record OpstellingData(string Title, string Team, List<LineupPlayer> Players);

/// <summary>Een spraakmakende transfer. Extra: nog een tip over de speler (zonder zijn naam).</summary>
public record WisselData(string Answer, List<string> Aliases, string When, string Fee, string From, string To, string Extra);

/// <summary>Een rugnummer met wie het droeg. Club: waar (en wanneer) hij dat nummer droeg.</summary>
public record RugnummerData(string Answer, List<string> Aliases, int Number, string Position, string Country, string Club, string Extra);

/// <summary>Een getekend mannetje: Avatar is een avatar-id (bv. "debruyne") of een zelfgemaakte code ("c1:…").</summary>
public record MannetjeData(string Answer, List<string> Aliases, string Avatar, List<string> Clues);

/// <summary>Op volgorde: een kaartje. Value (bv. "€ 115 mln" of "1986") wordt pas na afloop getoond.</summary>
public record VolgordeItem(string Label, string Value);

/// <summary>Op volgorde: de opdracht en de kaartjes in de juiste volgorde (de server husselt ze).</summary>
public record VolgordeData(string Question, List<VolgordeItem> Items);

/// <summary>
/// De Krant: een artikel. Text bevat de gaten als [[antwoord / ook goed : hint]] of, voor een getal, [[#69 : minuut]]
/// (met een stap: [[#115~5 : miljoen euro]]). Een lege lijn begint een nieuwe alinea. Kind: verslag, interview of dossier.
/// </summary>
public record KrantData(string Kind, string Headline, string Intro, string Text);

/// <summary>
/// De Krant: een stuk van een alinea zoals de speler het ziet. Gap = -1 is gewone tekst, anders een gat met een hint.
/// Filled: wat er al staat (een gevonden naam, of het getal dat je gokte). Na afloop ook Answer en Score (0..1).
/// </summary>
public record KrantPiece(string Text, int Gap = -1, string? Hint = null, bool IsNumber = false, string? Filled = null,
    string? Answer = null, double? Score = null);

/// <summary>Detectivedag: een verdachte. Key verwijst ernaar vanuit de verklaringen; Card is een korte regel (positie, periode).</summary>
public record DetectiveSuspect(string Key, string Name, string Card);

/// <summary>
/// Detectivedag: een getuigenverklaring, een voetbalfeit over de dader. Excludes: de verdachten (keys) die ze uitsluit, door
/// de redactie ingevuld en niet berekend. Explain: per uitgesloten verdachte waarom (voor het dossier achteraf).
/// </summary>
public record DetectiveClue(string Text, List<string> Excludes, Dictionary<string, string>? Explain = null);

/// <summary>
/// Detectivedag: één zaak. Story is het sjabloon (bv. "gestolen-trofee"), Intro het verhaaltje, Clues in vaste volgorde
/// van vaag naar scherp, Culprit de key van de dader en Solution de uitleg over de dader in het dossier.
/// </summary>
public record DetectiveData(string Story, string Title, string Intro, List<DetectiveSuspect> Suspects, string Culprit,
    List<DetectiveClue> Clues, string Solution);

/// <summary>Detectivedag: een verdachte zoals de speler hem ziet. Accused: al (fout) beschuldigd.</summary>
public record DetectiveCard(string Name, string Card, bool Accused);

/// <summary>
/// Detectivedag tijdens het spelen: de verdachten, de geopende verklaringen, hoeveel beschuldigingen je nog hebt en wat
/// de dader aanwijzen nu oplevert (PointsNext: na nog een verklaring; null als er geen meer is).
/// </summary>
public record DetectiveView(List<DetectiveCard> Suspects, List<string> Clues, int CluesTotal, int AccusationsLeft, int PointsNow,
    int? PointsNext);

/// <summary>Detectivedag, het dossier achteraf: per verdachte de eerste verklaring (1..n) die hem uitsloot, en waarom.</summary>
public record DetectiveVerdict(string Name, string Card, bool IsCulprit, bool Accused, int? Clue, string? Reason);

/// <summary>Een plaats in een opstelling zoals de speler hem ziet (Name null = nog in te vullen).</summary>
public record LineupSlot(string Position, string? Name, bool IsBlank);

/// <summary>Kluts in de zestien: één letter. Fixed = staat al op de juiste plaats.</summary>
public record LetterTile(string Letter, bool Fixed);
