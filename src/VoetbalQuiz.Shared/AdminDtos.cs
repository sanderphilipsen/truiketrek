namespace VoetbalQuiz.Shared;

// ---------- Spelersbeheer ----------

/// <summary>Enkel goedgekeurde spelers komen in de quiz. Nieuwe of twijfelachtige spelers staan op "te keuren".</summary>
public enum PlayerStatus { Review, Approved, Rejected }

public record PlayerListItem(
    int Id, string Name, PlayerStatus Status, string Country, string Competition, string Era, int Difficulty,
    List<string> Tags, int Sources, DateOnly? CheckedAt, int TimesUsed, DateOnly? LastUsed, string? ReviewNote);

public record PlayerEdit(
    int Id, string Name, List<string> Answers, string Country, string Competition, string Era, int Difficulty,
    List<string> Hints, List<string> Sources, List<string> Tags, PlayerStatus Status, string? ReviewNote,
    DateOnly? CheckedAt, string? UpdatedBy, int TimesUsed);

public record PlayerCounts(int Review, int Approved, int Rejected);

/// <summary>Startscherm van het admin-paneel: wat vraagt aandacht?</summary>
public record AdminDashboard(
    PlayerCounts Players, int NewFeedback, int OpenFeedback,
    List<SpecialAdminItem> ActiveSpecials, List<SpecialAdminItem> UpcomingSpecials,
    Dictionary<string, int> TagCounts, int DailyDaysLeft);

/// <summary>Thema's om spelers te groeperen, o.a. voor specials.</summary>
public static class PlayerTags
{
    public static readonly (string Key, string Label)[] All =
    [
        ("champions-league", "Champions League-winnaar"),
        ("gouden-schoen", "Gouden Schoen-winnaar"),
        ("rode-duivels", "Rode Duivel"),
        ("kerst", "Geboren rond Kerstmis (24–26/12)"),
        ("nieuwjaar", "Geboren op oudjaar/nieuwjaar"),
        ("halloween", "Geboren op Halloween (31/10)"),
        ("sinterklaas", "Geboren rond Sinterklaas (5–6/12)"),
        ("anderlecht", "Speelde bij Anderlecht"),
        ("club-brugge", "Speelde bij Club Brugge"),
        ("standard", "Speelde bij Standard"),
        ("oranje", "Nederlander"),
        ("legende", "Legende (klassieker)"),
        ("wk", "WK-geschiedenis"),
        ("familie", "Voetbalfamilie (vader/zoon, broers)"),
        ("curiosa", "Curiosa en blunders (1 april)"),
    ];

    /// <summary>Een special kan meerdere thema's combineren, bv. "anderlecht,club-brugge" voor de topper.</summary>
    public static List<string> Split(string? tags) =>
        (tags ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();

    public static string? Join(IEnumerable<string> tags) => tags.Distinct().ToList() is { Count: > 0 } list ? string.Join(",", list) : null;

    /// <summary>Heeft een speler of vraag minstens één van de thema's?</summary>
    public static bool Any(IEnumerable<string> itemTags, string? tags) => Split(tags) is var wanted && itemTags.Any(wanted.Contains);

    public static string Label(string key) => All.FirstOrDefault(t => t.Key == key).Label ?? key;
}

/// <summary>Controles voor een speler: wat moet kloppen voor hij in de quiz mag.</summary>
public static class PlayerValidation
{
    public static List<string> Problems(PlayerEdit p, bool forApproval)
    {
        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(p.Name)) problems.Add("Geef een naam.");
        if (p.Hints.Count != GameRules.HintCount || p.Hints.Any(string.IsNullOrWhiteSpace))
            problems.Add($"Er zijn exact {GameRules.HintCount} tips nodig.");

        var nameParts = Normalize(p.Name).Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length > 3).ToList();
        for (var i = 0; i < p.Hints.Count; i++)
        {
            var hint = Normalize(p.Hints[i]);
            var leak = nameParts.FirstOrDefault(w => $" {hint} ".Contains($" {w} "));
            if (leak is not null) problems.Add($"Tip {i + 1} verklapt de naam (\"{leak}\").");
            if (p.Hints[i].Length > 160) problems.Add($"Tip {i + 1} is te lang (max. 160 tekens).");
        }

        if (forApproval && p.Sources.Count(s => Uri.TryCreate(s, UriKind.Absolute, out var u) && u.Scheme is "http" or "https") == 0)
            problems.Add("Voeg minstens één bron (URL) toe voor je goedkeurt.");
        return problems;
    }

    private static string Normalize(string s)
    {
        var d = s.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        var chars = d.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .Select(c => char.IsLetterOrDigit(c) ? c : ' ');
        return new string(chars.ToArray());
    }
}

