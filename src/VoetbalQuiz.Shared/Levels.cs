namespace VoetbalQuiz.Shared;

/// <summary>
/// Wat een speler gedaan heeft, de basis voor XP. CorrectPoints: de punten van juiste antwoorden, plus alle punten van
/// types met deelpunten (<see cref="QuestionRules.PartialScores"/>: kaart, opstelling, counter), ook als die niet volledig juist zijn.
/// </summary>
public record XpInput(
    int QuestionsPlayed, int CorrectPoints, int DaysCompleted, int PerfectDays, int DaysWon,
    int MatchesPlayed = 0, int MatchesWon = 0);

/// <summary>
/// XP en levels. Spelen levert XP op, goed spelen nog meer. Elk level vraagt 100 XP meer dan het vorige
/// (level 2 = 100 XP, level 3 = 300, level 4 = 600, …), zonder maximum.
/// </summary>
public static class Levels
{
    public const int XpPerQuestion = 10;        // elke gespeelde vraag
    public const int CorrectPointsPerXp = 5;    // juist antwoord: punten / 5 (100 punten = 20 XP)
    public const int XpPerCompletedDay = 50;    // alle 10 vragen gespeeld
    public const int XpPerPerfectDay = 50;      // 10 op 10 juist
    public const int XpPerDayWon = 100;         // dagwinnaar
    public const int XpPerMatch = 25;           // online wedstrijd uitgespeeld
    public const int XpPerMatchWon = 75;        // online wedstrijd gewonnen

    public static List<XpPart> Breakdown(XpInput input) =>
    [
        new("Gespeelde vragen", input.QuestionsPlayed * XpPerQuestion),
        new("Juiste antwoorden", input.CorrectPoints / CorrectPointsPerXp),
        new("Volledige dagen", input.DaysCompleted * XpPerCompletedDay),
        new("Perfecte dagen", input.PerfectDays * XpPerPerfectDay),
        new("Dagzeges", input.DaysWon * XpPerDayWon),
        new("Online wedstrijden", input.MatchesPlayed * XpPerMatch),
        new("Online zeges", input.MatchesWon * XpPerMatchWon),
    ];

    public static int Xp(XpInput input) => Breakdown(input).Sum(p => p.Xp);

    /// <summary>Totale XP die nodig is om <paramref name="level"/> te bereiken.</summary>
    /// <summary>Nieuwe speler: nog op level 1 (een volledige daily brengt je al naar level 2). Home toont dan "Zo werkt het".</summary>
    public static bool IsNewcomer(int level) => level <= 1;

    public static int XpForLevel(int level) => 50 * level * (level - 1);

    public static LevelInfo For(int xp)
    {
        var level = 1;
        while (xp >= XpForLevel(level + 1)) level++;
        var start = XpForLevel(level);
        return new LevelInfo(level, Title(level), xp, xp - start, XpForLevel(level + 1) - start);
    }

    public static string Title(int level) => level switch
    {
        < 5 => "Jeugdspeler",
        < 10 => "Beloftenspeler",
        < 15 => "Basisspeler",
        < 20 => "Kapitein",
        < 30 => "Rode Duivel",
        < 40 => "Wereldklasse",
        _ => "Legende",
    };
}

public record XpPart(string Label, int Xp);

public record LevelInfo(int Level, string Title, int Xp, int XpIntoLevel, int XpForNextLevel);
