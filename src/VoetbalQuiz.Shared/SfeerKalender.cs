namespace VoetbalQuiz.Shared;

/// <summary>
/// De seizoenssfeer van de hele app per quizdag (eerste stap van backlog 005): op die dag kleurt elke pagina mee.
/// De server geeft de sfeer van vandaag mee in <see cref="ServerStatus"/> (servertijd, zoals de daily), zodat een
/// verkeerd ingestelde klok op de gsm er niets aan verandert. Null = de gewone app.
/// </summary>
public static class SfeerKalender
{
    public static string? Op(DateOnly day) => day is { Month: 10, Day: 31 } ? Spookhuis.Theme : null;
}
