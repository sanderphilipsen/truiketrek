# 016 – Revanche na een online wedstrijd

- **Soort:** feature
- **Status:** idee
- **Prioriteit:** midden
- **Moeite:** S
- **Aangemaakt:** 2026-10-22
- **Bron:** agent ideeen-brainstormer, gekozen door de gebruiker

## Waarom

De verliezer van een partijtje wil altijd nog een keer. Nu moet iemand opnieuw een tafel aanmaken, de instellingen kiezen en de link rondsturen, en tegen dan is de helft al weg. Eén knop houdt de groep samen.

## Wat

1. Op het **eindscherm** van een online wedstrijd staat een knop **Revanche!**.
2. Eén tik maakt een **nieuw partijtje met dezelfde instellingen** (vraagtypes, aantal vragen, derby-opties, boosters) en zet de speler meteen in de lobby.
3. De **andere deelnemers** van de afgelopen wedstrijd zien op hun eindscherm "Jan wil revanche: doe mee", met één tik naar de nieuwe lobby.
4. De nieuwe wedstrijd is een gewone wedstrijd: zelfde punten, zelfde regels, zelfde klassement.

## Open vragen

1. **Wie mag de revanche starten?** Iedereen, of alleen de gastheer van de vorige tafel? Wat als twee spelers tegelijk op Revanche tikken (één tafel, de tweede tik voegt toe aan de eerste)?
2. **Spelers die al weg zijn:** zien zij de uitnodiging nog bij een volgend bezoek, of alleen wie nog op het eindscherm staat?
3. **Daglimiet:** een revanche is een gewone wedstrijd en telt dus mee volgens de bestaande regels. Moet de knop verdwijnen als de speler zijn 1000 punten al heeft, of wordt dat op de gewone manier afgehandeld?
4. **Naam:** "Revanche!" of iets voetbalachtiger ("Terugmatch", "Verlengingen")?

## Aanpak (optioneel)

- **Server:** `MatchService` krijgt een `CreateRematch(matchId, userId)` dat de instellingen van de vorige wedstrijd kopieert en de nieuwe tafel aan de vorige koppelt (`RematchOfMatchId`, kleine migratie of alleen in het geheugen). Via de SignalR-groep van de oude wedstrijd (`Wedstrijd.razor`) een bericht "revanche aangemaakt" naar de deelnemers.
- **Client:** knop op het eindscherm in `Wedstrijd.razor`, de uitnodiging in de stijl van `RewardPopup`; hergebruik van het aanmaken uit `CreateTableForm.razor`.
- **Tests:** instellingen worden exact gekopieerd; een tweede tik maakt geen tweede tafel.
- Past goed in dezelfde ronde als een "Man van de match"-scherm na een wedstrijd (nog niet in de backlog).

## Notities

- 2026-10-22: gekozen uit de brainstorm samen met [013](013-daag-uit.md), [014](014-verzamelalbum.md) en [015](015-pushmeldingen.md).
