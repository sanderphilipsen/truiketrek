# 024 – Puzzeldag: 12 woorden, 3 verbanden

- **Soort:** feature
- **Status:** concept
- **Prioriteit:** midden
- **Moeite:** M
- **Aangemaakt:** 2026-10-23
- **Bron:** idee van de eigenaar ("de puzzel uit De Slimste Mens ter wereld"), uitgewerkt in de tweede brainstorm over puzzels die een hele daily vullen

## Waarom

Elke Vlaming kent de Puzzel uit De Slimste Mens: 12 woorden op een bord, 3 verborgen verbanden. Het formaat is meteen duidelijk, vraagt associatief denken in plaats van één feit, en lokvogels zorgen voor het "aha"-moment. Een voetbalversie is een daily om mee uit te pakken.

## Wat

- Een **speciale daily** met **2 of 3 puzzels** na elkaar.
- Per puzzel ziet de speler **12 woorden** in een raster van 3×4. Ze vormen **3 groepen van 4** met elk een verborgen verband.
- Hij **typt een verband** in. Is het juist, dan lichten de 4 bijhorende woorden op en schuiven ze samen.
- Er zitten bewust **lokvogels** tussen: woorden die bij twee verbanden lijken te passen.
- Eén klok per puzzel; punten per gevonden verband.

Voorbeeld:

| | | | |
|---|---|---|---|
| Courtois | Bosuil | Buffalo's | Pfaff |
| Rouches | Jan Breydel | Mignolet | Kanaries |
| Lotto Park | Preud'homme | Great Old | Cegeka Arena |

Verbanden: *keepers van de Rode Duivels*, *stadions*, *bijnamen van clubs*. Lokvogels: Bosuil en Great Old (allebei Antwerp), Preud'homme (speelde bij Standard, de Rouches).

### Variant: Groepjesdag (makkelijker)

Uit de eerste brainstorm: de speler **tikt 4 tegels aan** om een groep te vormen in plaats van het verband te typen (zoals NYT Connections). Geen vrije invoer, dus geen discussie over formuleringen. Mogelijke combinatie: eerst groeperen, dan het verband benoemen voor extra punten.

## Open vragen

- **Typen of tikken:** het verband intypen (zoals in het programma, moeilijker te controleren), tegels groeperen (Groepjesdag), of allebei?
- **Formuleringen:** hoe ruim aanvaarden we een verband? "Keepers", "doelmannen", "Belgische keepers" moeten goed zijn. Een lijst aanvaarde formuleringen per verband, en wat met een antwoord dat er niet in staat (net als ⏳ bij het [Raster](009-vraagtype-raster.md), of gewoon fout)?
- **Punten binnen 1000:** per verband gelijk, of een bonus voor een volledig opgeloste puzzel? Hoeveel puzzels per dag?
- **Tijd:** hoeveel per puzzel (bv. 60 seconden)? Mag je onbeperkt verbanden proberen binnen de tijd?
- **Na de tijd:** worden de ontbrekende verbanden en groepen getoond?
- **Rechten:** "De Slimste Mens" is een merknaam. We noemen het type zelf anders (Puzzeldag) en nemen alleen het spelprincipe over.
- **Hoe vaak, online en Academie:** vaste dag, special, of via het admin-paneel? Ook in online wedstrijden (wie vindt het eerst)?

## Aanpak (optioneel)

- **Data:** een JSON-bestand per puzzel: 12 woorden, 3 verbanden met elk 4 woorden en een lijst aanvaarde formuleringen, met een bron per feit. Te schrijven door `speler-redacteur`, na te kijken door `inhoud-controleur` (vooral: past geen enkel woord écht bij twee verbanden?).
- **Shared:** nieuw `QuestionType` (bv. `Puzzel`), regels in `QuestionRules`. De dag via het mechanisme van specials (`Types` in `SpecialTemplates`).
- **Server-autoriteit:** de client krijgt de 12 woorden in willekeurige volgorde, nooit de groepen of verbanden. De server controleert het verband met `AnswerMatcher` en stuurt pas dan de 4 woorden terug.
- **Client:** raster van 3×4 op 375 px, een invoerveld boven het toetsenbord, gevonden groepen die samenschuiven (`animatie-bouwer`).
- Volg de skill `nieuw-vraagtype`; laten nakijken door `spelregel-reviewer`.

## Notities

- 2026-10-23: op de backlog gezet op vraag van de eigenaar, samen met [025 Open Deur](025-open-deur.md).
