# 031 – Correctie op een gespeelde dag

- **Soort:** feature (admin)
- **Status:** concept
- **Prioriteit:** midden
- **Moeite:** M (fase 1), later M
- **Aangemaakt:** 2026-10-25
- **Bron:** eigen idee

## Waarom

Soms blijkt pas na een daily dat er iets mis was: het "juiste" antwoord klopte niet, een tweede antwoord was ook goed, een tip verraadde of misleidde, of een vraag was gewoon fout. Vandaag kan de admin de vraag wel aanpassen in de vragenbank, maar de scores van wie die dag al gespeeld heeft, blijven staan. Spelers die benadeeld werden, verliezen punten in het dag- en maandklassement, en dat voelt oneerlijk.

De admin wil een foute vraag achteraf kunnen rechtzetten op een gespeelde dag, waarna de scores (en alles wat ervan afhangt) herberekend worden. Een correctie moet ook weer ongedaan gemaakt kunnen worden.

## Wat

### Fase 1: een vraag schrappen of neutraliseren

In het admin-paneel, bij een gespeelde dag (daily of special), per vraag een knop **Correctie** met één keuze:

| Keuze | Wat er gebeurt | Gevolg |
|---|---|---|
| **Neutraliseren** (standaard) | Iedereen die de vraag speelde, krijgt de volle punten van die vraag | Niemand gaat achteruit; wie fout had, krijgt de punten "gratis" |
| **Schrappen** | Die vraag levert niemand nog punten op | Wie het juist had, verliest de punten; de dagwinnaar kan wisselen |

Waarom dit volstaat: het werkt voor **elk vraagtype en ook voor oude dagen**, want we hoeven niet te weten wat iemand gegokt heeft. (De foute gokken zelf worden vandaag niet bewaard, enkel de laatste gok en het aantal foute gokken; zie "Later".)

- Elke correctie krijgt een korte **reden** ("Mertens speelde ook bij PSV"), wordt bewaard met datum en admin, en kan **teruggedraaid** worden. Terugdraaien zet de oorspronkelijke punten terug.
- Vóór het toepassen toont de admin een **voorbeeld**: hoeveel spelers erop vooruit of achteruit gaan, en of de dagwinnaar, de specialwinnaar of de top van de maand verandert. Pas na bevestigen wordt het toegepast.
- Spelers zien bij het resultaat van die dag "Correctie: vraag 4 geneutraliseerd (+80 punten)" met de reden.

### Later (fase 2, optioneel): een extra of ander juist antwoord goedkeuren

Fijner dan neutraliseren: enkel wie het (alternatief) juiste antwoord gaf, krijgt de punten.

- Kan meteen bij types waar het gekozen antwoord bewaard wordt: meerkeuze, niet-bij, jaar (`Choice`), op volgorde, opstelling, counter (`Found`), kaart (`Picks`).
- Bij getypte antwoorden (Wie ben ik?, carrièrepad, wissel, rugnummer, mannetje, emoji, kluts) pas met een **gokkenlog** per poging (tekst + tijdstip + tip), want nu kennen we enkel de laatste gok. Die log helpt ook [001](001-moeilijkheid-herberekenen.md).
- Het extra antwoord meteen ook in de vragenbank zetten (aliassen, `EditedInAdmin`) zodat het bij hergebruik goed is.

## Wat er herberekend moet worden

Uit het datamodel (2026-10-25):

- **Live berekend uit `Attempt.Points`**, past zich vanzelf aan zodra de punten kloppen: dagklassement, maandklassement (`MonthRankingService`), XP en level, de meeste badges (`ProgressService`), statistieken.
- **Vastgelegd, moet expliciet herrekend worden:**
  - `DailyChallenge.WinnerUserId` / `WinnerPoints` (dagwinnaar, badges Dagwinnaar/Dagkoning, dagwinnaarbonus).
  - Het klassement en de winnaar van een **special** (beker + badge Specialist).
  - `GoldenBall` per maand, als de correctie in een al afgesloten maand valt (`GoldenBallMonth`).
  - `AppUser.SeenBadges`: een badge die door een schrapping wegvalt, was misschien al getoond.
- **Niet geraakt:** `TrainingAttempt` (telt nergens mee) en online matchen (eigen rating).

## Open vragen

- **Daglimiet van 1000:** neutraliseren kan nooit boven `DayMaxPoints` uitkomen (de volle punten van de vraag zaten al in de 1000). Wel nagaan bij speciale dagen met een andere verdeling (bv. Detectivedag, opstelling van 200 punten).
- **"Volle punten" per type:** bij Wie ben ik? en snelvuur hangen de punten af van tip en snelheid. Is "vol" het maximum van de vraag (`MaxPoints`), of het beste dat iemand die dag haalde? Voorstel: het maximum.
- **Wie speelde de vraag?** Neutraliseren enkel voor wie de vraag startte, of voor iedereen die die dag speelde (ook wie stopte vóór die vraag)? Voorstel: enkel wie ze startte.
- **Hoe ver terug?** Correcties in een afgesloten maand met een toegekende Gouden Bal: toelaten, maar de Gouden Bal enkel verschuiven na extra bevestiging? Of de Gouden Bal laten liggen?
- **Spelers verwittigen:** een melding (`Notification`) aan wie punten bij- of afkreeg, of enkel een vermelding bij het dagresultaat? Voorstel: melding enkel bij schrappen (punten kwijt), vermelding altijd.
- **Wie mag het?** Enkel admins; elke correctie gelogd (wie, wanneer, waarom).

## Aanpak (optioneel)

- **Data (migratie):** `Attempt.OriginalPoints` (null = nooit gecorrigeerd) en een entiteit `ScoreCorrection` (dag, vraag, keuze, reden, admin, tijdstip, teruggedraaid). Punten worden in `Attempt.Points` overschreven, zodat alle bestaande queries blijven werken; terugdraaien = `OriginalPoints` terugzetten.
- **Shared:** de regel voor "volle punten" per vraag, DTO's voor het voorbeeld en de vermelding.
- **Server:** `CorrectionService` (voorbeeld berekenen, toepassen, terugdraaien) die daarna dagwinnaar, specialwinnaar en (na bevestiging) Gouden Bal opnieuw bepaalt. Admin-endpoints.
- **Client:** correctiescherm per gespeelde dag in admin; vermelding bij het dagresultaat van de speler.
- **Tests (test-bouwer):** neutraliseren en schrappen, terugdraaien geeft exact de oude scores, dagwinnaar wisselt, twee correcties na elkaar, daglimiet blijft gerespecteerd.
- **Review:** spelregel-reviewer (punten op de server, daglimiet) en security-wachter (admin-endpoint).

## Notities

- 2026-10-25: fase 1 beperkt tot schrappen en neutraliseren (werkt voor alle types en oude dagen, geen gokkenlog nodig). Een extra/ander antwoord goedkeuren schuift naar later.