// ---------- Specials ----------

/// <summary>Een affiche: twee ploegen met hun kleuren, getoond als wedstrijdbanner.</summary>
public record Affiche(string Home, string HomeColor, string Away, string AwayColor);

/// <summary>
/// Sjabloon voor een special. Tag kan meerdere thema's bevatten ("anderlecht,club-brugge").
/// Types: de vraagtypes die het sjabloon voorstelt (null = alles behalve de opstelling).
/// </summary>
public record SpecialTemplate(string Theme, string Name, string Icon, string Description, string? Tag, int? Month, int? Day,
    List<QuestionType>? Types = null, Affiche? Affiche = null);

public static class SpecialTemplates
{
    private static readonly List<QuestionType> PlayerRounds = [QuestionType.WieBenIk, QuestionType.Kluts, QuestionType.Counter];

    /// <summary>Themaspecials: de spelersrondes plus de vraagtypes waarvoor de vragenbank themavragen heeft.</summary>
    private static readonly List<QuestionType> ThemedRounds =
        [QuestionType.WieBenIk, QuestionType.Kluts, QuestionType.Snelvuur, QuestionType.NietBij, QuestionType.Jaar, QuestionType.Emoji];

    public static readonly List<SpecialTemplate> All =
    [
        // Affiches
        new("topper", "De Topper: Anderlecht – Club Brugge", "🔥", "Paars-wit tegen blauw-zwart. Ken jij de spelers van beide kampen?",
            "anderlecht,club-brugge", null, null, PlayerRounds, new("Anderlecht", "#5b2a86", "Club Brugge", "#1f4fa3")),
        new("clasico", "Clasico: Standard – Anderlecht", "⚔️", "De Clasico van het Belgische voetbal: Sclessin tegen het Lotto Park.",
            "standard,anderlecht", null, null, PlayerRounds, new("Standard", "#d81f26", "Anderlecht", "#5b2a86")),
        new("lage-landen", "Derby der Lage Landen: België – Nederland", "🦁", "Rode Duivels tegen Oranje: wie kent zijn buren het best?",
            "rode-duivels,oranje", null, null, [.. PlayerRounds, QuestionType.Rugnummer, QuestionType.Mannetje], new("België", "#d81f26", "Nederland", "#f36c21")),

        // Thema's
        new("rode-duivels", "Rode Duivels Special", "😈", "Alles over onze nationale ploeg: van Mexico '86 tot de gouden generatie.",
            "rode-duivels", null, null,
            [QuestionType.WieBenIk, QuestionType.Emoji, QuestionType.Rugnummer, QuestionType.Mannetje, QuestionType.Kluts, QuestionType.Counter]),
        new("legendes", "Legendes Special", "🏛️", "Den dezen van vroeger: de grootste namen uit de voetbalgeschiedenis.",
            "legende", null, null, [QuestionType.WieBenIk, QuestionType.Carriere, QuestionType.Jaar, QuestionType.Mannetje, QuestionType.Kluts]),
        new("deadline-day", "Transfer Deadline Day", "⏰", "De transfermarkt sluit! Raad de spraakmakendste transfers en carrières.",
            null, 9, 1, [QuestionType.Wissel, QuestionType.Carriere]),

        new("champions-league", "Champions League Special", "⭐", "Speeldag in de Champions League: raad de winnaars van de grootste beker.", "champions-league", null, null),
        new("kerst", "Christmas Special", "🎄", "Boxing Day, kerstmatchen en voetballers die met Kerstmis jarig zijn.", "kerst", 12, 25, ThemedRounds),
        new("nieuwjaar", "New Year Special", "🎆", "Nieuwjaarsmatchen, de wintermercato en voetballers die op oudjaar of nieuwjaar geboren zijn.",
            "nieuwjaar", 1, 1, ThemedRounds),
        new("halloween", "Halloween Special", "🎃", "Griezelig goed: spookgoals, vervloekingen, duivels en voetballers die op Halloween geboren zijn.",
            "halloween", 10, 31, ThemedRounds),
        new("sinterklaas", "Sinterklaas Special", "🎁", "Cadeaugoals, Spaans voetbal en voetballers die rond Sinterklaas jarig zijn.", "sinterklaas", 12, 6, ThemedRounds),
        new("gouden-schoen", "Gouden Schoen Special", "👟", "Op de avond van de Gouden Schoen: alle winnaars.", "gouden-schoen", null, null),
        new("wk", "WK Special", "🌍", "Van Mexico '70 tot Noord-Amerika 2026: finales, helden en drama op het wereldkampioenschap.", "wk", null, null,
            [.. ThemedRounds, QuestionType.Opstelling]),
        new("vaderdag", "Vaderdag Special", "👨‍👦", "Vaders, zonen en broers: de grootste voetbalfamilies.", "familie", null, null, ThemedRounds),
        new("1-april", "1 april Special", "🤡", "Geen grap: de gekste momenten, blunders en curiosa uit de voetbalgeschiedenis.", "curiosa", 4, 1,
            [QuestionType.Snelvuur, QuestionType.NietBij, QuestionType.Jaar, QuestionType.Emoji]),
        new("custom", "Eigen special", "✨", "", null, null, null),
    ];

