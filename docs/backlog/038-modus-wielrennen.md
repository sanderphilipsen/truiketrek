# 038 – Modus Wielrennen

- **Soort:** feature
- **Status:** idee
- **Prioriteit:** laag
- **Moeite:** L (eerder XL: meerdere weken, zie "Inschatting")
- **Aangemaakt:** 2026-10-26
- **Bron:** eigen idee

## Waarom

Vlaanderen is minstens even gek van koers als van voetbal. Een tweede sport met dezelfde app (daily, klassementen, online, training, specials, badges) bereikt een nieuw publiek, en geeft het voorjaar (klassiekers) en de zomer (Tour) eigen hoogtepunten wanneer het voetbal stilligt.

## Wat

- De speler kiest een modus: **Voetbal** of **Wielrennen**. Alle features blijven, met eigen inhoud en eigen klassementen.
- Eigen vragenbank en rennersdatabase (renners in plaats van voetballers, ploegen in plaats van clubs).
- Eigen dag- en maandklassement, eigen dagwinnaar, en een eigen maandprijs in plaats van de Gouden Bal (bv. "de Gele Trui" of "de Flandrien van de maand").
- Eigen sfeer: kleuren, iconen en woorden uit de koers (derbywapens als "Waaier", "Bus in de afdaling", "Bidon", ... in plaats van rode kaart en bengaals vuur).
- Specials rond de koerskalender: Omloop, Ronde van Vlaanderen, Parijs-Roubaix, de Tour, het WK.

### Welke vraagtypes passen

| Past meteen | Past met een kleine aanpassing | Past niet / nieuw te bedenken |
|---|---|---|
| Wie ben ik?, Snelvuur, Carrièrepad (ploegen), Wie hoort er niet bij?, Raad het jaar, Emoji, Kluts, Counter, Op volgorde, De Zondag, Detectivedag, Lijst (winnaars van de Ronde, ...), Mannetje | Wissel (transfers tussen ploegen), Spelersfiche (velden: ploeg, specialiteit, grote overwinningen), Opstelling (een Tourploeg van 8 renners), Kaart (hellingen of finishplaatsen in plaats van stadions) | Rugnummer (wisselt per koers). Nieuwe ideeën: hoogteprofiel raden, podium invullen, kasseistroken op volgorde |

## Inschatting

Ruwe schatting met de werkwijze van nu (Claude + agents, jij die afstemt en nakijkt):

| Deel | Moeite | Wat |
|---|---|---|
| Sport als dimensie in het datamodel | 3-5 dagen | Kolom `Sport` op `Player`, `QuizItem`, `DailyChallenge` (unieke index wordt datum + sport), `DailyScheduleDay`, `SpecialEvent`, `GoldenBall`, online `Match`, `TrainingRun`. Migratie. Alle services die een daily, klassement of vraag kiezen krijgen de sport mee (QuestionPicker, QuizService, ResultsService, MonthRankingService, GoldenBallService, TrainingService, OnlineService, StockAlertService). Tests uitbreiden. |
| Woorden en sfeer per sport | 2-3 dagen | Voetbaltermen staan nu hardcoded in Shared (QuestionTypes, DerbyRules, Commentary, Avatars) en in een twintigtal Razor-schermen. Die moeten naar een tekst- en themaset per sport. Modus-keuze op Home en in het profiel. |
| Vraagtypes geschikt maken | 2-4 dagen | De kleine aanpassingen uit de tabel hierboven; een nieuw kaartbestand voor hellingen of finishplaatsen. Nieuwe types apart (eigen backlog-items). |
| Admin-paneel per sport | 1 dag | Filter op sport bij spelers, vragen, planning en specials. |
| **Inhoud** | **het grootste deel** | Voetbal heeft nu ±430 spelers en ±2.400 vragen. Om te starten is minimum ±150 renners en ±800-1.000 vragen nodig (genoeg voor een paar maanden daily zonder herhaling), allemaal met bronnen en nagekeken. Met speler-redacteur + inhoud-controleur reken op enkele weken, vooral door het nakijken. |

**Samen:** ±2 weken bouwwerk plus enkele weken inhoud. Het bouwwerk kan deels vóór de inhoud, maar de modus kan pas live als er genoeg goedgekeurde inhoud is.

**Goedkoper alternatief:** een aparte installatie ("Truitje Trek Koers") van dezelfde code, met een instelling die bepaalt welke sport en welke teksten. Geen `Sport`-kolom en geen extra klassementen in de code (scheelt ±3-4 dagen), maar wel aparte accounts, een aparte database en een tweede Azure-app om te onderhouden. Het werk aan woorden en sfeer blijft nodig.

## Open vragen

- **Eén app met twee modi, of twee aparte installaties?** Bepaalt het grootste deel van het technische werk.
- **Gedeeld of apart per sport?** XP en level, badges, avatar, reeksen (streak), vrienden. Voorstel: account, avatar en vrienden gedeeld; XP, klassementen en badges apart.
- **Daglimiet van 1000 punten:** per sport (dus 2000 per dag mogelijk), of samen?
- **Online wedstrijden:** kies je de sport bij het openen van een tafel, of kan een gemengde tafel?
- **Naam en maandprijs** voor de wielrenmodus.
- Wie zorgt voor de inhoud, en hoeveel moet er klaar zijn vóór de start?

## Aanpak (optioneel)

1. Open vragen afstemmen (vooral: één app of twee).
2. software-architect: de sport-dimensie (of de instelling per installatie) uitwerken en in kleine stappen bouwen.
3. Teksten en thema per sport uit de code halen; ux-designer voor de koerssfeer.
4. Parallel: speler-redacteur en vraag-toevoegen voor de startinhoud, inhoud-controleur en moeilijkheid-inschatter om na te kijken.
5. Eerst intern spelen met een kleine groep, dan openzetten rond een klassieker (bv. Omloop eind februari).

## Notities

- Raakt bijna elke laag, dus best niet tegelijk bouwen met andere grote features zoals [033 Divisies](033-divisies.md).
