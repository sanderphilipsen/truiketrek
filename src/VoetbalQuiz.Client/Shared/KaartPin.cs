namespace VoetbalQuiz.Client.Shared;

/// <summary>Toestand van een speld op de kaart van België.</summary>
public enum KaartPinState { Wacht, Huidig, Juist, Deels, Fout }

/// <summary>
/// Een speld op <see cref="BelgieKaart"/>: coördinaten, toestand en (na het antwoord) de clubnaam. Bij de huidige speld
/// enkel als de kaart <c>LabelHuidig</c> krijgt (deel 2: de club staat erbij, je zoekt het stadion).
/// </summary>
public record KaartPin(double Lat, double Lon, KaartPinState State, string? Label = null);
