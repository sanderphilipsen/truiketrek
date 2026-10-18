using Microsoft.AspNetCore.Identity;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Data;

public class AppUser : IdentityUser
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? AvatarId { get; set; }

    /// <summary>XP per onderdeel (zoals Levels.Breakdown) die de speler al zag in de beloningspopup. Null = nog nooit.</summary>
    public List<int>? SeenXpParts { get; set; }

    /// <summary>Badges die de speler al zag in de beloningspopup. Null = nog nooit.</summary>
    public List<string>? SeenBadges { get; set; }
}

public class Player
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public List<string> Answers { get; set; } = [];
    public string Country { get; set; } = "";
    public string Competition { get; set; } = "";
    public string Era { get; set; } = "modern";
    public int Difficulty { get; set; } = 2;
    public List<string> Hints { get; set; } = [];

    /// <summary>Enkel goedgekeurde spelers komen in de quiz.</summary>
    public PlayerStatus Status { get; set; } = PlayerStatus.Review;
    public List<string> Sources { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public string? ReviewNote { get; set; }
    public DateOnly? CheckedAt { get; set; }

    /// <summary>Aangepast in het admin-paneel: de JSON-seed overschrijft deze speler dan niet meer.</summary>
    public bool EditedInAdmin { get; set; }
    public string? UpdatedBy { get; set; }
}

public class DailyChallenge
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public List<DailyQuestion> Questions { get; set; } = [];

    /// <summary>Ingevuld als dit de vragenreeks van een special is (anders: de gewone daily).</summary>
    public int? SpecialEventId { get; set; }
    public SpecialEvent? SpecialEvent { get; set; }

    /// <summary>Wordt ingevuld zodra de dag voorbij is (zie ResultsService).</summary>
    public DateTime? SettledAt { get; set; }
    public string? WinnerUserId { get; set; }
    public AppUser? Winner { get; set; }
    public int? WinnerPoints { get; set; }
}

public class DailyQuestion
{
    public int Id { get; set; }
    public int DailyChallengeId { get; set; }
    public DailyChallenge DailyChallenge { get; set; } = null!;
    public int Number { get; set; }
    public QuestionType Type { get; set; } = QuestionType.WieBenIk;

    /// <summary>Bij "Wie ben ik?": de speler.</summary>
    public int? PlayerId { get; set; }
    public Player? Player { get; set; }

    /// <summary>Bij de andere types: de vraag uit de vragenbank.</summary>
    public int? QuizItemId { get; set; }
    public QuizItem? QuizItem { get; set; }

    /// <summary>Opstelling: welke plaatsen (0–10) ingevuld moeten worden. Voor iedereen dezelfde.</summary>
    public List<int> Blanks { get; set; } = [];

    /// <summary>Counter: de spelers (ids), in de volgorde waarin ze verschijnen.</summary>
    public List<int> CounterPlayerIds { get; set; } = [];

    /// <summary>Counter: de spelers zelf (niet opgeslagen, ingeladen door QuizService).</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public List<Player> CounterPlayers { get; set; } = [];
}

/// <summary>Eén poging van een gebruiker op een vraag. Alle tijden zijn servertijden (UTC).</summary>
public class Attempt
{
    public int Id { get; set; }
    public required string UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public int DailyQuestionId { get; set; }
    public DailyQuestion DailyQuestion { get; set; } = null!;

    public DateTime StartedAt { get; set; }
    public DateTime? AnsweredAt { get; set; }

    public int WrongGuesses { get; set; }
    public string? LastGuess { get; set; }
    public bool Correct { get; set; }
    public int Points { get; set; }

    /// <summary>Meerkeuze/niet-bij: gekozen optie. Jaar: gekozen jaartal.</summary>
    public int? Choice { get; set; }

    /// <summary>Opstelling: welke plaatsen al juist ingevuld zijn. Counter: welke spelers (index) geraden zijn.</summary>
    public List<int> Found { get; set; } = [];

    /// <summary>Counter: welke spelers (index) overgeslagen zijn.</summary>
    public List<int> Skipped { get; set; } = [];
}

