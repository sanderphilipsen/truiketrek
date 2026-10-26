# Architectuuranalyse Truitje Trek – 26 oktober 2026: structuur en DDD

Tweede architectuurcheck, na de vraag "kunnen we de code beter structureren en een architectuur zoals DDD volgen?".
Geanalyseerd: `main` op c663b90. Fases 0 tot 3 uit [architectuur-2026-10-20.md](architectuur-2026-10-20.md) waren toen al
uitgevoerd (`AttemptFlow`, `QuestionPicker`, `MatchScoring`, `DerbyService`, `Match.Version`). Dit rapport gaat over de
schuld die sindsdien bijkwam en over het DDD-oordeel.

**Beslissing (26 oktober):** DDD-light. Zie [Status](#status) voor wat al uitgevoerd is.

## Uitgangssituatie

| Controle | Resultaat |
|---|---|
| `dotnet build --no-incremental` | geslaagd, 3 warnings (CS8604 in `LijstTests.cs:77,89,105`) |
| `dotnet test` | 509 groen, 1 overgeslagen (PostgreSQL-rooktest, Docker ontbreekt) |

## 1. Diagnose

### Pijnpunten (ernstigste eerst)

1. **`QuestionEngine.cs` (1062 regels) is de echte god-class (hoog).** Gegroeid van 465 naar 1062 regels in zes dagen.
   De 17 vraagtypes staan in een switch op tien plaatsen: `AnswerLabel`, `TimeLimit`, `Visible`, `Guess`, `WithAnswer`,
   `PointsWhenStopped`, `Result`, `Describe`, `KeyFor` en `Problems`. Een nieuw type wijzigt dus tien plaatsen in één
   bestand, plus `QuestionSeeder`. In `Visible` kan het antwoord van een ander type lekken zonder dat het opvalt.
2. **`Shared/QuestionTypes.cs` (618 regels) mengt drie dingen (middel):** de catalogus (`QuestionTypes`), de puntenregels
   van alle types (`QuestionRules`) en de inhoudsrecords met het antwoord (`SnelvuurData.Correct`, `DetectiveData.Culprit`, ...).
3. **`AdminService` (427 regels) heeft vijf redenen om te wijzigen (middel):** spelers, vragenbank, weekplanning, specials
   en gebruikers/dashboard, over 11 DbSets. "Admin" is geen domein maar een scherm bovenop alle domeinen.
4. **`Data/Entities.cs` (479 regels): één bestand voor alle entiteiten (middel).** Het meest gewijzigde bestand.
5. **Progressie is één grote feitenquery (middel).** `ProgressService` heeft 6 afhankelijkheden, een `Facts`-record met
   47 velden en de badge-catalogus in dezelfde klasse. Een nieuwe badge betekent een veld, een stuk query en een `BadgeDef`.
6. **Verantwoordelijkheden schuiven tussen `QuizService` en `ResultsService` (laag-middel).** `QuizService.GetSpecialsAsync`
   en `VisitDailyRankAsync` wikkelen `ResultsService` in. Leespaden schrijven (`CloseExpiredTodayAsync`, `SettleFinishedDaysAsync`).
7. **Anemische entiteiten, maar alleen waar het pijn doet (laag-middel).** Meestal geen probleem, omdat de logica in pure
   statische klassen staat. Het wringt bij `Match`/`MatchPlayer`: statusovergangen, plaatsen en "één wapen per speler"
   staan verspreid over `MatchService` en `DerbyService`.
8. **`DbContext` overal is géén probleem.** EF is al unit of work en repository. Enkel `AccountEndpoints.cs` (215 regels)
   spreekt zelf `db`/`UserManager` aan: dat endpoint is te dik.

### Wat goed zit en moet blijven

- Shared hangt nergens van af; de Client hangt enkel van Shared af.
- De server heeft het laatste woord: `Visible` en `Result` zijn gescheiden.
- Eén `AttemptState` voor daily, training en online.
- `MatchScoring` is pure logica; `ChangeAsync` met `Version` is in feite al een aggregate-grens.
- De klok is injecteerbaar (`QuizClock`/`QuizDay`).
- Geen overbodige lagen, en ruim 500 tests.
- Een eigen domeintaal: daily, special, derby, academie.

## 2. Oordeel over DDD

**Levert hier echt iets op:**
- Strategisch DDD: expliciete bounded contexts als mappen en namespaces, en een consequente domeintaal.
- Aggregates met invarianten, alleen waar ze bestaan: `Match` (concurrency, status, wapens), `DailyChallenge` (vast na
  aanmaak, ≤ 10 vragen, ≤ 1000 punten) en de poging (≤ 40 gokken, definitief na `AnsweredAt`).
- Polymorfisme per vraagtype: een `IQuestionKind` per type in plaats van tien switches. Geen DDD-patroon, wel de grootste winst.
- Een paar value objects waar het fouten voorkomt (de Belgische speeldag bestaat al als `QuizDay`).

**Overkill voor deze app** (één deploy, één instantie):
- Clean Architecture met 4 of meer projecten.
- Repositories bovenop EF: je verliest `Include` en `ExecuteUpdate`/`ExecuteDelete`.
- CQRS of MediatR.
- Domain events met een dispatcher.
- Overal private setters: dat vecht tegen EF en de JSON-seeders.

**Aanbeveling: DDD-light.** Strategische contexten als feature-mappen in de bestaande drie projecten, plus gerichte
tactiek: een strategie per vraagtype en methodes op de aggregates `Match` en `DailyChallenge`.

## 3. Doelstructuur

| Context | Aggregates / kern | Belangrijkste invarianten |
|---|---|---|
| **Inhoud** (vragenbank) | `Player`, `QuizItem`, seeders, `StockAlertService` | goedkeuren kan enkel als `Problems()` leeg is; de sleutel is uniek |
| **Spelmotor** (gedeelde kern) | `IQuestionKind` per type, `AttemptState`, `AttemptFlow`, `AnswerMatcher` | het antwoord gaat nooit mee in `Visible`; ≤ 40 gokken; punten uit de servertijd |
| **Daily & specials** (+ academie) | `DailyChallenge`+`DailyQuestion`, `Attempt`, `SpecialEvent`, `DailyScheduleDay`, `TrainingRun` | iedereen dezelfde vragen; ≤ 1000 punten per dag; vragen in volgorde; training telt nergens mee |
| **Online** | `Match` (spelers, vragen, antwoorden), `MatchScoring`, derby, rating, `MatchRealtime` | `Version`; plaatsen; statusovergangen; één wapen per speler |
| **Klassementen & progressie** | dag/maand/Gouden Bal, XP, badges (vooral projecties) | de winnaar ligt één keer vast; +50 voor de dagwinnaar |
| **Accounts & communicatie** | `AppUser`, Google-login, herstel, verwijderen, meldingen, feedback | — |

**Admin** wordt geen eigen context: `AdminService` wordt verdeeld over Inhoud, Daily (weekplanning en specials) en Accounts.

**Indeling:** `Server/Features/<Context>/` met de services en endpoints van die context. Eén `QuizDbContext`, met de
configuratie per context. Geen nieuwe projecten. **Shared** blijft één project (de client heeft regels en DTO's nodig),
maar krijgt mappen voor regels, contracten en inhoud.

## 4. Migratieplan

Elke stap: build en tests groen; routes, DTO's en SignalR-berichten ongewijzigd.

| Fase | Wat | Omvang | Risico |
|---|---|---|---|
| 0 | Vangnet: per vraagtype een test "`Visible` bevat nooit het antwoord"; de 3 warnings oplossen | S | geen |
| 1 | `Entities.cs` en `OnModelCreating` opsplitsen per context; namespace blijft `VoetbalQuiz.Server.Data` (geen migratie) | S | laag |
| 2 | `QuestionEngine` wordt een dispatcher plus 17 `IQuestionKind`-klassen, één type per stap (eerst `WieBenIk`/`Emoji`) | L (~6 stappen) | laag-middel |
| 3 | Feature-mappen voor services en endpoints; `AdminService` verdelen; `QuestionTypes.cs` splitsen; `AccountEndpoints` verdunnen | M | laag |
| 4 | Invarianten expliciet: `Join`/`Leave`/`Start`/`UseWeapon` op `Match`; één fabriek voor `DailyChallenge` met de 1000-puntenlimiet | M | middel (online-flow) |
| 5 | Optioneel, enkel als het gemeten traag is: dagafsluiting en XP uit de leespaden halen | M | middel |

Na fases 2 en 4: `spelregel-reviewer` laten meekijken.

## 5. Open vragen

1. ~~Volstaat DDD-light?~~ Ja (26 oktober).
2. Mogen de namespaces van de entiteiten wijzigen? (lege migratie en gewijzigde snapshot)
3. Logica als methodes op de entiteiten (een rijk `Match`), of pure statische klassen zoals nu?
4. Wordt de academie een eigen context of een submap van Daily?
5. Gaat de client mee in fase 2 (`QuestionPlay.razor`/`QuestionDisplay.cs` per type)?
6. Pauzeren we nieuwe vraagtypes tijdens fase 2?

## Status

| Stap | Status | Notitie |
|---|---|---|
| Fase 0 | uitgevoerd (4e2c6f4) | `NoLeakTests` over de echte inhoud, alle types, bij start/halverwege/einde; faalt voor een type zonder geheimen. Warnings opgelost |
| Fase 1 | uitgevoerd (4e2c6f4) | `Data/Model/` met per context de entiteiten en hun EF-configuratie; namespace gelijk, geen migratie |
| Fase 3 (deels) | uitgevoerd | `QuestionTypes.cs` gesplitst, badge-catalogus apart, doorgeefluiken in `QuizService` weg, `AdminService` verdeeld |

Niet gecontroleerd in de analyse: de Razor-pagina's, de migraties, `MonthRankingService`, `OnlineService`,
`NotificationService` en de seeders (enkel op hoofdlijnen), en de migraties tegen een echte PostgreSQL.