    public static SpecialTemplate? Find(string? theme) => All.FirstOrDefault(t => t.Theme == theme);
}

public record SpecialEdit(
    int Id, string Theme, string Name, string Icon, string Description,
    DateOnly StartDate, DateOnly EndDate, string? Tag, List<int> PlayerIds, List<QuestionType>? Types = null);

public record SpecialAdminItem(
    int Id, string Theme, string Name, string Icon, DateOnly StartDate, DateOnly EndDate, string? Tag,
    int PoolSize, bool Locked, int Participants, List<QuestionType>? Types = null);

/// <summary>Een special zoals spelers hem zien.</summary>
public record SpecialSummary(
    int Id, string Name, string Icon, string Description, DateOnly StartDate, DateOnly EndDate,
    bool IsActive, bool IsUpcoming, int? MyPoints, int Answered, string? WinnerName, string? Theme = null)
{
    /// <summary>De affiche van het sjabloon (bv. Anderlecht – Club Brugge), of null.</summary>
    [System.Text.Json.Serialization.JsonIgnore] public Affiche? Affiche => SpecialTemplates.Find(Theme)?.Affiche;
}

// ---------- Badges, bekers, spelersprofiel ----------

public enum BadgeTier { Brons, Zilver, Goud }

public record BadgeState(string Id, string Icon, string Name, string Description, BadgeTier Tier, bool Earned, string? Progress);

/// <summary>Month = top 3 van het maandklassement (Daily/Online bleven voor oude gegevens).</summary>
public enum TrophyKind { Daily, Online, Special, Month }

public record Trophy(TrophyKind Kind, string Season, int Place, int Points);

public record HeadToHead(
    int DailyDaysTogether, int MyDailyWins, int TheirDailyWins, int DailyDraws,
    int MatchesTogether, int MyMatchWins, int TheirMatchWins);

public record PublicProfile(
    string UserName, string? AvatarId, LevelInfo Level, DateOnly MemberSince, bool IsMe,
    int DaysPlayed, int DaysWon, int BestDay, int Rating, int MatchesWon,
    List<BadgeState> Badges, List<Trophy> Trophies, HeadToHead? VersusMe);

// ---------- Vragenbank en weekplanning ----------

public record QuizItemListItem(
    int Id, QuestionType Type, string Label, string Answer, PlayerStatus Status, int Difficulty, List<string> Tags,
    int Sources, DateOnly? CheckedAt, int TimesUsed, DateOnly? LastUsed, string? ReviewNote);

/// <summary>Een vraag uit de vragenbank. Data is de inhoud als JSON (formaat per type, zie QuestionTypes.cs).</summary>
public record QuizItemEdit(
    int Id, QuestionType Type, string Data, int Difficulty, List<string> Sources, List<string> Tags,
    PlayerStatus Status, string? ReviewNote, DateOnly? CheckedAt, string? UpdatedBy, int TimesUsed);

/// <summary>Een weekdag: één type, of een mix als Mix twee of meer types bevat.</summary>
public record ScheduleDay(DayOfWeek Day, QuestionType Type, List<QuestionType>? Mix = null)
{
    [System.Text.Json.Serialization.JsonIgnore] public bool IsMix => Mix is { Count: > 1 };

    /// <summary>De geplande types: de mix, of enkel Type.</summary>
    [System.Text.Json.Serialization.JsonIgnore] public List<QuestionType> Planned => IsMix ? Mix! : [Type];
}

/// <summary>Hoeveel goedgekeurde vragen er per type klaarliggen.</summary>
public record TypeStock(QuestionType Type, int Approved, int Review, int NeverUsed, int WeeksLeft, int PerWeek = 0);

/// <summary>Disabled: types die de admin uitgeschakeld heeft.</summary>
public record ScheduleOverview(List<ScheduleDay> Days, List<TypeStock> Stock, List<QuestionType>? Disabled = null);
