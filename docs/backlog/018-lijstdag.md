# 018 – Lijstdag: "Noem ze allemaal"

- **Soort:** feature
- **Status:** concept
- **Prioriteit:** midden
- **Moeite:** S
- **Aangemaakt:** 2026-10-23
- **Bron:** brainstorm over puzzels die een hele daily vullen (zoals [008](008-vraagtype-kaart.md) en [009](009-vraagtype-raster.md))

## Waarom

Een daily die één grote opdracht is, in plaats van losse vragen. Het "nog één, nog één..."-gevoel van een lijstquiz (Sporcle) is erg verslavend, en omdat de lijst gesloten is, is controleren eenvoudig: geen Wikidata, geen admin-review zoals bij het Raster. Achteraf zien hoeveel % een naam vond, geeft gespreksstof.

## Wat

- Een **speciale daily** met één opdracht, bv. **"Noem de 23 Rode Duivels van het WK 2018"**, "Alle winnaars van de Gouden Schoen sinds 2000" of "Alle Belgische landskampioenen".
- De speler ziet een lijst **lege plaatsen**, elk met een kleine hint (rugnummer, positie of jaartal), en één invoerveld.
- Hij typt namen in willekeurige volgorde. Elke treffer **klikt meteen op zijn plek**; een fout of dubbel antwoord kost niets behalve tijd.
- Er loopt één klok voor de hele lijst (bv. 4 minuten). Opgeven kan, dan worden de gemiste namen getoond.
- Na de dag ziet de speler per naam **hoeveel % van de spelers hem vond** ("Dendoncker: 18%").

Voorbeeldlijsten:

| Opdracht | Aantal |
|---|---|
| Selectie van de Rode Duivels op het WK 2018 | 23 |
| Gouden Schoen sinds 2000 | 25 |
| Landskampioenen van België (clubs) | ± 16 |
| Belgische topschutters in de Premier League | 10 |
| Basiself van Club Brugge in de bekerfinale van 2024 | 11 |

## Open vragen

- **Punten binnen 1000:** 1000 / aantal namen, of een vaste waarde per naam met een bonus voor een volledige lijst?
- **Tijd:** vaste 4 minuten, of afhankelijk van de lengte van de lijst (bv. 10 seconden per naam)?
- **Hints:** altijd zichtbaar, of pas na een tijd of tegen puntenverlies?
- **Dubbele lijsten:** een speler die twee keer in de lijst staat (bv. twee keer Gouden Schoen): telt die één keer of twee keer, en hoe tonen we dat?
- **Hoe vaak:** vaste dag in de week, in een special, of handmatig via het admin-paneel?
- **Online en Academie:** doet de lijstdag daar ook mee?

## Aanpak (optioneel)

- **Data:** een JSON-bestand per lijst (opdracht, hint per plaats, naam en aliassen per plaats, bron). Te schrijven door `speler-redacteur`.
- **Shared:** nieuw `QuestionType` (bv. `Lijst`), regels (tijd, punten) in `QuestionRules`. De dag zelf via het mechanisme van specials (`Types` in `SpecialTemplates`), net zoals de kaartdag.
- **Server:** elke ingetypte naam wordt meteen gecontroleerd met `AnswerMatcher` tegen de lijst; de server houdt bij welke plaatsen gevonden zijn en rekent de punten.
- **Server-autoriteit:** de client krijgt alleen de hints, nooit de namen, tot de lijst afgelopen is.
- **Client:** lijst met lege plaatsen die oplichten bij een treffer, één invoerveld onderaan dat boven het toetsenbord blijft staan op 375 px.
- Volg de skill `nieuw-vraagtype`; laten nakijken door `spelregel-reviewer`.

## Notities

- 2026-10-23: idee gekozen door de eigenaar uit een brainstorm (samen met [019](019-matchverslag.md), [020](020-toernooiboom.md) en [021](021-codekraker.md)).
