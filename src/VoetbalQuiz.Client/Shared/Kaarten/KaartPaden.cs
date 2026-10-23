namespace VoetbalQuiz.Client.Shared.Kaarten;

/// <summary>Eén regio (provincie, regio, deelstaat, ...) als SVG-pad in de viewBox van het land.</summary>
public sealed record KaartRegio(string Id, string Naam, string D);

/// <summary>De grenzen van één land: de regio's en een aparte buitenlijn (landsgrens en kust).</summary>
/// <param name="Grenzen">Wat de binnenlijnen zijn, voor de aria-label: "Kaart van Frankrijk met regiogrenzen".</param>
public sealed record KaartVorm(string Grenzen, IReadOnlyList<KaartRegio> Regios, string Buitenlijn);

/// <summary>
/// Vaste SVG-paden per land, gegenereerd uit Natural Earth (Admin 1 – States, Provinces 1:10m, publiek domein,
/// https://www.naturalearthdata.com/about/terms-of-use/). Gedeelde grenzen zijn als één boog vereenvoudigd, zodat
/// buren exact op elkaar passen. De projectie per land staat in <see cref="VoetbalQuiz.Shared.KaartProjection"/>.
/// De data staat per land in KaartPaden.XX.cs.
/// </summary>
public static partial class KaartPaden
{
    /// <summary>De grenzen van dit land (ISO-code), of null als er geen kaart is.</summary>
    public static KaartVorm? Find(string? land) => land?.Trim().ToUpperInvariant() switch
    {
        "BE" => BE,
        "FR" => FR,
        "IT" => IT,
        "ES" => ES,
        "DE" => DE,
        "GB" => GB,
        _ => null,
    };
}
