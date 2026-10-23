# 023 – Inzetdag: punten inzetten per vraag

- **Soort:** feature
- **Status:** concept
- **Prioriteit:** midden
- **Moeite:** S
- **Aangemaakt:** 2026-10-23
- **Bron:** tweede brainstorm over puzzels die een hele daily vullen (zoals [008](008-vraagtype-kaart.md) en [009](009-vraagtype-raster.md))

## Waarom

Een nieuwe laag bovenop vragen die al bestaan: er is geen nieuwe inhoud en geen nieuw vraagtype nodig. Het beloont spelers die weten **wat ze weten**, en het geeft elke vraag spanning, omdat je vooraf moet kiezen hoeveel je durft. Zo krijg je een heel andere daily met weinig bouwwerk.

## Wat

- Een **speciale daily** met gewone vragen (bv. 8 vragen van verschillende types).
- Vóór elke vraag ziet de speler **alleen de categorie en de moeilijkheid**, bv. "Rugnummer, ★★★★".
- Hij **zet punten in** uit een budget (bv. 1000), met een schuifbalk of vaste stappen (50, 100, 200, alles).
- **Juist:** hij krijgt zijn inzet terug plus winst. **Fout:** de inzet is weg.
- Het budget gaat mee van vraag naar vraag. Wie op het einde alles op één vraag zet, kan de dag nog ommekeren.
- De eindscore is het budget op het einde.

## Open vragen

- **Daglimiet van 1000:** de eindscore kan boven 1000 uitkomen. Rekenen we terug naar 0-1000 (bv. eindbudget / maximum × 1000), of telt een vaste omzetting?
- **Winst bij juist:** altijd inzet × 2, of afhankelijk van de moeilijkheid (makkelijk × 1,5, moeilijk × 3)? Laat `spel-econoom` dit doorrekenen.
- **Minimale inzet:** moet je per vraag iets inzetten (bv. minstens 50), zodat je niet alles op 0 kan zetten en op veilig spelen?
- **Failliet:** wat als het budget op 0 staat vóór de laatste vraag? Uitgespeeld, of een kleine "noodlening"?
- **Tijd:** de gewone tijd per vraag, plus een korte tijd om in te zetten?
- **Gedeeltelijk juist:** hoe werkt de inzet bij types met gedeeltelijke punten (Op volgorde, Raad het jaar)? Een deel van de winst?
- **Online:** een inzetronde in een online wedstrijd, waarbij je de inzet van de tegenstander na de vraag ziet?
- **Academie:** doet de inzetdag daar ook mee?

## Aanpak (optioneel)

- **Geen nieuw vraagtype:** het is een **dagformaat** dat de bestaande vragen hergebruikt. Bekijken waar dat het best past: een vlag op de special (bv. `Inzet = true` in `SpecialTemplates`) of een eigen soort dag.
- **Shared:** regels voor budget, minimale inzet, winstfactor en omzetting naar de daglimiet in `QuestionRules` (of een eigen `BetRules`), niet hardcoded in Server of Client.
- **Server-autoriteit:** de inzet wordt op de server vastgelegd **vóór** de vraag getoond wordt, en kan daarna niet meer wijzigen. De server rekent de winst en het budget.
- **Client:** een inzetscherm vóór elke vraag (categorie, moeilijkheid, budget, schuifbalk) en na de vraag het budget dat op- of afloopt. `animatie-bouwer` kan het optellen beleving geven.
- Laten nakijken door `spelregel-reviewer` (daglimiet, server-autoriteit) en `spel-econoom` (winstfactor, uitbuitbare trucs).

## Notities

- 2026-10-23: idee gekozen door de eigenaar uit een tweede brainstorm (samen met [022](022-detectivedag.md)).
