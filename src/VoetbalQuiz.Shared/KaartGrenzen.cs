namespace VoetbalQuiz.Shared;

/// <summary>
/// De omtrek van elk land van de kaartdag (zie <see cref="KaartProjection.Landen"/>), sterk vereenvoudigd, voor de controle
/// op de inhoud: een speld moet binnen haar land vallen, niet enkel binnen de rechthoek van de kaart (anders passeren Rijsel
/// op de kaart van België of een tekenfout in de lengtegraad). De ringen staan in KaartGrenzen.Data.cs (gegenereerd).
/// </summary>
public static partial class KaartGrenzen
{
    /// <summary>
    /// Zoveel kilometer buiten de (vereenvoudigde) omtrek mag een speld nog liggen: stadions aan de kust of vlak bij de grens
    /// vallen zo niet af door de vereenvoudiging. Ruim onder de afstand van Rijsel tot de Belgische grens (±10 km).
    /// </summary>
    public const double MarginKm = 5;

    /// <summary>Valt dit punt binnen het land (ISO-code), of hoogstens <paramref name="marginKm"/> erbuiten?</summary>
    public static bool Contains(string land, double lat, double lon, double marginKm = MarginKm)
    {
        if (!Ringen.TryGetValue(land.Trim().ToUpperInvariant(), out var rings)) return false;
        var cos = Math.Cos(lat * Math.PI / 180);
        foreach (var ring in rings)
        {
            if (Inside(ring, lat, lon)) return true;
        }
        return rings.Any(ring => DistanceKm(ring, lat, lon, cos) <= marginKm);
    }

    /// <summary>Ray casting: een even aantal snijpunten = erbuiten. De ring is plat: lat, lon, lat, lon, …</summary>
    private static bool Inside(double[] ring, double lat, double lon)
    {
        var inside = false;
        var n = ring.Length / 2;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            var (latI, lonI) = (ring[2 * i], ring[2 * i + 1]);
            var (latJ, lonJ) = (ring[2 * j], ring[2 * j + 1]);
            if (latI > lat != latJ > lat && lon < (lonJ - lonI) * (lat - latI) / (latJ - latI) + lonI) inside = !inside;
        }
        return inside;
    }

    /// <summary>De kortste afstand (km) van het punt tot de rand van de ring, vlak benaderd rond het punt.</summary>
    private static double DistanceKm(double[] ring, double lat, double lon, double cos)
    {
        (double X, double Y) Xy(int k) => ((ring[2 * k + 1] - lon) * 111.32 * cos, (ring[2 * k] - lat) * 110.57);
        var n = ring.Length / 2;
        var best = double.MaxValue;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            var (ax, ay) = Xy(j);
            var (bx, by) = Xy(i);
            double dx = bx - ax, dy = by - ay, len = dx * dx + dy * dy;
            var t = len == 0 ? 0 : Math.Clamp(-(ax * dx + ay * dy) / len, 0, 1);
            best = Math.Min(best, Math.Sqrt(Math.Pow(ax + t * dx, 2) + Math.Pow(ay + t * dy, 2)));
        }
        return best;
    }
}
