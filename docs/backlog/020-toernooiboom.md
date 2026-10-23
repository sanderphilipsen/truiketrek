# 020 – Toernooiboom: de knock-outfase invullen

- **Soort:** feature
- **Status:** concept
- **Prioriteit:** laag
- **Moeite:** M
- **Aangemaakt:** 2026-10-23
- **Bron:** brainstorm over puzzels die een hele daily vullen (zoals [008](008-vraagtype-kaart.md) en [009](009-vraagtype-raster.md))

## Waarom

Een toernooiboom is een beeld dat elke voetbalfan kent. Hem reconstrueren vraagt een ander soort kennis dan namen: wie stond tegen wie, en wie ging door. Het is een sterke daily rond een groot toernooi (bv. tijdens het WK 2026 of als terugblik op het EK 2000 in eigen land).

## Wat

- Een **speciale daily** rond één toernooi, bv. **WK 1986** of **EK 2000**.
- De speler ziet de **boom van de knock-outfase**: kwartfinales (of achtste finales), halve finales en de finale.
- Per wedstrijd kiest hij in een **keuzelijst** de ploegen en wie doorging. Een gekozen winnaar schuift door naar de volgende ronde.
- Een deel van de boom staat al ingevuld als houvast (bv. de finale of één kwartfinale), zodat het niet onhaalbaar is.
- Na het indienen kleurt de boom groen en rood, met de echte uitslagen erbij (inclusief penalty's en verlengingen).

## Open vragen

- **Invullen per ronde of alles tegelijk:** alles invullen en dan indienen (zoals een pronostiek), of ronde per ronde met tussentijdse controle? Bij ronde per ronde is een fout in de kwartfinale minder rampzalig.
- **Doorwerkende fouten:** als je de verkeerde ploeg in de halve finale zet, krijg je dan nog punten voor de juiste winnaar verder in de boom?
- **Keuzelijst:** enkel de ploegen die het toernooi speelden, of alle deelnemers inclusief die uit de groepsfase (moeilijker)?
- **Extra's:** ook de uitslag of de doelpuntenmaker van de finale vragen?
- **Formaat op 375 px:** een boom van 8 of 16 ploegen past niet in de breedte. Ronde per ronde scrollen (horizontaal), of een verticale weergave?
- **Welke toernooien:** WK's, EK's, Champions League, Beker van België?
- **Punten binnen 1000** en **tijd** voor de hele boom?

## Aanpak (optioneel)

- **Data:** een JSON-bestand per toernooi met de wedstrijden per ronde, de uitslagen en welke plaatsen voorgegeven zijn, met een bron. Te schrijven door `speler-redacteur`.
- **Shared:** nieuw `QuestionType` (bv. `Toernooiboom`), regels in `QuestionRules`. De dag via het mechanisme van specials.
- **Server-autoriteit:** de client krijgt de lijst ploegen en de voorgegeven plaatsen, nooit de uitslagen. De server rekent per wedstrijd de punten.
- **Client:** het lastigste deel is de weergave van de boom op een gsm; laten ontwerpen door `ux-designer` en testen door `mobiel-tester`.
- Volg de skill `nieuw-vraagtype`; laten nakijken door `spelregel-reviewer`.

## Notities

- 2026-10-23: idee gekozen door de eigenaar uit een brainstorm (samen met [018](018-lijstdag.md), [019](019-matchverslag.md) en [021](021-codekraker.md)). Prioriteit laag gezet omdat het vooral rond een groot toernooi leuk is; het WK 2026 is al gespeeld, dus een terugblik of een volgend toernooi is het logische moment.
- Raakt aan [011 Op volgorde](011-vraagtype-op-volgorde.md) (ook iets ordenen), maar het mechanisme is anders.
