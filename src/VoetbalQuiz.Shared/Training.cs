namespace VoetbalQuiz.Shared;

/// <summary>
/// Academie (in de code: training): oude dailies en afgelopen specials opnieuw spelen. Telt nergens mee: geen punten in een klassement, geen XP, geen reeks, geen badges.
/// Eén uitzondering voor de weergave (backlog 037): wie een dag nooit echt speelde, komt met zijn eerste run in het klassement van die dag,
/// als "nagespeeld", zonder eigen plaats en zonder iets te veranderen aan dagwinnaar, podium, maandklassement of XP.
/// </summary>
public static class TrainingRules
{
    /// <summary>Een daily kan je trainen zodra haar dag voorbij is (Belgische tijd): die van vandaag dus pas morgen.</summary>
    public static bool CanTrain(DateOnly date, DateOnly today) => date < today;

    /// <summary>Een special kan je opnieuw spelen zodra ze afgelopen is (de dag na haar einddatum).</summary>
    public static bool CanTrainSpecial(DateOnly endDate, DateOnly today) => endDate < today;

    public const string Label = "Academie – telt niet voor het maandklassement of XP";
}

/// <summary>
/// Je score in het klassement van een dag of special: punten, juist, en je plaats. Bij je echte poging is Rank je echte plaats en Players
/// het aantal spelers; bij een nagespeelde run (eerste keer in de academie) de plaats die je zou hebben tussen de echte spelers, met jezelf meegeteld.
/// </summary>
public record RealResult(int Points, int Correct, int Rank, int Players);

/// <summary>Eén trainingsrun op een daily. IsBest: je hoogste afgewerkte trainingsscore op die daily.</summary>
public record TrainingRunInfo(int Id, DateTime StartedAt, bool Finished, int Points, int Correct, bool IsBest);

/// <summary>Een afgelopen special in de academie.</summary>
public record TrainingSpecial(int Id, string Name, string Icon, DateOnly StartDate, DateOnly EndDate);

/// <summary>
/// Een daily of afgelopen special in het archief. Real is null als je ze niet echt speelde; Replayed is je eerste run in de academie op een
/// dag die je niet echt speelde (zo sta je in het klassement). Special: ingevuld voor een special; Date is dan haar startdatum (ze staat in de
/// maand waarin ze startte, zoals in het maandklassement). Blocked: een vraag zit ook in de daily van vandaag die je nog niet speelde.
/// BeforeYourTime: van vóór je inschrijving; telt niet als gemist. Started: je begon de echte daily zonder één vraag af te sluiten
/// (bv. vlak voor middernacht): niet in het klassement, maar ook niet gemist. Runs telt enkel runs waarin je een vraag startte.
/// </summary>
public record TrainingDay(
    DateOnly Date, double Difficulty, List<QuestionType> Types, RealResult? Real, int Runs, int? BestTraining, TrainingSpecial? Special = null,
    RealResult? Replayed = null, bool Blocked = false, bool BeforeYourTime = false, int Questions = 0, int Players = 0, bool Started = false)
{
    /// <summary>Al eens gespeeld, echt of in de academie.</summary>
    [System.Text.Json.Serialization.JsonIgnore] public bool Played => Real is not null || Replayed is not null || Runs > 0 || Started;

    /// <summary>Je score in het klassement van die dag: echt, of anders nagespeeld.</summary>
    [System.Text.Json.Serialization.JsonIgnore] public RealResult? Board => Real ?? Replayed;

    /// <summary>Gemist: nooit gespeeld, terwijl je toen al meespeelde.</summary>
    [System.Text.Json.Serialization.JsonIgnore] public bool Missed => !Played && !BeforeYourTime;
}

/// <summary>Wat de academie bovenaan aanraadt: de meest recente gemiste dag, of (als je niets meer mist) een revanche op een dag waar je laag eindigde.</summary>
public record TrainingSuggestion(TrainingDay Day, bool Revenge);

/// <summary>
/// De dailies en afgelopen specials van één maand, nieuwste eerst. HasEarlier: er zijn nog oudere maanden met een daily; HasLater: er is een recentere
/// maand om te trainen. Suggestion: over alle maanden heen, null als er niets aan te raden valt.
/// </summary>
public record TrainingArchive(int Year, int Month, List<TrainingDay> Days, bool HasEarlier, bool HasLater, TrainingSuggestion? Suggestion = null);

/// <summary>
/// Een daily of afgelopen special in detail: al je scores (echt + elke trainingsrun, nieuwste eerst). SharesWithToday: een vraag zit ook in de
/// daily van vandaag en je speelde die daar nog niet; dan kan je deze daily pas trainen na de daily van vandaag. Replayed: zie <see cref="TrainingDay"/>.
/// </summary>
public record TrainingDayDetail(
    DateOnly Date, double Difficulty, List<QuestionType> Types, int Questions, int MaxPoints, int Players,
    RealResult? Real, List<TrainingRunInfo> Runs, bool SharesWithToday = false, TrainingSpecial? Special = null, RealResult? Replayed = null);

/// <summary>
/// Na (of tijdens) een trainingsrun. WouldBeRank: je plaats als je die dag (of special) zo gespeeld had, tegenover de echte
/// deelnemers (zonder jezelf); Players: het aantal spelers in dat klassement, jij meegerekend. CountsForBoard: deze run is je eerste
/// op een dag die je niet echt speelde, dus zo sta je (als nagespeeld) in het klassement van die dag.
/// </summary>
public record TrainingSummary(
    int RunId, DateOnly Date, bool Finished, int Points, int Correct, int Questions, int MaxPoints,
    int WouldBeRank, int Players, RealResult? Real, List<TrainingRunInfo> Runs, TrainingSpecial? Special = null, bool CountsForBoard = false);
