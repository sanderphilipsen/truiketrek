namespace VoetbalQuiz.Shared;

public enum MatchStatus { Lobby, Running, Finished, Cancelled }

/// <summary>Waar een lopende wedstrijd zich bevindt.</summary>
public enum MatchPhase { Lobby, Countdown, Question, Reveal, Finished, Cancelled }

/// <summary>Types: één vraagtype of een mix (leeg = "Wie ben ik?"). Enkel types die aan staan.</summary>
public record CreateMatchRequest(string? Name, int Seats, List<QuestionType>? Types = null);

/// <summary>Een tafel zoals die in de lobby verschijnt.</summary>
public record MatchSummary(
    int Id, string Name, string HostName, string? HostAvatarId, int Seats, int Players,
    MatchStatus Status, bool IAmIn, DateTime CreatedAt, List<QuestionType>? Types = null);

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
    int MyPoints = 0);

// Online klassement & statistieken
/// <summary>Rij in een online klassement. Value is de rating of het puntentotaal (zie ValueLabel).</summary>
public record RatingEntry(
    int Rank, string UserName, string? AvatarId, int Level, int Value,
    int Played, int Wins, int WinPercent, bool IsMe);

public record OnlineLeaderboard(string Title, string ValueLabel, string? Note, List<RatingEntry> Entries);

public record OnlineMyStats(int Played, int Wins, int Rating, int BestRating, int FirstCorrect, int BestScore);

public record OnlineStats(OnlineMyStats Me, List<StatCard> HallOfFame);
