namespace VoetbalQuiz.Shared;

/// <summary>
/// De kaart van één land (vraagtype Kaart): viewBox en projectie van lengte- en breedtegraad naar x/y in
/// <c>LandKaart.razor</c>. Equirectangulair, gecorrigeerd met cos(<see cref="CosLatDeg"/>) zodat het land niet
/// uitgerekt oogt. Eén definitie voor de kaart en de controle op de inhoud: een speld die <see cref="Contains"/>
/// doorstaat, valt altijd binnen de viewBox.
/// </summary>
/// <param name="Code">ISO-code van het land (BE, FR, IT, ES, DE, GB).</param>
/// <param name="Naam">Naam in een zin, met lidwoord waar nodig: "Kaart van {Naam}".</param>
/// <param name="Width">Breedte van de viewBox.</param>
/// <param name="Height">Hoogte van de viewBox.</param>
/// <param name="MinLon">Lengtegraad van de linkerrand.</param>
/// <param name="MaxLat">Breedtegraad van de bovenrand.</param>
/// <param name="Scale">Eenheden per breedtegraad.</param>
/// <param name="CosLatDeg">Middenbreedte (graden) voor de cos-correctie op de lengtegraad.</param>
public sealed record KaartLand(string Code, string Naam, double Width, double Height, double MinLon, double MaxLat, double Scale, double CosLatDeg)
{
    private readonly double cos = Math.Cos(CosLatDeg * Math.PI / 180);

    /// <summary>De onderrand van de kaart, afgeleid uit de viewBox.</summary>
    public double MinLat => MaxLat - Height / Scale;

    /// <summary>De rechterrand van de kaart, afgeleid uit de viewBox.</summary>
    public double MaxLon => MinLon + Width / (cos * Scale);

    public (double X, double Y) Project(double lat, double lon) =>
        ((lon - MinLon) * cos * Scale, (MaxLat - lat) * Scale);

    /// <summary>Valt dit punt binnen de kaart (de viewBox)?</summary>
    public bool Contains(double lat, double lon) =>
        lat >= MinLat && lat <= MaxLat && lon >= MinLon && lon <= MaxLon;
}

/// <summary>
/// De kaarten per land. De grenzen in <c>Client/Shared/Kaarten/KaartPaden.XX.cs</c> zijn in exact deze
/// projectie getekend: wie hier iets wijzigt, moet de paden opnieuw genereren.
/// </summary>
public static class KaartProjection
{
    // viewBox steeds ±380 breed en hoogstens 330 hoog (schermhoogte in het spel); hoge landen (IT, DE, GB) staan
    // gecentreerd met ruimte links en rechts. Gegenereerd met hetzelfde script als de paden.
    public static readonly KaartLand BE = new("BE", "België", 381, 326, 2.45, 51.58, 150, 50.5);
    public static readonly KaartLand FR = new("FR", "Frankrijk", 380, 330, -6.107, 51.336, 32.2, 46);
    public static readonly KaartLand IT = new("IT", "Italië", 380, 330, 4.066, 47.351, 30.1, 42);
    public static readonly KaartLand ES = new("ES", "Spanje", 380, 287, -9.605, 44.023, 34.8, 40);
    public static readonly KaartLand DE = new("DE", "Duitsland", 380, 330, 2.927, 55.264, 40.2, 51);
    public static readonly KaartLand GB = new("GB", "het Verenigd Koninkrijk", 380, 330, -12.29, 58.899, 36, 54.5);

    /// <summary>Alle landen met een kaart.</summary>
    public static readonly IReadOnlyList<KaartLand> Landen = [BE, FR, IT, ES, DE, GB];

    /// <summary>De kaart van dit land (ISO-code, hoofdletterongevoelig), of null als er geen kaart is.</summary>
    public static KaartLand? Find(string? land) =>
        Landen.FirstOrDefault(l => string.Equals(l.Code, land?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>De kaart van dit land (ISO-code); een onbekend land geeft een <see cref="ArgumentException"/>.</summary>
    public static KaartLand For(string land) =>
        Find(land) ?? throw new ArgumentException($"Geen kaart voor land '{land}'.", nameof(land));
}
