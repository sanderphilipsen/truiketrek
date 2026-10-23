namespace VoetbalQuiz.Shared;

/// <summary>
/// Regels voor online wedstrijden. Tips en snelheidspunten zijn dezelfde als in de daily
/// (zie <see cref="GameRules"/>); daarbovenop krijgen de eerste juiste antwoorden een positiebonus.
/// </summary>
public static class MatchRules
{
    /// <summary>Aantal vragen dat de organisator kan kiezen (de punten tellen niet voor de daglimiet).</summary>
    public const int DefaultQuestions = 10;
    public const int MinQuestions = 3;
    public const int MaxQuestions = 30;

    /// <summary>De keuzes in het formulier (de server aanvaardt alles van <see cref="MinQuestions"/> tot <see cref="MaxQuestions"/>).</summary>
    public static readonly int[] QuestionChoices = [5, 10, 15, 20, 30];

    /// <summary>Binnen de grenzen, en minstens één vraag per gekozen type (anders valt een type uit de mix).</summary>
    public static int ClampQuestions(int? questions, int types = 1) =>
        Math.Clamp(questions ?? DefaultQuestions, MinQuestionsFor(types), MaxQuestions);

    public static int MinQuestionsFor(int types) => Math.Clamp(types, MinQuestions, MaxQuestions);

    /// <summary>
    /// Vraagtypes die online gespeeld kunnen worden. De solo-types (de kaart, zie <see cref="QuestionTypes.IsSolo"/>) niet:
    /// één kaart van twee minuten past niet in een partijtje met een gedeelde klok per vraag.
    /// </summary>
    public static bool AllowedOnline(QuestionType type) => !QuestionTypes.IsSolo(type);

    public const int MinSeats = 2;

    /// <summary>
    /// Een zege telt (Elo, reeksen, badges, XP, maandklassement) pas als er minstens zoveel spelers tot het einde bleven.
    /// Wie als enige overblijft wint het partijtje wel, maar dat telt nergens als zege: anders win je gratis door
    /// de anderen te laten vertrekken.
    /// </summary>
    public const int StayersForWin = 2;
    public const int MaxSeats = 8;

    /// <summary>
    /// Een zege: alleen eerste (een gedeelde eerste plaats is een gelijkspel en telt voor niemand als zege: anders
    /// win je allebei door samen op alles te passen) en genoeg spelers die bleven (<see cref="StayersForWin"/>).
    /// </summary>
    /// <param name="rank">De eindplaats van de speler.</param>
    /// <param name="stayers">Hoeveel spelers tot het einde bleven.</param>
    /// <param name="firsts">Hoeveel spelers op de eerste plaats eindigden.</param>
    public static bool IsWin(int? rank, int stayers, int firsts) => rank == 1 && stayers >= StayersForWin && firsts == 1;

    /// <summary>Aftellen voor de eerste vraag.</summary>
    public const double CountdownSeconds = 3;

    /// <summary>Hoe lang het antwoord en de tussenstand getoond worden na een vraag.</summary>
    public const double RevealSeconds = 6;

    /// <summary>Een partijtje in de lobby dat niet start, verdwijnt na deze tijd.</summary>
    public static readonly TimeSpan LobbyTimeout = TimeSpan.FromHours(2);

    /// <summary>Bonus voor de 1e, 2e en 3e die juist antwoordt.</summary>
    public static readonly int[] PositionBonus = [50, 30, 15];

    public static int BonusFor(int position) =>
        position >= 1 && position <= PositionBonus.Length ? PositionBonus[position - 1] : 0;

    // Elo-rating voor het online klassement
    public const int StartRating = 1000;
    public const double EloK = 32;
}
