# 008 – Vraagtype "Kaart van België"

- **Soort:** feature
- **Status:** idee
- **Prioriteit:** laag
- **Moeite:** L
- **Aangemaakt:** 2026-10-22
- **Bron:** brainstormgesprek over nieuwe vraagtypes

## Waarom

Alle huidige types zijn intypen, meerkeuze of een getal schatten. Tikken op een kaart is een nieuwe beweging, en geografie (waar ligt dat stadion?) is een kant van voetbalkennis die nog niet gevraagd wordt. Leuk in specials rond derby's.

## Wat

De speler ziet een kaart van België (later eventueel Europa) en een vraag zoals "Waar speelt KV Kortrijk?" of "Waar ligt het Jan Breydelstadion?". Hij tikt op de kaart; hoe dichter bij de juiste plek, hoe meer punten (zoals bij "Raad het jaar").

## Open vragen

- Alleen clubs en stadions, of ook geboorteplaatsen van spelers en historische matchen?
- Puntentelling op afstand in km: welke schijven (bv. < 5 km = 100, < 25 km = 60, ...)?
- Werkt een tik precies genoeg op een gsm van 375 px? Inzoomen nodig?
- Alleen België, of ook Europese stadions in specials (CL-finale, WK)?

## Aanpak (optioneel)

- Shared: nieuw `QuestionType`, `KaartData` (vraag, lat/lon), puntentabel in `QuestionRules`.
- Client: SVG-kaart van België met projectie lat/lon → x/y; de tik gaat als coördinaat naar de server, die de afstand en punten berekent.
- Data: `kaart.json` met coördinaten en bron per vraag.
- Volg de skill `nieuw-vraagtype`.
