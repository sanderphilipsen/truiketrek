namespace VoetbalQuiz.Shared;

/// <summary>
/// De seizoenssfeer van de hele app per quizdag (eerste stap van backlog 005): op die dagen kleurt elke pagina mee.
/// De server geeft de sfeer van vandaag mee in <see cref="ServerStatus"/> (servertijd, zoals de daily), zodat een
/// verkeerd ingestelde klok op de gsm er niets aan verandert. De namen zijn dezelfde als de thema's van de specials
/// (<see cref="SpecialTemplates"/>), zodat een special met dat thema dezelfde sfeer krijgt. Null = de gewone app.
/// </summary>
public static class SfeerKalender
{
    public const string Verjaardag = "verjaardag", Halloween = Spookhuis.Theme, Sinterklaas = "sinterklaas", Kerst = "kerst",
        Nieuwjaar = "nieuwjaar", Pasen = "pasen";

    /// <summary>Alle sferen waarvoor de app kleuren en een decoratie heeft.</summary>
    public static readonly IReadOnlyList<string> Alle = [Pasen, Verjaardag, Halloween, Sinterklaas, Kerst, Nieuwjaar];

    public static string? Op(DateOnly day) => day == PaasZondag(day.Year) || day == PaasZondag(day.Year).AddDays(1) ? Pasen : (day.Month, day.Day) switch
    {
        (10, 18) => Verjaardag, // de verjaardag van Truitje Trek
        (10, 31) => Halloween,
        (12, 5 or 6) => Sinterklaas,
        (12, >= 22 and <= 30) => Kerst,
        (12, 31) or (1, 1 or 2) => Nieuwjaar,
        _ => null,
    };

    /// <summary>Paaszondag (gregoriaanse kalender, de "anonieme" computus van Meeus/Jones/Butcher); paasmaandag is de dag erna.</summary>
    public static DateOnly PaasZondag(int year)
    {
        int a = year % 19, b = year / 100, c = year % 100, d = b / 4, e = b % 4;
        int f = (b + 8) / 25, g = (b - f + 1) / 3, h = (19 * a + b - d - g + 15) % 30;
        int i = c / 4, k = c % 4, l = (32 + 2 * e + 2 * i - h - k) % 7, m = (a + 11 * h + 22 * l) / 451;
        int month = (h + l - 7 * m + 114) / 31, day = (h + l - 7 * m + 114) % 31 + 1;
        return new DateOnly(year, month, day);
    }
}
