using System.Text.RegularExpressions;

namespace VoetbalQuiz.Shared;

/// <summary>
/// De Krant: een gat in het artikel. Number is ingevuld bij een getal (dan is Answer het getal als tekst), met
/// Tolerance als stap voor de deelpunten. Hint staat in het lege gat ("speler", "minuut").
/// </summary>
public record KrantGap(string Answer, List<string> Aliases, string Hint, int? Number, int Tolerance);

/// <summary>Een stuk van een alinea: gewone tekst, of (Gap ≥ 0) het gat met dat nummer.</summary>
public record KrantSegment(string Text, int Gap = -1);

/// <summary>
/// Leest de tekst van een artikel (<see cref="KrantData.Text"/>): alinea's gescheiden door een lege lijn, gaten als
/// [[Jan Vertonghen / Vertonghen : speler]] of [[#69 : minuut]] of [[#115~5 : miljoen euro]]. Server (beoordelen) en
/// admin (voorbeeld) gebruiken dezelfde lezer.
/// </summary>
public static partial class KrantText
{
    [GeneratedRegex(@"\[\[(.*?)\]\]", RegexOptions.Singleline)]
    private static partial Regex GapPattern();

    [GeneratedRegex(@"^#\s*(\d{1,9})\s*(?:~\s*(\d{1,9}))?$")]
    private static partial Regex NumberPattern();

    [GeneratedRegex(@"\n\s*\n")]
    private static partial Regex ParagraphBreak();

    public record Article(List<List<KrantSegment>> Paragraphs, List<KrantGap> Gaps, List<string> Problems);

    public static Article Parse(string? text)
    {
        var paragraphs = new List<List<KrantSegment>>();
        var gaps = new List<KrantGap>();
        var problems = new List<string>();

        foreach (var raw in ParagraphBreak().Split((text ?? "").Replace("\r\n", "\n").Trim()))
        {
            var paragraph = raw.Trim().Replace('\n', ' ');
            if (paragraph.Length == 0) continue;
            var segments = new List<KrantSegment>();
            var at = 0;
            foreach (Match m in GapPattern().Matches(paragraph))
            {
                if (m.Index > at) segments.Add(new KrantSegment(paragraph[at..m.Index]));
                if (ParseGap(m.Groups[1].Value) is { } gap)
                {
                    segments.Add(new KrantSegment("", gaps.Count));
                    gaps.Add(gap);
                }
                else problems.Add($"Gat \"{m.Value}\" is ongeldig.");
                at = m.Index + m.Length;
            }
            if (at < paragraph.Length) segments.Add(new KrantSegment(paragraph[at..]));
            if (segments.Any(s => s.Gap < 0 && (s.Text.Contains("[[") || s.Text.Contains("]]"))))
                problems.Add("Er staat een [[ of ]] zonder tegenhanger in de tekst.");
            paragraphs.Add(segments);
        }
        return new Article(paragraphs, gaps, problems);
    }

    private static KrantGap? ParseGap(string inner)
    {
        var colon = inner.LastIndexOf(':');
        var body = (colon < 0 ? inner : inner[..colon]).Trim();
        var hint = colon < 0 ? "" : inner[(colon + 1)..].Trim();

        if (body.StartsWith('#'))
        {
            var m = NumberPattern().Match(body);
            if (!m.Success || !int.TryParse(m.Groups[1].Value, out var number)) return null;
            var tolerance = m.Groups[2].Success && int.TryParse(m.Groups[2].Value, out var t) ? Math.Max(1, t) : 1;
            return new KrantGap(number.ToString(), [], hint.Length > 0 ? hint : "getal", number, tolerance);
        }

        var names = body.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return names.Length == 0 ? null : new KrantGap(names[0], [.. names.Skip(1)], hint, null, 1);
    }

    [GeneratedRegex(@"\d{1,9}")]
    private static partial Regex Digits();

    /// <summary>
    /// Een ingetypt getal ("69", "69e", "€ 115") als getal: de eerste reeks cijfers, zodat "2-1" of "69 of 70" niet
    /// 21 of 6970 wordt. Null als er geen cijfers in staan (dan telt de gok niet en mag je opnieuw).
    /// </summary>
    public static int? ParseNumber(string? input) =>
        Digits().Match(input ?? "") is { Success: true } m ? int.Parse(m.Value) : null;
}
