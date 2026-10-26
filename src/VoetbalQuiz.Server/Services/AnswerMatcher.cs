using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace VoetbalQuiz.Server.Services;

/// <summary>
/// Vergelijkt een ingetypt antwoord met de naam van de speler. Aanvaard worden:
/// tikfouten, hoofdletters/accenten, en antwoorden die fonetisch juist zijn ("Kurtwa" voor Courtois).
/// </summary>
public static partial class AnswerMatcher
{
    /// <param name="surname">Ook de achternaam aanvaarden (zie <see cref="Surname"/>); uit bij een lijst waarin ze meerdere namen aanwijst.</param>
    public static bool IsMatch(string? guess, string name, IEnumerable<string> alternatives, bool surname = true)
    {
        var g = Normalize(guess);
        if (g.Length < 2) return false;

        var candidates = alternatives.Prepend(name).Select(Normalize).Where(c => c.Length > 0).ToList();
        if (surname && Surname(Normalize(name)) is { } last) candidates.Add(last);

        // 1. Zoals geschreven, met een paar tikfouten speling.
        if (candidates.Any(c => g == c || Levenshtein(g, c) <= AllowedTypos(c.Length))) return true;

        // 2. Zoals het klinkt: vergelijk de klanksleutels (met wat minder speling, want die zijn al vergevingsgezind).
        var gk = Phonetic(g);
        if (gk.Length < 3) return false;
        return candidates.Select(Phonetic).Any(ck => ck.Length >= 3 && (gk == ck || Levenshtein(gk, ck) <= AllowedPhoneticTypos(ck.Length)));
    }

    /// <summary>
    /// De achternaam telt altijd, ook zonder alias en ook als ze dubbelzinnig is
    /// ("Onana" voor Amadou Onana, "De Bruyne" voor Kevin De Bruyne, "Hazard" voor Eden én Thorgan).
    /// </summary>
    public static string? Surname(string name)
    {
        var space = name.IndexOf(' ');
        return space < 0 ? null : name[(space + 1)..];
    }

    private static int AllowedTypos(int length) => length switch
    {
        < 5 => 0,
        < 9 => 1,
        _ => 2,
    };

    private static int AllowedPhoneticTypos(int length) => length switch
    {
        < 6 => 0,
        < 11 => 1,
        _ => 2,
    };

    /// <summary>Kleine letters, zonder accenten, leestekens en dubbele spaties ("Agüero" → "aguero").</summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";

        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (char.IsWhiteSpace(c) || c is '-' or '.' or '\'') sb.Append(' ');
        }
        var cleaned = sb.ToString()
            .Replace('ø', 'o').Replace('ß', 's').Replace('đ', 'd').Replace('ł', 'l').Replace('æ', 'a');
        return string.Join(' ', cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// Een klanksleutel: schrijfwijzen die (in het Nederlands of Frans) hetzelfde klinken, worden gelijk gemaakt.
    /// Bv. "Courtois" en "kurtwa" → "kurtva(s)", "Lukaku" en "lukakoe" → "lukaku", "Hazard" en "azar" → "azar(t)".
    /// Spaties vallen weg, zodat "De Bruyne" en "debruine" gelijk zijn.
    /// </summary>
    public static string Phonetic(string normalized)
    {
        var s = normalized.Replace(" ", "");
        foreach (var (pattern, replacement) in Rules) s = Regex.Replace(s, pattern, replacement);
        // Dubbele letters samenvoegen ("mertenns" → "mertens").
        s = DoubleLetters().Replace(s, "$1");
        // Stomme eindletters (Frans): "hazard" ≈ "azar", "courtois" ≈ "kurtwa".
        return SilentEnd().Replace(s, "");
    }

    /// <summary>Volgorde telt: eerst de lange klankgroepen, dan de losse letters.</summary>
    private static readonly (string Pattern, string Replacement)[] Rules =
    [
        ("oi", "wa"),            // Courtois → kurtwa
        ("eau|au", "o"),         // Thibault/Renaud
        ("ou|oe|oo|uu", "u"),    // Lukakoe, Doku, Rousseau
        ("ij|ei|ey|ai|ay", "ei"),// Mbaye/Mbeje, Vertonghen-achtig
        ("ie|y", "i"),           // Bruyne → bruine, Fernandinho/ie
        ("ae|aa", "a"),
        ("ee", "e"),
        ("sch|sh|ch", "sj"),  // Chadli → sjadli, Schweinsteiger → sjveinsteiger
        ("ph", "f"),
        ("th", "t"),
        ("ck|q|qu", "k"),
        ("x", "ks"),
        ("c(?=[eiy])", "s"),     // Vincent → vinsent
        ("c", "k"),              // Courtois → kurtwa
        ("z", "s"),
        ("w", "v"),
        ("dt", "t"),
        ("h", ""),              // stomme h (Hazard → azar); ch/sh werden al sj
        ("gn", "nj"),            // Castagne → kastanje
        ("rd$", "r"),            // stomme d (Frans): Hazard → azar, Girard → zjirar
        ("d$", "t"),
    ];

    [GeneratedRegex(@"(.)\1+")]
    private static partial Regex DoubleLetters();

    [GeneratedRegex(@"(?<=[aeiou])[stdx]$")]
    private static partial Regex SilentEnd();

    private static int Levenshtein(string a, string b)
    {
        var prev = new int[b.Length + 1];
        var curr = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) prev[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            curr[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(Math.Min(curr[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
            }
            (prev, curr) = (curr, prev);
        }
        return prev[b.Length];
    }
}
