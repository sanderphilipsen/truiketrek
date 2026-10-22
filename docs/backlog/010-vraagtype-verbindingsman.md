# 010 – Vraagtype "Verbindingsman"

- **Soort:** feature
- **Status:** idee
- **Prioriteit:** laag
- **Moeite:** L
- **Aangemaakt:** 2026-10-22
- **Bron:** brainstormgesprek over nieuwe vraagtypes

## Waarom

Een puzzel waarbij je zelf moet redeneren via kleedkamers ("wie speelde er met allebei samen?"), in plaats van één speler herkennen. Meerdere antwoorden kunnen juist zijn.

## Wat

Twee spelers verschijnen, bv. Vincent Kompany en Jan Vertonghen. De speler noemt een ploegmaat van allebei (bij dezelfde club of in de nationale ploeg). Moeilijkere variant: een ketting van twee schakels.

## Open vragen

- Telt de nationale ploeg mee, of alleen clubs?
- Moeten ze in hetzelfde seizoen samen gespeeld hebben (vraagt data per seizoen), of volstaat dezelfde club?
- Meer punten voor een zeldzame verbinding?
- Hoeveel pogingen en tijd?

## Aanpak (optioneel)

- **Voorwaarde:** dezelfde clubgeschiedenis per speler als [009](009-vraagtype-raster.md), liefst met seizoenen (van-tot).
- Server valideert de verbinding tegen de spelersdatabase; de client krijgt de oplossingen niet.
- Volg de skill `nieuw-vraagtype`.
