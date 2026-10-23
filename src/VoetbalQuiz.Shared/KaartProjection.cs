namespace VoetbalQuiz.Shared;

/// <summary>
/// De projectie van de kaart van België (vraagtype Kaart): lengte- en breedtegraad naar x/y in de viewBox van
/// <c>BelgieKaart.razor</c>. Eén definitie voor de kaart en de controle op de inhoud: een speld die
/// <see cref="Contains"/> doorstaat, valt altijd op de kaart.
/// </summary>
public static class KaartProjection
{
    /// <summary>Breedte en hoogte van de viewBox (de provinciegrenzen in BelgieKaart zijn in deze eenheden getekend).</summary>
    public const double Width = 381, Height = 326;

    /// <summary>De linkerbovenhoek van de viewBox: (2,45° O, 51,58° N).</summary>
    public const double MinLon = 2.45, MaxLat = 51.58;

    /// <summary>Eenheden per breedtegraad.</summary>
    public const double Scale = 150;

    // Equirectangulair, gecorrigeerd met cos(50,5°) zodat België niet uitgerekt oogt.
    private static readonly double CosLat = Math.Cos(50.5 * Math.PI / 180);

    /// <summary>De onderrand en de rechterrand van de kaart, afgeleid uit de viewBox.</summary>
    public static readonly double MinLat = MaxLat - Height / Scale;
    public static readonly double MaxLon = MinLon + Width / (CosLat * Scale);

    public static (double X, double Y) Project(double lat, double lon) =>
        ((lon - MinLon) * CosLat * Scale, (MaxLat - lat) * Scale);

    /// <summary>Valt dit punt binnen de kaart?</summary>
    public static bool Contains(double lat, double lon) =>
        lat >= MinLat && lat <= MaxLat && lon >= MinLon && lon <= MaxLon;
}
