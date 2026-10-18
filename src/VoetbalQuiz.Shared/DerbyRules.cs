namespace VoetbalQuiz.Shared;

/// <summary>De wapens van een derby: elke speler krijgt er bij de aftrap één, en kan het één keer inzetten.</summary>
public enum DerbyWeapon { RodeKaart, BengaalsVuur, Wissel, Trump }

public record DerbyWeaponInfo(DerbyWeapon Weapon, string Name, string Icon, string HowTo);

/// <summary>
/// Regels voor de derby mode van een online tafel. Zowel de server (beslist) als de client (toont de knop) gebruiken ze.
/// Een wapen kan je inzetten tijdens een vraag, of klaarzetten tijdens het aftellen of de tussenstand: dan gaat het af bij de volgende vraag.
/// </summary>
public static class DerbyRules
{
    public static readonly List<DerbyWeaponInfo> All =
    [
        new(DerbyWeapon.RodeKaart, "Rode kaart", "🟥", "Kies een tegenstander: die krijgt maar de helft van zijn punten voor deze vraag."),
        new(DerbyWeapon.BengaalsVuur, "Bengaals vuur", "🎆", "Steek het af: rode rook trekt over het scherm van al je tegenstanders en ze zien een hele tijd niets."),
        new(DerbyWeapon.Wissel, "Wissel", "🔁", "Iedereen krijgt een nieuwe vraag van hetzelfde type, met een volle timer."),
        new(DerbyWeapon.Trump, "Trump booster", "🟨", "Jouw antwoord wordt het juiste. Wie het echte antwoord gaf, heeft het fout. Enkel bij Snelvuur, Wie hoort er niet bij en Raad het jaar."),
    ];

    public static DerbyWeaponInfo Get(DerbyWeapon weapon) => All.First(w => w.Weapon == weapon);

    /// <summary>Hoe lang het bengaals vuur brandt en de rook het scherm van de anderen onleesbaar maakt (tot de vraag afloopt).</summary>
    public const double FlareSeconds = 12;

    /// <summary>Aftellen met het wisselbord voor de nieuwe vraag begint.</summary>
    public const double SwapCountdownSeconds = 3;

    /// <summary>Wissel en bengaals vuur: enkel in het eerste deel van de vraagtijd (daarna heeft het weinig zin).</summary>
    public const double UseWindow = 0.5;

    /// <summary>Wie een rode kaart krijgt, houdt dit deel van zijn punten (inclusief positiebonus) voor die vraag.</summary>
    public static int AfterRedCard(int points) => points / 2;

    /// <summary>Trump kan enkel bij vragen met één gekozen antwoord: een optie of een jaartal.</summary>
    public static bool TrumpWorksOn(QuestionType type) =>
        type is QuestionType.Snelvuur or QuestionType.NietBij or QuestionType.Jaar;

    /// <summary>Wapenstilstand: op de eerste en de laatste vraag kan niemand een wapen inzetten.</summary>
    public static bool Ceasefire(int number, int total) => number <= 1 || number >= total;

    /// <summary>
    /// Kan dit wapen nu (of klaargezet: bij de volgende vraag) ingezet worden?
    /// <paramref name="number"/> is de vraag waarop het effect heeft; <paramref name="elapsed"/> &lt; 0 = die vraag is nog niet begonnen.
    /// </summary>
    public static bool CanUse(DerbyWeapon weapon, QuestionType type, int number, int total, double elapsed, double limit)
    {
        if (Ceasefire(number, total)) return false;
        if (weapon == DerbyWeapon.Trump && !TrumpWorksOn(type)) return false;
        return weapon switch
        {
            // Een wissel klaarzetten heeft geen zin: niemand heeft de vraag al gezien.
            DerbyWeapon.Wissel => elapsed >= 0 && elapsed < limit * UseWindow,
            DerbyWeapon.BengaalsVuur => elapsed < limit * UseWindow,
            _ => elapsed < limit,
        };
    }

    /// <summary>
    /// Deelt de wapens uit: de vier worden geschud en om de beurt uitgedeeld, zodat er pas vanaf
    /// vijf spelers dubbels zijn. Zonder keuzevragen aan tafel valt Trump weg.
    /// </summary>
    public static List<DerbyWeapon> Deal(int players, bool trumpPossible, Random random)
    {
        var pool = All.Select(w => w.Weapon).Where(w => trumpPossible || w != DerbyWeapon.Trump).ToList();
        var dealt = new List<DerbyWeapon>();
        while (dealt.Count < players)
        {
            var round = pool.OrderBy(_ => random.Next()).ToList();
            dealt.AddRange(round.Take(players - dealt.Count));
        }
        return dealt;
    }
}