public class Feedback
{
    public int Id { get; set; }
    public required string UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public FeedbackType Type { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public FeedbackStatus Status { get; set; } = FeedbackStatus.Nieuw;
    public string? Reply { get; set; }
}

// ---------- Online wedstrijden ----------

public class Match
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string HostUserId { get; set; }
    public AppUser Host { get; set; } = null!;
    public int Seats { get; set; }
    public MatchStatus Status { get; set; } = MatchStatus.Lobby;
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }

    /// <summary>Gekozen vraagtypes: één type of een mix (leeg = "Wie ben ik?", zoals de oudere tafels).</summary>
    public List<QuestionType> Types { get; set; } = [];

    /// <summary>Derby mode: elke speler krijgt bij de aftrap een wapen (zie <see cref="DerbyRules"/>).</summary>
    public bool Derby { get; set; }
    public List<MatchPlayer> Players { get; set; } = [];
    public List<MatchQuestion> Questions { get; set; } = [];
}

public class MatchPlayer
{
    public int Id { get; set; }
    public int MatchId { get; set; }
    public Match Match { get; set; } = null!;
    public required string UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public DateTime JoinedAt { get; set; }
    /// <summary>Verlaten tijdens de wedstrijd: telt niet meer mee om een vraag vroeg af te sluiten.</summary>
    public DateTime? LeftAt { get; set; }
    public int Score { get; set; }
    public int? Rank { get; set; }

    // Derby: één wapen per speler, één keer in te zetten.
    public DerbyWeapon? Weapon { get; set; }
    public DateTime? WeaponUsedAt { get; set; }
    /// <summary>De vraag waarop het wapen effect heeft (klaargezet: de volgende vraag).</summary>
    public int? WeaponQuestion { get; set; }
    /// <summary>Rode kaart: wie ze kreeg.</summary>
    public string? WeaponTargetUserId { get; set; }
    /// <summary>Wat het wapen anderen kostte of jezelf opleverde in punten, voor het derby-rapport.</summary>
    public int WeaponEffect { get; set; }
}

public class MatchQuestion
{
    public int Id { get; set; }
    public int MatchId { get; set; }
    public Match Match { get; set; } = null!;
    public int Number { get; set; }
    public QuestionType Type { get; set; } = QuestionType.WieBenIk;

    /// <summary>Bij "Wie ben ik?" en kluts: de speler.</summary>
    public int? PlayerId { get; set; }
    public Player? Player { get; set; }

    /// <summary>Bij de types uit de vragenbank: de vraag.</summary>
    public int? QuizItemId { get; set; }
    public QuizItem? QuizItem { get; set; }

    /// <summary>Opstelling: welke plaatsen ingevuld moeten worden. Voor iedereen aan tafel dezelfde.</summary>
    public List<int> Blanks { get; set; } = [];

    /// <summary>Counter: de spelers (ids), in de volgorde waarin ze verschijnen.</summary>
    public List<int> CounterPlayerIds { get; set; } = [];

    /// <summary>Counter: de spelers zelf (niet opgeslagen, ingeladen door MatchService).</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public List<Player> CounterPlayers { get; set; } = [];

    /// <summary>Wanneer de eerste tip verschijnt (null = nog niet aan de beurt).</summary>
    public DateTime? StartsAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public List<MatchAnswer> Answers { get; set; } = [];

    // Derby: wapens op deze vraag (elk maximaal één keer per vraag).
    /// <summary>Trump booster: het antwoord van deze speler wordt het juiste.</summary>
    public string? TrumpUserId { get; set; }
    /// <summary>Bengaals vuur: wie het afstak en wanneer (klaargezet = vanaf de start van de vraag).</summary>
    public string? FlareUserId { get; set; }
    public DateTime? FlareAt { get; set; }
    /// <summary>Wissel: wie de vraag liet vervangen.</summary>
    public string? SwapUserId { get; set; }
}

public class MatchAnswer
{
    public int Id { get; set; }
    public int MatchQuestionId { get; set; }
    public MatchQuestion MatchQuestion { get; set; } = null!;
    public required string UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public int WrongGuesses { get; set; }
    public string? LastGuess { get; set; }
    public bool Correct { get; set; }
    public bool GaveUp { get; set; }
    /// <summary>Klaar met deze vraag: juist, gepast, een optie gekozen of een opstelling/counter afgewerkt.</summary>
    public DateTime? AnsweredAt { get; set; }
    /// <summary>Punten zonder positiebonus (ook deelpunten bij jaar, opstelling en counter).</summary>
    public int SpeedPoints { get; set; }

