# 002 – Herwerking van de klassementen

- **Soort:** feature
- **Status:** idee
- **Prioriteit:** midden
- **Moeite:** L
- **Aangemaakt:** 2026-10-20
- **Bron:** eigen idee

## Waarom

De klassementen moeten herwerkt worden. Wat er precies beter moet, ligt nog niet vast (zie open vragen).

## Wat

Nog uit te werken. Dit bestaat er vandaag:

- **Klassementscherm** (`Klassement.razor`): twee tabbladen.
  - *Vandaag*: enkel de daily van vandaag.
  - *Maand*: enkel het totaal (daily + specials + online + bonussen), met bladeren per maand (`MonthRankingService`, `MonthRules`).
- **Elders:** een eigen klassement per special, het online klassement met rating (`OnlineLeaderboard`), de Gouden Bal (`GoldenBallService`) en je plaats in het dagklassement op Home.
- Alles wordt getoond via dezelfde lijst (`LeaderboardList`): rang, naam, avatar, punten.

## Open vragen

- **Wat werkt er nu niet?** Overzicht (te veel losse plekken)? Motivatie (wie ver achterstaat, haakt af)? Weinig info (enkel een totaal)? Te weinig variatie?
- **Welke klassementen wil je?** Bv. een week- of seizoensklassement, all-time, per vraagtype of per special, tussen vrienden of een eigen groep (minicompetitie)?
- **Wat toon je per rij?** Opsplitsing van het maandtotaal (daily / specials / online / bonus), stijging of daling sinds gisteren, reeksen, badges?
- **Waar sta ik?** Altijd je eigen rij tonen (vastgepind) en de spelers rond jou, in plaats van enkel de top?
- **Divisies:** promotie en degradatie tussen reeksen per maand, zodat iedereen om iets speelt?
- **Beloning:** wat krijg je voor een eindklassering (badge, titel, Gouden Bal-punten)?
- Moeten de bestaande klassementen (special, online, Gouden Bal) samen op één scherm komen?

## Aanpak (optioneel)

Hangt af van de keuzes. Het raakt waarschijnlijk:

- `ResultsService`, `MonthRankingService` en `OnlineService` op de server;
- de klassement-DTO's in Shared (`Leaderboard`, `MonthRanking`, `OnlineLeaderboard`);
- `Klassement.razor`, `LeaderboardList` en Home op de client;
- mogelijk een migratie (bv. voor groepen of divisies).

Laat het ontwerp eventueel uitwerken door `ux-designer`.

## Notities

