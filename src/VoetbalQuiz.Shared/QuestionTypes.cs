namespace VoetbalQuiz.Shared;

public enum QuestionType { WieBenIk, Snelvuur, Carriere, NietBij, Jaar, Emoji, Opstelling, Wissel, Rugnummer, Mannetje, Kluts, Counter }

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
        new(QuestionType.Snelvuur, "Snelvuur", "⚡", "Kies het juiste antwoord uit 4. Je hebt 5 seconden.", 10, 100, "snelvuur"),
        new(QuestionType.Carriere, "Carrièrepad", "🧭", "De clubs verschijnen één voor één. Wie is de speler?", 10, 100, "carriere"),
        new(QuestionType.NietBij, "Wie hoort er niet bij?", "🧩", "Tik op de vreemde eend. Je hebt 6 seconden.", 10, 100, "niet-bij"),
        new(QuestionType.Jaar, "Raad het jaar", "📅", "Kies het jaartal. Hoe dichter, hoe meer punten.", 10, 100, "jaar"),
        new(QuestionType.Emoji, "Emoji-raadsel", "😀", "Welke speler of club zit er in deze emoji's?", 10, 100, "emoji"),
        new(QuestionType.Opstelling, "Opstelling", "📋", "Vul de ontbrekende spelers van een bekende basiself aan.", 5, 200, "opstelling"),
        new(QuestionType.Wissel, "Wissel", "🔄", "Een spraakmakende transfer, feit per feit. Wie maakte de overstap?", 10, 100, "wissel"),
        new(QuestionType.Rugnummer, "Rugnummer", "🔢", "Een rugnummer, dan positie, land en club. Wie droeg dit shirt?", 10, 100, "rugnummer"),
        new(QuestionType.Mannetje, "Mannetje", "👤", "Van zwart silhouet tot volledig mannetje. Herken jij de speler?", 10, 100, "mannetje"),
        new(QuestionType.Kluts, "Kluts in de zestien", "🌀", "De letters van een naam liggen door elkaar. Om de paar seconden valt er één juist.", 10, 100, "", FromPlayers: true),
        new(QuestionType.Counter, "Counter", "🏃", "Zoveel mogelijk spelers in 60 seconden, met één tip per speler. Raden of doorspelen!", 5, 200, "", FromPlayers: true),
    ];

    public static QuestionTypeInfo Get(QuestionType type) => All.First(t => t.Type == type);

    /// <summary>
    /// Hoeveel vragen van elk type een mix krijgt: om beurten één vraag per type, zolang de dag onder
    /// 1000 punten en 10 vragen blijft. Een opstelling (200 punten) telt dus voor twee gewone vragen.
    /// </summary>
    public static Dictionary<QuestionType, int> Mix(IEnumerable<QuestionType> types)
    {
        var list = types.Distinct().ToList();
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
    /// Een mix met een vast aantal vragen (een online tafel): om beurten één vraag per type,
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
    /// (het type van die dag staat uit), dan wordt het een mix van alle types die nog aan staan.
    /// </summary>
    public static List<QuestionType> ResolveDay(IEnumerable<QuestionType> planned, ICollection<QuestionType> disabled)
    {
        var types = planned.Where(t => !disabled.Contains(t)).Distinct().Order().ToList();
        if (types.Count == 0) types = All.Select(i => i.Type).Where(t => !disabled.Contains(t)).ToList();
        return types.Count == 0 ? [QuestionType.WieBenIk] : types;
    }

    /// <summary>"🕵️ ⚡ 📅" voor een mix.</summary>
    public static string Icons(IEnumerable<QuestionType> types) => string.Join(" ", types.Select(t => Get(t).Icon));
    /// <summary>"⚡ Snelvuur" of "🎲 Mix 🕵️ ⚡ 📅" voor de banner van een dag.</summary>
    public static string DayLabel(DailyOverview day) =>
        day.IsMix ? $"🎲 Mix {Icons(day.Types!)}" : $"{Get(day.Type).Icon} {Get(day.Type).Name}";

    /// <summary>Zelfde label voor een lijst types (bv. van een online tafel); leeg = "Wie ben ik?".</summary>
    public static string Label(IReadOnlyCollection<QuestionType>? types) => types switch
    {
        { Count: > 1 } => $"🎲 Mix {Icons(types)}",
        { Count: 1 } => $"{Get(types.First()).Icon} {Get(types.First()).Name}",
        _ => Label([QuestionType.WieBenIk]),
    };

    public static string DayHowTo(DailyOverview day) =>
        day.IsMix ? "Een mix van vraagtypes. Bij elke vraag zie je wat je moet doen." : Get(day.Type).HowTo;

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
    public const double SnelvuurSeconds = 5;
    public const double NietBijSeconds = 6;
    public const double JaarSeconds = 10;
    public const double EmojiSeconds = 15;
    public const double OpstellingSeconds = 60;
    public const double CounterSeconds = 60;

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

    public static double RevealLimit(int steps) => steps * CarriereClubSeconds + CarriereExtraSeconds;

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

    public static int OpstellingPoints(int found, int blanks) =>
        blanks == 0 ? 0 : (int)Math.Round(200.0 * found / blanks);
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

/// <summary>Een plaats in een opstelling zoals de speler hem ziet (Name null = nog in te vullen).</summary>
public record LineupSlot(string Position, string? Name, bool IsBlank);

/// <summary>Kluts in de zestien: één letter. Fixed = staat al op de juiste plaats.</summary>
public record LetterTile(string Letter, bool Fixed);
