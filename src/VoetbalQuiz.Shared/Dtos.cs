namespace VoetbalQuiz.Shared;

// Account
public record RegisterRequest(string UserName, string Email, string Password);
public record LoginRequest(string UserNameOrEmail, string Password);
/// <summary>Welke aanmeldmanieren de server aanbiedt (Google enkel als de sleutels ingesteld zijn).</summary>
public record LoginOptions(bool Google);
/// <summary>Na Google, voor een nieuwe speler: voorgestelde gebruikersnaam en het e-mailadres van Google.</summary>
public record GoogleSignupInfo(string SuggestedUserName, string Email);
public record GoogleSignupRequest(string UserName);
public record UserInfo(string UserName, string Email, string? AvatarId, int Level, bool IsAdmin = false);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record SetAvatarRequest(string AvatarId);
public record ResetPasswordRequest(string UserName);
public record ResetPasswordResult(string UserName, string TemporaryPassword);

public record ProfileInfo(
    string UserName, string Email, string? AvatarId, DateOnly MemberSince,
    LevelInfo Level, List<XpPart> XpBreakdown, bool HasPassword = true, DateTime? DeletionRequestedAt = null);

/// <summary>Een admin verwijdert het account van een speler die daarom vroeg.</summary>
public record DeleteUserRequest(string UserName);
public record ErrorResponse(string Message);

/// <summary>
/// Toestand van de server: <paramref name="UpdateAt"/> (UTC) is gezet zolang er zo een update komt. Aan
/// <paramref name="Version"/> ziet het deployscript dat de nieuwe versie draait.
/// </summary>
public record ServerStatus(DateTime? UpdateAt, string? Version = null);

/// <summary>Wat je bijkreeg sinds de vorige beloningspopup (na een daily, special of online wedstrijd).</summary>
public record RewardSummary(LevelInfo Before, LevelInfo After, List<XpPart> Gained, List<BadgeState> NewBadges)
{
    [System.Text.Json.Serialization.JsonIgnore] public int GainedXp => Gained.Sum(p => p.Xp);
    [System.Text.Json.Serialization.JsonIgnore] public bool HasNews => GainedXp > 0 || NewBadges.Count > 0;
}

// Daily challenge
public enum QuestionStatus { NotStarted, InProgress, Answered }

public record DailyOverview(DateOnly Date, int TotalPoints, List<QuestionSummary> Questions, DayWinner? LastWinner,
    QuestionType Type = QuestionType.WieBenIk, int MaxPoints = 1000, List<QuestionType>? Types = null, double Difficulty = 0)
{
    /// <summary>Een dag met meer dan één vraagtype.</summary>
    [System.Text.Json.Serialization.JsonIgnore] public bool IsMix => Types is { Count: > 1 };
}

/// <summary>
/// Jouw plaats in het klassement van vandaag (de daily), berekend op de server.
/// Date: de quizdag (Belgische datum) waarvoor de plaats geldt; Home toont ze enkel als die gelijk is aan de daily die het toont.
/// Rank is null zolang je vandaag nog geen vraag beantwoordde; Players is het aantal spelers in dat klassement.
/// PreviousRank: je plaats bij je vorige bezoek aan Home vandaag (null bij het eerste bezoek of als je toen nog niet meespeelde).
/// Streak: je huidige reeks speeldagen na elkaar (tot vandaag of gisteren), zoals in de statistieken; voor het tegeltje op Home.
/// </summary>
public record DailyRank(DateOnly Date, int? Rank, int Players, int? PreviousRank, int Streak = 0)
{
    /// <summary>Zoveel plaatsen gestegen sinds je vorige bezoek (0 als je gelijk bleef, zakte of toen nog niet meespeelde).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public int RisenBy => Rank is { } now && PreviousRank is { } before && before > now ? before - now : 0;
}

public record DayWinner(DateOnly Date, string UserName, string? AvatarId, int Points, bool IsMe);

public record QuestionSummary(
    int Number,
    QuestionStatus Status,
    int? Points,
    bool? Correct,
    string? PlayerName,
    QuestionType Type = QuestionType.WieBenIk)
{
    /// <summary>
    /// Niet helemaal juist, maar wel punten bij een type met deelpunten (<see cref="QuestionRules.PartialScores"/>):
    /// deels juist, geen fout.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool Partial => Correct == false && Points > 0 && QuestionRules.PartialScores(Type);
}

/// <summary>Huidige toestand van één vraag zoals de server die ziet.</summary>
public record QuestionState(
    int Number,
    QuestionStatus Status,
    List<string> Hints,
    double ElapsedSeconds,
    int WrongGuesses,
    string? LastWrongGuess,
    AnswerResult? Result,
    QuestionType Type = QuestionType.WieBenIk,
    double TimeLimit = 30,
    QuestionView? View = null);

