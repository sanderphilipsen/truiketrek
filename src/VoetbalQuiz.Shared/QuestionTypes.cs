namespace VoetbalQuiz.Shared;

public enum QuestionType { WieBenIk, Snelvuur, Carriere, NietBij, Jaar, Emoji, Opstelling, Wissel, Rugnummer, Mannetje, Kluts, Counter, Volgorde, Kaart }

/// <summary>
/// Vaste eigenschappen per vraagtype. Een dag is altijd maximaal 1000 punten waard.
/// FromPlayers: de vragen komen uit de spelersdatabase (zoals "Wie ben ik?"), niet uit de vragenbank.
/// </summary>
public record QuestionTypeInfo(
    QuestionType Type, string Name, string Icon, string HowTo, int PerDay, int MaxPoints, string FileName, bool FromPlayers = false);

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
        new(QuestionType.Kaart, "Kaart", "🗺️",
            $"Twee delen op dezelfde kaart: eerst kies je bij elke speld de club, daarna bij elke club het stadion. " +
            $"Per deel {(int)QuestionRules.KaartSeconds / 60} minuten en {QuestionRules.KaartPartPoints} punten.",
            QuestionRules.KaartParts, QuestionRules.KaartPartPoints, "kaart"),
    ];

    public static QuestionTypeInfo Get(QuestionType type) => All.First(t => t.Type == type);

    /// <summary>
    /// Hoeveel spelers of vragen uit de bank <paramref name="questions"/> vragen van dit type nodig hebben: een counter
    /// vraagt <see cref="QuestionRules.CounterPlayers"/> spelers per vraag, de twee delen van een kaartdag delen één kaart.
    /// </summary>
    public static int ItemsFor(QuestionType type, int questions) => type switch
    {
        QuestionType.Counter => questions * QuestionRules.CounterPlayers,
        QuestionType.Kaart => (questions + QuestionRules.KaartParts - 1) / QuestionRules.KaartParts,
        _ => questions,
    };

    /// <summary>
    /// Solo-types vullen in hun eentje een hele dag (de kaart is 1000 punten): ze passen niet in een mix, niet in een
    /// special met andere types en niet online (zie <see cref="MatchRules.AllowedOnline"/>).
    /// </summary>
    public static bool IsSolo(QuestionType type) => type == QuestionType.Kaart;

    /// <summary>
    /// De solo-regel voor de weekplanning en de specials: een solo-type staat altijd alleen. Geeft de foutmelding voor het
    /// admin-paneel, of null als de keuze klopt. Komt er toch een combinatie binnen (oude data), dan wint het solo-type
    /// (zie <see cref="Mix(IEnumerable{QuestionType})"/>): het valt nooit stilletjes weg.
    /// </summary>
    public static string? SoloProblem(IEnumerable<QuestionType> types)
    {
        var list = types.Distinct().ToList();
        return list.Count > 1 && list.Where(IsSolo).ToList() is [var solo, ..]
            ? $"{Get(solo).Name} is een dag op zich: combineer het niet met andere vraagtypes."
            : null;
    }

    /// <summary>Zit er een solo-type tussen, dan enkel dat (het eerste); anders de types zelf.</summary>
    private static List<QuestionType> SoloOrAll(List<QuestionType> types) =>
        types.Where(IsSolo).Take(1).ToList() is [_] solo ? solo : types;

    /// <summary>
    /// Hoeveel vragen van elk type een mix krijgt: om beurten één vraag per type, zolang de dag onder
    /// 1000 punten en 10 vragen blijft. Een opstelling (200 punten) telt dus voor twee gewone vragen.
    /// Een solo-type (de kaart) is de hele dag: zit het erbij, dan wordt het enkel dat type.
    /// </summary>
    public static Dictionary<QuestionType, int> Mix(IEnumerable<QuestionType> types)
    {
        var list = SoloOrAll(types.Distinct().ToList());
        if (list.Count == 0) list = [QuestionType.WieBenIk];
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
        var list = types.Distinct().ToList();
        if (list.Count == 0) list = [QuestionType.WieBenIk];
        return list.Select((t, i) => (t, n: questions / list.Count + (i < questions % list.Count ? 1 : 0)))
            .Where(x => x.n > 0).ToDictionary(x => x.t, x => x.n);
    }

    public const int DayMaxPoints = 1000;
    /// <summary>
    /// Wat een dag werkelijk wordt: de geplande types zonder de uitgeschakelde. Blijft er niets over
    /// (het type van die dag staat uit), dan wordt het een mix van alle types die nog aan staan, zonder de solo-types.
    /// Een solo-type (de kaart) staat altijd alleen: zit het in de planning, dan is de dag enkel dat type.
    /// </summary>
    public static List<QuestionType> ResolveDay(IEnumerable<QuestionType> planned, ICollection<QuestionType> disabled)
    {
        var types = SoloOrAll(planned.Where(t => !disabled.Contains(t)).Distinct().Order().ToList());
        if (types.Count == 0) types = All.Select(i => i.Type).Where(t => !disabled.Contains(t) && !IsSolo(t)).ToList();
        return types.Count == 0 ? [QuestionType.WieBenIk] : types;
    }

    /// <summary>
    /// De types van een dag volgens de weekplanning: de mix als er meer dan één type gekozen is, anders het ene type
    /// (geen planning = <see cref="DefaultFor"/>), en dan zoals <see cref="ResolveDay"/>.
    /// </summary>
    public static List<QuestionType> ResolveSchedule(
        QuestionType? type, IReadOnlyCollection<QuestionType>? mixTypes, DayOfWeek day, ICollection<QuestionType> disabled) =>
        ResolveDay(mixTypes is { Count: > 1 } ? mixTypes : [type ?? DefaultFor(day)], disabled);

    /// <summary>
    /// De types van een special, zonder de uitgeschakelde. Geen keuze = "Wie ben ik?"; staan ze allemaal uit, dan toch
    /// de gekozen types (een special wordt niet plots een mix van iets anders). Een solo-type (de kaart) staat altijd
    /// alleen: zit het erbij, dan is de special enkel dat type. Een solo-type dat uit staat, wordt nooit "toch" gespeeld:
    /// een special met enkel de kaart wordt dan "Wie ben ik?".
    /// </summary>
    public static List<QuestionType> ResolveSpecial(IReadOnlyCollection<QuestionType> types, ICollection<QuestionType> disabled)
    {
        IReadOnlyCollection<QuestionType> chosen = types.Count > 0 ? types : [QuestionType.WieBenIk];
        var enabled = SoloOrAll(chosen.Where(t => !disabled.Contains(t)).Distinct().Order().ToList());
        if (enabled.Count > 0) return enabled;
        var fallback = chosen.Where(t => !IsSolo(t)).Distinct().ToList();
        return fallback.Count > 0 ? fallback : [QuestionType.WieBenIk];
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
        _ => QuestionType.Opstelling,
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

    /// <summary>
    /// Kaart: een kaartdag bestaat uit twee delen (twee vragen op dezelfde kaart). Deel 1 (<see cref="KaartClubsPart"/>):
    /// bij elke speld de club(s) kiezen. Deel 2 (<see cref="KaartStadiumsPart"/>): bij elke club(s) het stadion kiezen.
    /// </summary>
    public const int KaartParts = 2;
    public const int KaartClubsPart = 1, KaartStadiumsPart = 2;

    /// <summary>Kaart: zoveel punten per deel; de twee delen samen zijn de hele dag (<see cref="QuestionTypes.DayMaxPoints"/>).</summary>
    public const int KaartPartPoints = QuestionTypes.DayMaxPoints / KaartParts;

    /// <summary>Kaart: de tijd voor één deel (alle spelden samen). Daarna zijn de resterende spelden fout.</summary>
    public const double KaartSeconds = 180;

    /// <summary>Kaart: deze "gok" slaat de huidige speld over (telt als fout).</summary>
    public const string KaartSkip = "/overslaan";

    /// <summary>Kaart: scheidt de gekozen clubs bij een gedeeld stadion ("Club Brugge|Cercle Brugge").</summary>
    public const char KaartSeparator = '|';

    /// <summary>
    /// Kaart: elke gok begint met het nummer van de speld (1 = de eerste): "3:Club Brugge|Cercle Brugge" of "3:/overslaan".
    /// Een gok voor een andere speld dan de huidige (een dubbele tik, een oude request) doet niets.
    /// </summary>
    public const char KaartPinPrefix = ':';

    /// <summary>De gok voor speld <paramref name="pin"/> (1 = de eerste) met dit antwoord.</summary>
    public static string KaartGuess(int pin, string answer) => $"{pin}{KaartPinPrefix}{answer}";

    /// <summary>
    /// Types met deelpunten: de vraag is zelden helemaal juist, maar wat je vindt, telt (kaart, opstelling, counter).
    /// Hun punten tellen mee voor de XP ("Juiste antwoorden") ook als de vraag niet volledig juist is, en het dagoverzicht
    /// toont een deelscore als deels juist (½) in plaats van fout.
    /// </summary>
    public static bool PartialScores(QuestionType type) =>
        type is QuestionType.Kaart or QuestionType.Opstelling or QuestionType.Counter;

    /// <summary>Kaart: een kaart heeft minstens zoveel spelden.</summary>
    public const int KaartMinPins = 4;

    /// <summary>
    /// Kaart: en hoogstens zoveel. Elke geldige gok zet één speld vast, dus ruim onder
    /// <see cref="GameRules.MaxGuessesPerQuestion"/>: een kaart kan nooit vastlopen op het maximum aantal pogingen.
    /// </summary>
    public const int KaartMaxPins = 24;

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

    /// <summary>
    /// Kaart: de punten van één deel (<see cref="KaartPartPoints"/>) naar rato, naar beneden afgerond. Deel 1: juiste clubs
    /// van alle clubs (een gedeeld stadion telt per club). Deel 2: juiste stadions van alle spelden.
    /// </summary>
    public static int KaartPoints(int right, int total) =>
        total <= 0 ? 0 : (int)Math.Floor((double)KaartPartPoints * Math.Clamp(right, 0, total) / total);
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

/// <summary>Kaart: een stadion met de club(s) die er thuis spelen (meer dan één bij een gedeeld stadion).</summary>
public record KaartStadion(string Stadium, string City, double Lat, double Lon, List<string> Clubs);

/// <summary>Kaart: één competitie op de kaart. De server schudt de spelden en geeft ze één voor één vrij.</summary>
public record KaartData(string Question, string Competition, string Season, List<KaartStadion> Pins);

/// <summary>Een plaats in een opstelling zoals de speler hem ziet (Name null = nog in te vullen).</summary>
public record LineupSlot(string Position, string? Name, bool IsBlank);

/// <summary>Kluts in de zestien: één letter. Fixed = staat al op de juiste plaats.</summary>
public record LetterTile(string Letter, bool Fixed);
