namespace VoetbalQuiz.Shared;

/// <summary>
/// Voetbaltaal als feedback: "Voorlangs!" bij een foute gok, "Binnen!" bij een juist antwoord, …
/// De keuze hangt af van een seed (bv. het vraagnummer), zodat de tekst niet verspringt bij elke update.
/// </summary>
public static class Commentary
{
    public static readonly string[] Wrong = ["Voorlangs!", "Naast!", "Over!", "Achterlangs!", "Tegen de cornervlag!"];
    public static readonly string[] Goal = ["Binnen!", "Zitten!", "Goal!", "Tegen de netten!"];
    public static readonly string[] Perfect = ["In de winkelhaak!", "Pegel!", "Onhoudbaar!"];
    public static readonly string[] Almost = ["Tegen de lat!", "Op de paal!", "Rakelings!"];
    /// <summary>Wat punten, maar minder dan <see cref="AlmostShare"/> van het maximum: geen "bijna".</summary>
    public static readonly string[] Partial = ["Halve kans.", "Schot op doel.", "Er zat meer in."];

    /// <summary>Vanaf dit deel van het maximum is een deelscore "bijna" (<see cref="Almost"/>), daaronder <see cref="Partial"/>.</summary>
    public const double AlmostShare = 0.5;
    public static readonly string[] Missed = ["Afgefloten.", "Gemiste kans.", "De keeper had hem."];
    public static readonly string[] Blocked = ["Afgeblokt!", "Buitenspel!"];

    public static string Pick(string[] options, int seed) => options[(seed % options.Length + options.Length) % options.Length];

    /// <summary>De uitroep na afloop van een vraag.</summary>
    public static string ForResult(bool correct, int points, int maxPoints, int seed) =>
        correct && points >= maxPoints ? Pick(Perfect, seed)
        : correct ? Pick(Goal, seed)
        : points > 0 && points >= AlmostShare * maxPoints ? Pick(Almost, seed)
        : points > 0 ? Pick(Partial, seed)
        : Pick(Missed, seed);

    /// <summary>
    /// Zoals hierboven, maar zonder punten zegt het waarom: de tijd was om, of de speler paste zelf.
    /// Een fout gekozen optie of jaartal blijft een gemiste kans.
    /// </summary>
    public static string ForResult(AnswerResult result, int maxPoints, int seed) => result switch
    {
        { Correct: false, Points: 0, TimedOut: true } => "Tijd is om!",
        { Correct: false, Points: 0, MyChoice: null, MyYear: null, MyOrder: null, MyPicks: null } => "Gepast.",
        _ => ForResult(result.Correct, result.Points, maxPoints, seed),
    };
}