/// <summary>Wat er van een vraag zichtbaar is, afhankelijk van het type (Hints in QuestionState voor tips en clubs).</summary>
public record QuestionView(
    string? Prompt = null,
    List<string>? Options = null,
    List<LineupSlot>? Lineup = null,
    int MaxPoints = 100,
    string? Note = null,
    // Types die stap voor stap onthullen: de huidige stap (1..Steps).
    int Step = 0,
    int Steps = 0,
    // Mannetje: de avatar zoals hij nu getoond mag worden (als code, zonder naam).
    string? Avatar = null,
    // Kluts in de zestien: de letters per woord.
    List<List<LetterTile>>? Letters = null,
    // Rugnummer: het nummer op het shirt.
    int? Shirt = null,
    // Counter: al gevonden en het totaal aantal spelers. Kaart: al beantwoorde spelden en het totaal aantal spelden.
    int Found = 0,
    int Total = 0,
    // De Krant: de alinea's met de gaten (Prompt is de kop, Note het intro). Found/Total: ingevulde gaten.
    List<List<KrantPiece>>? Article = null,
    string? ArticleKind = null,
    // Detectivedag: verdachten en geopende verklaringen (Prompt is de titel, Note het verhaaltje).
    DetectiveView? Detective = null,
    // Kaart: de beantwoorde spelden en de huidige, nooit de volgende. De keuzelijst (alle clubs in deel 1, alle
    // stadions in deel 2) staat in Options.
    List<KaartSpeld>? Pins = null,
    // Kaart: welk deel (QuestionRules.KaartClubsPart of KaartStadiumsPart); 0 bij de andere types.
    int Part = 0,
    // Kaart: het land van de kaart (ISO-code, voor LandKaart). Geen geheim: de kaart zelf toont het land.
    string? Country = null,
    // Kaart, deel 2: wat de keuzelijst toont, in dezelfde volgorde als Options (stadion met capaciteit). De gok blijft
    // de waarde uit Options. Null = Options zelf tonen.
    List<string>? OptionLabels = null,
    // Lijst: de rijen (Found/Total: gevonden namen en het aantal namen).
    List<LijstSlot>? Rows = null);

/// <summary>
/// Kaart: een speld zoals de speler ze ziet. Choices = hoeveel keuzelijsten (deel 1: één per club die er speelt; deel 2: één).
/// Clubs: in deel 2 de club(s) bij de speld, want dat is de vraag (in deel 1 null tot het antwoord).
/// Answer (het juiste antwoord: de clubs of het stadion), Pick (wat jij koos, leeg = overgeslagen) en Hits (hoeveel juist)
/// pas zodra de speld beantwoord is.
/// </summary>
public record KaartSpeld(double Lat, double Lon, int Choices, List<string>? Clubs = null,
    List<string>? Answer = null, string? Pick = null, int? Hits = null)
{
    /// <summary>Nog niet beantwoord: de speld die nu aan de beurt is.</summary>
    public bool IsCurrent => Hits is null;

    /// <summary>Alles juist (bij een gedeeld stadion in deel 1: beide clubs).</summary>
    public bool IsRight => Hits == Choices;
}

public record AnswerRequest(string Guess);

public record AnswerResult(
    bool Correct,
    int Points,
    string PlayerName,
    int WrongGuesses,
    List<string> AllHints,
    string? Explanation = null,
    int? CorrectIndex = null,
    int? MyChoice = null,
    int? CorrectYear = null,
    int? MyYear = null,
    List<LineupSlot>? FullLineup = null,
    // Op volgorde: de gekozen volgorde (indexen in de juiste volgorde), null als de speler niets instuurde.
    List<int>? MyOrder = null,
    // De Krant: het hele artikel met per gat het antwoord, wat jij invulde en je score.
    List<List<KrantPiece>>? FullArticle = null,
    // Detectivedag: het dossier, per verdachte welke verklaring hem uitsloot (AllHints: alle verklaringen).
    List<DetectiveVerdict>? Dossier = null,
    // Kaart: wat de speler per beantwoorde speld koos (leeg = overgeslagen), null als hij geen enkele speld beantwoordde.
    List<string>? MyPicks = null,
    // Lijst: alle rijen ingevuld, met per rij of jij die naam vond.
    List<LijstSlot>? FullList = null,
    // De vraag liep af omdat de tijd om was (niet omdat de speler paste of antwoordde).
    bool TimedOut = false);

// Leaderboard
public record LeaderboardEntry(
    int Rank, string UserName, string? AvatarId, int Level, int Points,
    int DaysPlayed, int CorrectAnswers, int DaysWon, bool IsMe);
public record Leaderboard(string Title, string? Note, List<LeaderboardEntry> Entries);

// Statistieken
public record StatsOverview(MyStats Me, List<StatCard> HallOfFame, List<HardQuestion> Hardest);

public record MyStats(
    int DaysPlayed, int DaysWon, int BestDay, int AverageDay, int CorrectPercent,
    int FirstHintHits, int CurrentStreak, int LongestStreak);

/// <summary>Eén record in de eregalerij, bv. "Meeste dagzeges".</summary>
public record StatCard(string Icon, string Title, string Caption, string UserName, string? AvatarId, string Value, bool IsMe);

public record HardQuestion(string PlayerName, int CorrectPercent, int Attempts);
