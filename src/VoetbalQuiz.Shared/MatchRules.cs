namespace VoetbalQuiz.Shared;

/// <summary>
/// Regels voor online wedstrijden. Tips en snelheidspunten zijn dezelfde als in de daily
/// (zie <see cref="GameRules"/>); daarbovenop krijgen de eerste juiste antwoorden een positiebonus.
/// </summary>
public static class MatchRules
{
    public const int Questions = 10;
    public const int MinSeats = 2;
    public const int MaxSeats = 8;

    /// <summary>Aftellen voor de eerste vraag.</summary>
    public const double CountdownSeconds = 3;

    /// <summary>Hoe lang het antwoord en de tussenstand getoond worden na een vraag.</summary>
    public const double RevealSeconds = 6;

    /// <summary>Een tafel in de lobby die niet start, verdwijnt na deze tijd.</summary>
    public static readonly TimeSpan LobbyTimeout = TimeSpan.FromHours(2);

    /// <summary>Bonus voor de 1e, 2e en 3e die juist antwoordt.</summary>
    public static readonly int[] PositionBonus = [50, 30, 15];

    public static int BonusFor(int position) =>
        position >= 1 && position <= PositionBonus.Length ? PositionBonus[position - 1] : 0;

    // Elo-rating voor het online klassement
    public const int StartRating = 1000;
    public const double EloK = 32;
}
