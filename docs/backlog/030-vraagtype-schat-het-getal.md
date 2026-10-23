# 030 – Vraagtype "Schat het getal"

- **Soort:** feature
- **Status:** idee
- **Prioriteit:** midden
- **Moeite:** S
- **Aangemaakt:** 2026-10-23
- **Bron:** brainstorm nieuwe vraagtypes (2026-10-23)

## Waarom

"Raad het jaar" werkt omdat iedereen kan meedoen en dichtbij ook punten oplevert. Dezelfde mechaniek past op veel meer cijfers: transferbedragen, toeschouwers, leeftijd bij een debuut, de minuut van een legendarisch doelpunt. De score-logica bestaat al (`JaarPoints`, `KrantNumberScore`), dus dit is vooral nieuwe inhoud en een schuifbalk met een eigen bereik.

## Wat

- Eén vraag met een getal als antwoord, bv. **"Hoeveel betaalde Chelsea in 2019 voor Eden Hazard (in miljoen euro)?"** of "In welke minuut scoorde Chadli tegen Japan?"
- De speler zet een **schuifbalk** binnen een bereik dat bij de vraag hoort (bv. 50 tot 150 miljoen, of 1 tot 90+ minuten).
- **Hoe dichter, hoe meer punten**, met een tolerantie per vraag (1 minuut, 5 miljoen, 5.000 toeschouwers).
- Na het antwoord zie je het echte getal en waar jij zat.

## Open vragen

- Tolerantie en bereik per vraag in de data, of per categorie in `QuestionRules`?
- Hergebruiken we de puntenschaal van Raad het jaar, of een gladde schaal zoals bij De Krant?
- Welke categorieën starten we mee? Transferbedragen verouderen niet, maar "aantal caps" van een actieve speler wel.
- Is dit een apart type, of breiden we "Raad het jaar" uit tot "Raad het getal" met een categorie per vraag?

## Aanpak (optioneel)

- **Data:** JSON met vraag, antwoord, eenheid, bereik, tolerantie, bron.
- **Shared:** nieuw `QuestionType` (of uitbreiding van `Jaar`), puntenfunctie met tolerantie in `QuestionRules`.
- **Server:** de server rekent de afstand en punten; het antwoord komt pas na de gok naar de client.
- **Client:** de schuifbalk van Raad het jaar hergebruiken met variabel bereik en eenheid.
- Volg de skill `nieuw-vraagtype`.

## Notities

- 2026-10-23: toegevoegd na de brainstorm, zie ook [026](026-vraagtype-hoger-of-lager.md).
