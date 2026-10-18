namespace VoetbalQuiz.Shared;

public enum MatchStatus { Lobby, Running, Finished, Cancelled }

/// <summary>Waar een lopende wedstrijd zich bevindt.</summary>
public enum MatchPhase { Lobby, Countdown, Question, Reveal, Finished, Cancelled }

/// <summary>
/// Types: één vraagtype of een mix (leeg = "Wie ben ik?"). Enkel types die aan staan.
/// Questions: aantal vragen (null = <see cref="MatchRules.DefaultQuestions"/>).
/// </summary>
public record CreateMatchRequest(string? Name, int Seats, List<QuestionType>? Types = null, bool Derby = false, int? Questions = null);

/// <summary>Een tafel zoals die in de lobby verschijnt.</summary>
public record MatchSummary(
    int Id, string Name, string HostName, string? HostAvatarId, int Seats, int Players,
    MatchStatus Status, bool IAmIn, DateTime CreatedAt, List<QuestionType>? Types = null, bool Derby = false,
    int Questions = MatchRules.DefaultQuestions);

/// <summary>Derby: een wapen inzetten. Target: de gebruikersnaam van wie een rode kaart krijgt.</summary>
public record UseWeaponRequest(string? Target);

/// <summary>
/// Een ingezet wapen op de vraag die nu loopt (of net afgelopen is).
/// Since: seconden sinds het effect begon (negatief = klaargezet, de vraag start nog).
/// Effect: punten die het kostte of opleverde, enkel na afloop van de vraag.
/// </summary>
public record DerbyEvent(
    DerbyWeapon Weapon, string By, string? ByAvatarId, bool ByMe, string? Target, bool TargetIsMe, double Since, int? Effect = null);

/// <summary>Na afloop: wie welk wapen inzette, en wat het opleverde.</summary>
public record DerbyReportLine(string UserName, string? AvatarId, DerbyWeapon Weapon, int? Question, string? Target, int Effect, bool IsMe);

/// <summary>
/// De derby-kant van een wedstrijd. MyWeapon blijft geheim voor de anderen.
/// WeaponFor: op welke vraag je wapen nu effect zou hebben (de volgende, tijdens het aftellen of de tussenstand).
/// TrumpOfficial/TrumpActual: na een Trump booster het "officiële" en het echte antwoord.
/// </summary>
public record DerbyState(
    DerbyWeapon? MyWeapon,
    bool MyWeaponUsed,
    bool CanUseWeapon,
    int WeaponFor,
    bool Ceasefire,
    List<DerbyEvent> Events,
    string? TrumpOfficial = null,
    string? TrumpActual = null,
    List<DerbyReportLine>? Report = null);

public record LobbyOverview(List<MatchSummary> Open, List<MatchSummary> Mine, List<MatchSummary> RecentlyFinished);

public record MatchPlayerState(
    string UserName, string? AvatarId, int Level, int Score, bool IsHost, bool IsMe, bool Left,
    bool AnsweredCurrent, int? Rank);

/// <summary>Wie punten haalde, in volgorde van aankomst. Position 0: deelpunten zonder juist antwoord (jaar, opstelling, counter).</summary>
public record RevealEntry(string UserName, string? AvatarId, int Position, int Points, int Bonus, bool IsMe);

public record MatchState(
    int Id,
    string Name,
    MatchStatus Status,
    MatchPhase Phase,
    int Seats,
    bool IsHost,
    bool IAmIn,
    List<MatchPlayerState> Players,
    int CurrentNumber,
    List<string> Hints,
    double PhaseElapsedSeconds,
    int MyWrongGuesses,
    string? MyLastWrongGuess,
    bool MyCorrect,
    bool MyDone,
    string? RevealedPlayer,
    List<RevealEntry> Reveal,
    List<QuestionType> Types,
    int TotalQuestions,
    // De lopende vraag: zelfde weergave als in de daily (zie QuestionEngine).
    QuestionType Type = QuestionType.WieBenIk,
    double TimeLimit = 30,
    QuestionView? View = null,
    // Na afloop van de vraag: het juiste antwoord en jouw keuze.
    AnswerResult? Result = null,
    int MyPoints = 0,
    // Derby mode (null bij een gewone tafel).
    DerbyState? Derby = null);

// Online klassement & statistieken
/// <summary>Rij in een online klassement. Value is de rating of het puntentotaal (zie ValueLabel).</summary>
public record RatingEntry(
    int Rank, string UserName, string? AvatarId, int Level, int Value,
    int Played, int Wins, int WinPercent, bool IsMe);

public record OnlineLeaderboard(string Title, string ValueLabel, string? Note, List<RatingEntry> Entries);

public record OnlineMyStats(int Played, int Wins, int Rating, int BestRating, int FirstCorrect, int BestScore);

public record OnlineStats(OnlineMyStats Me, List<StatCard> HallOfFame);
