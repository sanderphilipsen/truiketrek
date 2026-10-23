# 008 – Kaartdag: "Wie speelt hier?"

- **Soort:** feature
- **Status:** concept
- **Prioriteit:** midden
- **Moeite:** M
- **Aangemaakt:** 2026-10-22
- **Bron:** brainstormgesprek over nieuwe vraagtypes; concept afgestemd met de eigenaar op 2026-10-23

## Waarom

Alle huidige types zijn intypen, meerkeuze of een getal schatten. Een kaart met spelden is een nieuwe manier van spelen, en weten waar een club thuis speelt is voetbalkennis die nog niet gevraagd wordt. Als aparte kaartdag is het een herkenbaar moment, bijvoorbeeld in een derbyweekend.

## Wat

- Een **speciale daily** met alleen dit type: de **kaartdag**. Het is geen type dat tussen de gewone daily-vragen opduikt.
- De speler ziet een **kaart van België met provinciegrenzen**. De **spelden** verschijnen **één voor één** op de stadions van een competitie (bv. alle clubs uit eerste klasse).
- Bij elke speld kiest de speler in een **keuzelijst** welke club daar speelt. Je tikt dus niet op de kaart, waardoor er geen probleem is met precisie op 375 px.
- Na het antwoord blijft de speld staan, groen (juist) of rood (fout), met de clubnaam. Zo vult de kaart zich gaandeweg.

### Beslissingen (2026-10-23)

- **De keuzelijst toont altijd alle clubs** van de competitie, ook de clubs die al geplaatst zijn. Zo is de laatste speld geen weggever en lukt gokken door eliminatie niet.
- **Gedeeld stadion** (bv. Jan Breydelstadion voor Club Brugge en Cercle Brugge, als beide in die competitie spelen): **één speld waarbij je beide clubs moet noemen**. Elke juiste club telt.
- **Punten per juiste speld en een tijdslimiet voor de hele kaart.**
- **Competities:** de **huidige eerste klasse** en de **Challenger Pro League** (tweede klasse). Historische seizoenen nu niet.
- Het eerdere voorstel om te tikken op de kaart (afstand in km, puntenschijven) is vervallen.

## Open vragen

- Tijdslimiet voor de hele kaart: 16 spelden in eerste klasse, Challenger Pro League ongeveer 16 tot 18. Bv. 10 seconden per speld?
- Puntentelling binnen 1000 per dag: 1000 / aantal spelden, of een vaste waarde per speld met een bonus voor een foutloze kaart?
- Volgorde van de spelden: willekeurig, of van makkelijk (grote clubs) naar moeilijk?
- Hoe vaak komt de kaartdag: vaste dag in een special (derbyweekend), maandelijks, of handmatig via het admin-paneel?
- Online mode en Academie: doet de kaartdag daar ook mee, of is het enkel een daily?

## Aanpak (optioneel)

- **Data:** `clubs.json` met per club een naam, aliassen, competitie(s), stadion en coördinaten (lat/lon), elk met een bron. Een kleine lijst (± 34 clubs) die je met de hand bijhoudt, geen Wikidata-koppeling nodig. Deze lijst kan [017 Clubraadsel](017-vraagtype-clubraadsel.md) hergebruiken (aliassen, stadion). Bij promotie of degradatie pas je de competitie aan.
- **Shared:** nieuw `QuestionType` (bv. `Kaart`) in `QuestionTypes.cs`, regels (tijd, punten) in `QuestionRules`. De kaartdag als dag met alleen dit type, via het mechanisme van specials (`Types` in `SpecialTemplates`).
- **Client:** SVG van België met provinciegrenzen (open data, bv. Statbel/NGI), een eenvoudige projectie van lat/lon naar x/y, spelden en een keuzelijst. Mobile-first, getest door `mobiel-tester` op 375 px.
- **Server-autoriteit:** de client krijgt alleen de coördinaten van de huidige speld en de lijst clubnamen, nooit welke club bij welke speld hoort. De server controleert en rekent de punten.
- Volg de skill `nieuw-vraagtype`; laten nakijken door `spelregel-reviewer`.

## Notities

- 2026-10-23: de planning stelde voor om dit item te schrappen (tikprecisie, overlap met Clubraadsel). Met het concept met spelden en een keuzelijst vervalt het precisieprobleem en daalt de moeite van L naar M. De eigenaar wil het behouden.
