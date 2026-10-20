namespace VoetbalQuiz.Shared;

/// <summary>
/// Academie (in de code: training): oude dailies en afgelopen specials opnieuw spelen. Telt nergens mee: geen punten in een klassement, geen XP, geen reeks, geen badges.
/// </summary>
public static class TrainingRules
{
    /// <summary>Een daily kan je trainen zodra haar dag voorbij is (Belgische tijd): die van vandaag dus pas morgen.</summary>
    public static bool CanTrain(DateOnly date, DateOnly today) => date < today;

    /// <summary>Een special kan je opnieuw spelen zodra ze afgelopen is (de dag na haar einddatum).</summary>
    public static bool CanTrainSpecial(DateOnly endDate, DateOnly today) => endDate < today;

    public const string Label = "Academie – telt niet voor het klassement";
}

/// <summary>Je echte poging (op de dag zelf of tijdens de special): punten, juist, en je plaats in dat klassement.</summary>
public record RealResult(int Points, int Correct, int Rank, int Players);

/// <summary>Eén trainingsrun op een daily. IsBest: je hoogste afgewerkte trainingsscore op die daily.</summary>
public record TrainingRunInfo(int Id, DateTime StartedAt, bool Finished, int Points, int Correct, bool IsBest);

/// <summary>Een afgelopen special in de academie.</summary>
public record TrainingSpecial(int Id, string Name, string Icon, DateOnly StartDate, DateOnly EndDate);

/// <summary>
/// Een daily of afgelopen special in het archief. Real is null als je ze niet echt speelde. Special: ingevuld voor een special;
/// Date is dan haar einddatum (daarop staat ze in het archief).
/// </summary>
public record TrainingDay(
    DateOnly Date, double Difficulty, List<QuestionType> Types, RealResult? Real, int Runs, int? BestTraining, TrainingSpecial? Special = null)
{
    /// <summary>Al eens gespeeld, echt of in training.</summary>
    [System.Text.Json.Serialization.JsonIgnore] public bool Played => Real is not null || Runs > 0;
}

/// <summary>De dailies en afgelopen specials van één maand, nieuwste eerst. HasEarlier: er zijn nog oudere maanden met een daily; HasLater: er is een recentere maand om te trainen.</summary>
public record TrainingArchive(int Year, int Month, List<TrainingDay> Days, bool HasEarlier, bool HasLater);

/// <summary>
/// Een daily of afgelopen special in detail: al je scores (echt + elke trainingsrun, nieuwste eerst). SharesWithToday: een vraag zit ook in de
/// daily van vandaag en je speelde die daar nog niet; dan kan je deze daily pas trainen na de daily van vandaag.
/// </summary>
public record TrainingDayDetail(
    DateOnly Date, double Difficulty, List<QuestionType> Types, int Questions, int MaxPoints, int Players,
    RealResult? Real, List<TrainingRunInfo> Runs, bool SharesWithToday = false, TrainingSpecial? Special = null);

/// <summary>
/// Na (of tijdens) een trainingsrun. WouldBeRank: je plaats als je die dag (of special) zo gespeeld had, tegenover de echte
/// deelnemers (zonder jezelf); Players: het aantal spelers in dat klassement, jij meegerekend.
/// </summary>
public record TrainingSummary(
    int RunId, DateOnly Date, bool Finished, int Points, int Correct, int Questions, int MaxPoints,
    int WouldBeRank, int Players, RealResult? Real, List<TrainingRunInfo> Runs, TrainingSpecial? Special = null);