    /// <summary>Zoals bij <see cref="Attempt"/>: gekozen optie of jaartal, gevonden en overgeslagen plaatsen.</summary>
    public int? Choice { get; set; }
    public List<int> Found { get; set; } = [];
    public List<int> Skipped { get; set; } = [];
    /// <summary>Volgorde van de juiste antwoorden, ingevuld als de vraag afloopt.</summary>
    public int? Position { get; set; }
    public int Bonus { get; set; }
    /// <summary>Derby: rode kaart gekregen van deze speler (halve punten voor deze vraag).</summary>
    public string? RedCardFromUserId { get; set; }
    /// <summary>Derby (propere handen): deze speler steelt de helft van de punten van deze vraag.</summary>
    public string? StolenByUserId { get; set; }
    /// <summary>Derby (gokchinees): dubbel of niks op deze vraag.</summary>
    public bool Gamble { get; set; }
    /// <summary>Derby (verlenging): extra seconden voor deze speler bij deze vraag.</summary>
    public int ExtraSeconds { get; set; }
}

// ---------- Specials ----------

/// <summary>Een themadag (bv. Christmas Special), beheerd in het admin-paneel.</summary>
public class SpecialEvent
{
    public int Id { get; set; }
    public required string Theme { get; set; }
    public required string Name { get; set; }
    public string Icon { get; set; } = "✨";
    public string Description { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    /// <summary>Spelers met deze tag vormen de vragenpool (tenzij er handmatig spelers gekozen zijn).</summary>
    public string? Tag { get; set; }
    public List<int> PlayerIds { get; set; } = [];

    /// <summary>Welke vraagtypes de special mengt (leeg = enkel "Wie ben ik?").</summary>
    public List<QuestionType> Types { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}

// ---------- Vragenbank en weekplanning ----------

/// <summary>Een vraag voor de nieuwe vraagtypes (snelvuur, carrièrepad, ...). De inhoud zit als JSON in Data.</summary>
public class QuizItem
{
    public int Id { get; set; }
    public QuestionType Type { get; set; }

    /// <summary>Unieke sleutel om de JSON-seed te koppelen (bv. de vraagtekst of het antwoord).</summary>
    public required string Key { get; set; }
    public required string Data { get; set; }
    public int Difficulty { get; set; } = 2;
    public PlayerStatus Status { get; set; } = PlayerStatus.Review;
    public List<string> Sources { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public string? ReviewNote { get; set; }
    public DateOnly? CheckedAt { get; set; }
    public bool EditedInAdmin { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>Welk vraagtype de daily op een weekdag heeft, of een mix van types.</summary>
public class DailyScheduleDay
{
    public int Id { get; set; }
    public DayOfWeek Day { get; set; }
    public QuestionType Type { get; set; }

    /// <summary>Twee of meer types: die dag is een mix (Type wordt dan niet gebruikt).</summary>
    public List<QuestionType> MixTypes { get; set; } = [];
}

/// <summary>Een vraagtype dat de admin (tijdelijk) uitgeschakeld heeft. Het komt niet in dailies of specials.</summary>
public class DisabledQuestionType
{
    public int Id { get; set; }
    public QuestionType Type { get; set; }
}

// ---------- Gouden Bal ----------

/// <summary>
/// De nummer 1 van een afgelopen maand. Vastgelegd zodra de maand voorbij is (zie GoldenBallService),
/// zodat de sterren blijven, ook als er later iets aan de berekening verandert. Bij een gedeelde eerste plaats: één rij per winnaar.
/// </summary>
public class GoldenBall
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public required string UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public int Points { get; set; }
    public DateTime AwardedAt { get; set; }
}

/// <summary>Een maand die afgesloten is voor de Gouden Bal (ook als niemand punten had), zodat we ze maar één keer berekenen.</summary>
public class GoldenBallMonth
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public DateTime SettledAt { get; set; }
}
