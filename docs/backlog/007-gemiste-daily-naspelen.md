# 007 – Academie: oude dailies en specials terugspelen

- **Soort:** feature
- **Status:** bezig
- **Prioriteit:** midden
- **Moeite:** M (eerste versie), L met de uitbreidingen
- **Aangemaakt:** 2026-10-20
- **Bron:** voorstel van Claude, gekozen en uitgebreid door de gebruiker

## Waarom

Wie een dag niet speelt, ziet die daily nooit meer. Dat is jammer voor de speler, die een reeks vragen mist, én voor de inhoud, want elke daily is met zorg samengesteld. Een trainingsmodus geeft:

- **gemiste dagen** een tweede leven;
- **nieuwe spelers** meteen veel meer te spelen dan één daily per dag;
- **fanatiekelingen** een plek om te oefenen en hun score op een oude dag te verbeteren, zonder de competitie te beïnvloeden.

## Wat

### Voor de speler

1. **Training** krijgt een eigen **tegel op Home**, in dezelfde stijl als de tegels van de daily en de online modus. Ze staat **onderaan** de pagina en is altijd zichtbaar, bv. "🏋️ Training – speel oude dailies opnieuw" met eventueel "3 gemist deze week".
2. Het **archief** toont per maand de dagen met een daily, de nieuwste bovenaan. In één oogopslag zie je of je een daily **al eens speelde**, echt of in training:
   - `ma 19 okt · ★★★ · nog nooit gespeeld` → **Spelen**
   - `zo 18 okt · ★★½ · ✓ echt 640 (3e van 12) · 2× getraind, beste 720` → **Opnieuw**
   - `za 17 okt · ★★★ · ✓ 1× getraind: 510` → **Opnieuw**
   - Een duidelijk kenmerk (vinkje, ingekleurde rij) voor "al gespeeld", en een ander voor "nog nooit gespeeld".
   - De daily van **vandaag** staat er pas in zodra de dag voorbij is (Belgische tijd). Je kan de competitie dus niet omzeilen door eerst te "trainen".
3. Tik je op een dag, dan zie je een **dagdetail** met **al je scores** op die daily:
   - je **echte poging** (als je die dag speelde): punten, juist/fout, plaats in het dagklassement, aangeduid als "Echt";
   - **elke trainingsrun**: datum en uur, punten, aantal juist, en of ze afgewerkt is; je beste run is gemarkeerd;
   - de knop **Train deze daily** (of **Opnieuw**).
4. Een trainingsdaily speel je **exact zoals de echte**: dezelfde 10 vragen, in dezelfde volgorde, met dezelfde timer, tips en strafpunten. Bovenaan staat steeds een duidelijk label: **🏋️ Training – telt niet voor het klassement**.
5. Na de laatste vraag volgt een **trainingsafsluiting** in plaats van de beloningspopup:
   - je score en het aantal juiste antwoorden;
   - "Daarmee was je **4e van 12** geweest op ma 19 okt" (vergeleken met de echte deelnemers van die dag);
   - je scorelijst op deze daily (echt + alle trainingsruns), met de run van zonet gemarkeerd, bv. "Echt: 640 · Training 1: 590 · **Training 2: 720 (nieuw record)**".

### Spelregels (voorstel)

| Vraag | Voorstel | Waarom |
|---|---|---|
| Welke dagen? | Elke gewone daily vóór vandaag die bestaat; die van gisteren zodra de dag voorbij is | Alles wat ooit was. Dagen waarop niemand speelde, hebben geen daily in de databank en worden **niet** achteraf aangemaakt. |
| Ook al gespeelde dagen? | Ja | Gevraagd door de gebruiker: oefenen en score verbeteren |
| Hoe vaak? | Onbeperkt opnieuw. Een nieuwe poging start de hele daily opnieuw | Bij herhalen ken je de antwoorden al, maar er hangt niets aan vast, dus dat is niet erg. |
| Wat bewaren we? | **Alle** runs, voor altijd, ook onafgewerkte. Per daily zie je je echte poging en elke trainingsrun | Gekozen: we bewaren ze |
| Wat levert het op? | **Niets** in de eerste versie: geen punten, geen XP, geen badges | Gekozen. XP of een trainingsbadge kan later (zie uitbreidingen). |
| Dag- en maandklassement, Gouden Bal | Telt niet mee | Competitie blijft enkel voor wie op de dag zelf speelt |
| Reeks "dagen op rij" | Telt niet mee, herstelt niets | Anders is de reeks waardeloos |
| Daglimiet 1000 punten | Niet van toepassing (training geeft geen punten) | |
| Statistieken ("juist geraden %", moeilijkste spelers) | Telt niet mee | Je kent bij herhaling de antwoorden |
| Moeilijkheid herberekenen ([001](001-moeilijkheid-herberekenen.md)) | Telt niet mee, of enkel de **eerste** trainingspoging op een dag die je nooit speelde | Herhalingen vertekenen het percentage |
| Specials | Niet in de eerste versie | Eigen flow, eigen klassement; later als uitbreiding |
| Boosters (Trump, Bengaals vuur, Wissel) | Niet van toepassing | Bestaan enkel in online matchen |

### Uitbreidingen (later, apart af te stemmen)

- **Losse vragen trainen:** "10 willekeurige vragen" uit het archief, per vraagtype (bv. enkel Opstelling) of per thema/tag (sluit aan bij [003 – Themakenners](003-themakenners.md)).
- **Afgelopen specials** terugspelen.
- **Beperkte XP** voor training (bv. max. 1 trainingsdaily per dag levert XP op) en een badge "Trainingsbeest" (bv. 25 trainingsdailies).

## Aanpak

### Data – aparte tabel (belangrijkste keuze)

Trainingspogingen komen **niet** in de tabel `Attempts`, maar in een eigen tabel. Daarvoor zijn drie redenen:

- `Attempts` heeft een unieke index op (`UserId`, `DailyQuestionId`): één poging per vraag. Training wil meerdere runs op dezelfde vraag, ook op vragen die je al echt speelde.
- Minstens acht queries lezen `db.Attempts` voor competitie en statistiek: `ResultsService` (dagklassement, winnaars, statistieken), `MonthRankingService`, `ProgressService` (XP, badges, reeksen), `AdminService` en `QuizService`. Met een vlag `Training` op `Attempt` moet elke query die (en elke toekomstige) filteren. Eén vergeten filter laat training meetellen in het klassement.
- Met een aparte tabel blijven alle bestaande klassementen, reeksen en badges ongewijzigd en is "training telt niet mee" vanzelf waar.

Voorstel:

```csharp
/// <summary>Eén trainingsrun: een oude daily die een gebruiker opnieuw speelt. Telt nergens mee.</summary>
public class TrainingRun
{
    public int Id { get; set; }
    public required string UserId { get; set; }
    public int DailyChallengeId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public List<TrainingAttempt> Attempts { get; set; } = [];
}

/// <summary>Een poging op één vraag binnen een trainingsrun (zelfde speltoestand als Attempt).</summary>
public class TrainingAttempt : AttemptState
{
    public int Id { get; set; }
    public int TrainingRunId { get; set; }
    public int DailyQuestionId { get; set; }
}
```

- De speltoestand die `QuestionEngine` nodig heeft (`StartedAt`, `AnsweredAt`, `Guesses`, `WrongGuesses`, `LastGuess`, `Correct`, `Points`, `Choice`, `Found`) gaat naar een gewone basisklasse `AttemptState`. Die is zelf geen EF-entiteit. `Attempt` en `TrainingAttempt` erven ervan, en `QuestionEngine` krijgt `AttemptState` in plaats van `Attempt`. De speellogica wordt zo hergebruikt zonder kopie, en de tabel `Attempts` verandert niet.
- Unieke index op (`TrainingRunId`, `DailyQuestionId`); `Guesses` als concurrency token, zoals bij `Attempt`.
- Migratie via skill `ef-migratie` (twee nieuwe tabellen; geen wijziging aan bestaande data).

### Server

- **Nieuwe `TrainingService`** (of een uitbreiding van `QuizService` met een "bron"). `QuizService` zit vast aan `Today` en `Attempts`; een aparte service houdt dat zuiver:
  - `GetArchiveAsync(userId, year, month)`: gewone dailies (`SpecialEventId == null`, `Date < Today`) met datum, moeilijkheid, of je ze al speelde, je echte score en plaats (uit `ResultsService`), het aantal trainingsruns en je beste trainingsscore.
  - `GetDayAsync(userId, date)`: het dagdetail met je echte poging (punten, juist, plaats) en de lijst van al je trainingsruns (start, punten, juist, afgewerkt, beste).
  - `StartRunAsync(userId, date)`: nieuwe run (een lopende, niet afgewerkte run op die dag wordt eerst afgesloten).
  - `StartAsync / GetStateAsync / GuessAsync / GiveUpAsync(userId, runId, number)`: dezelfde flow als de daily (volgorde afdwingen, `CloseIfExpired`, servertijd). De gedeelde stukken (`StateAsync`, `CloseIfExpired`, `SaveAsync`) verhuizen naar een kleine helper die beide services gebruiken.
  - `GetSummaryAsync(userId, runId)`: score, "je was X-e geweest" (rangschikking tegenover de echte dagtotalen van die datum), en het verschil met je echte score.
- **Endpoints** onder `/api/training` (met `RequireAuthorization` en `HandleQuizErrors`):
  `GET /archive/{year}/{month}`, `GET /{date}`, `POST /{date}/runs`, `POST /runs/{runId}/questions/{n}/start`, `GET /runs/{runId}/questions/{n}`, `POST .../guess`, `POST .../giveup`, `GET /runs/{runId}/summary`.
  Een run is altijd van de ingelogde gebruiker (anders 404).
- **Vandaag uitsluiten:** `date >= Today` → `QuizException("Deze daily kan je pas morgen trainen.")`. Ook bij een daily die voor vandaag gepland is maar nog loopt.
- **Antwoorden lekken niet:** dezelfde `QuestionState` als de daily. Het antwoord komt pas mee als de vraag afgelopen is.

### Shared

- DTO's: `TrainingArchive` (maand, dagen), `TrainingDay` (datum, moeilijkheid, al gespeeld, echte punten + plaats, aantal runs, beste training), `TrainingDayDetail` (echte poging + lijst `TrainingRunInfo`), `TrainingRunInfo` (start, punten, juist, afgewerkt, beste), `TrainingSummary`.
- Regels in Shared (bv. `TrainingRules`): of de daily van vandaag uitgesloten is, of training XP geeft (nu: nee). Dan staat dat niet hardcoded in de server.

### Client

- **Archiefscherm** `/training`, met maandnavigatie zoals het maandklassement (`DutchDates.MonthYear`) en een kenmerk "al gespeeld" per dag.
- **Dagdetail** `/training/{datum}` met al je scores op die daily (echt + trainingsruns) en de knop om te trainen.
- **Vraagscherm hergebruiken:** `Question.razor` krijgt een derde route `/training/{RunId:int}/vraag/{Number:int}`. `ApiClient` werkt nu met een prefix per bron (`api/daily` / `api/specials/{id}`); daar komt `api/training/runs/{id}` bij. Geen beloningspopup na de laatste vraag, maar een doorverwijzing naar de trainingsafsluiting.
- **Label "Training"** bovenaan, zodat het nooit verward wordt met de echte daily.
- **Home:** een tegel "Training" onderaan, in de stijl van de daily- en online-tegel (`home-daily`, `home-online`), eventueel met "Je miste 3 dailies deze week".

### Tests

- Training telt niet mee in dag- en maandklassement, Gouden Bal, reeks, XP, badges en statistieken. Test met een gebruiker die enkel traint, en met een die dezelfde dag echt én in training speelde.
- De daily van vandaag (en later) kan niet getraind worden.
- Meerdere runs op dezelfde dag, en vragen in volgorde.
- `QuestionEngine` geeft dezelfde punten voor `Attempt` en `TrainingAttempt` (bestaande tests blijven groen na de refactor naar `AttemptState`).
- Laten nakijken door `spelregel-reviewer` (server-autoriteit, geen lekken van antwoorden).
- Doorspelen op gsm-formaat met `mobiel-tester`.

### Stappen

1. Refactor `Attempt` → `AttemptState`-basisklasse, en `QuestionEngine` op `AttemptState` (geen gedragswijziging; tests groen).
2. Entiteiten `TrainingRun` + `TrainingAttempt`, en migratie.
3. `TrainingService` + endpoints + Shared-DTO's, met tests.
4. Client: tegel op Home, archief, dagdetail, route in `Question.razor`, afsluiting.
5. Review (`spelregel-reviewer`) en gsm-test (`mobiel-tester`).

## Beslissingen

- Spelregels zoals in de tabel; training levert in de eerste versie **niets** op.
- Training krijgt een **tegel onderaan op Home**, zoals de daily en de online modus.
- De daily van gisteren mag getraind worden **zodra de dag voorbij is**.
- **Alle trainingsruns worden bewaard.**
- Per daily zie je **al je scores**, inclusief je echte poging, en in het archief zie je of je een daily **al eens speelde**.

## Notities

- 2026-10-20: uitgebreid van "gemiste dailies naspelen" naar een trainingsmodus met alle dailies die ooit waren, ook al gespeelde.
- 2026-10-20: uitgewerkt tot een concreet voorstel (aparte tabel, `TrainingService`, archief, hergebruik van het vraagscherm).
- 2026-10-20: afgestemd met de gebruiker (zie Beslissingen) → status `klaar`. Toegevoegd: scorelijst per daily en kenmerk "al gespeeld" in het archief.
- 2026-10-20: gebouwd (nog niet gecommit). Aparte tabellen `TrainingRuns` + `TrainingAttempts` (migratie `TrainingRuns`), `AttemptState` als gedeelde basis voor `QuestionEngine`, `AttemptFlow` (klok en toestand), `TrainingService` + `/api/training`, pagina's `/training`, `/training/{datum}`, `/training/run/{id}`, een extra route in het vraagscherm en een tegel onderaan Home. Tests in `TrainingTests`. Niet gebouwd: de teller "je miste 3 dailies" op de Home-tegel.
- Opvolging: `QuizService` kan dezelfde `AttemptFlow` gebruiken voor de daily (nu staat die logica er nog dubbel), zodra het lopende moeilijkheidswerk in `QuizService.cs` gecommit is.
- 2026-10-20: voor de speler hernoemd naar **Academie** (adres `/academie`, pogingen onder `/academie/poging/{id}`). De Home-tegel toont enkel "🎓 Academie"; de uitleg staat in de academie zelf. In de code blijven de namen `Training…` (service, tabellen, `/api/training`).
- 2026-10-20: review door `spelregel-reviewer` verwerkt:
  - **Niets verklappen:** een oude daily die een speler of vraag deelt met de daily van vandaag (die je daar nog niet beantwoordde) blijft dicht tot je de daily van vandaag speelde. Gecontroleerd bij het starten van de run én per vraag; het dagdetail toont dan "Speel eerst de daily van vandaag".
  - Standaardmaand van het archief komt van de server (Belgische tijd), niet van de klok van de browser.
  - Een lege run (nog geen vraag gestart) wordt hergebruikt, zodat herhaald tikken geen runs opstapelt.
  - Dubbele tik op Start geeft geen serverfout meer.
  - Extra tests: gedeelde vragen, middernacht in Brussel, lege runs, standaardmaand.
  - Niet gedaan: `QuizService` laten werken met `AttemptFlow` (zie opvolging hierboven).
- 2026-10-20: ook **afgelopen specials** in de academie (gevraagd door de gebruiker). Een special staat in het archief op haar einddatum, in de stijl van een special-kaart, en kan pas herspeeld worden als ze afgelopen is (een lopende special telt nog voor haar klassement). Een special zonder reeks wordt niet achteraf samengesteld. "Je was X-e geweest" vergelijkt met het klassement van de special. Adres `/academie/special/{id}`; "Opnieuw spelen" start een nieuwe run op dezelfde daily of special (`POST /api/training/runs/{id}/again`). Geen migratie nodig: een run hangt aan een `DailyChallenge`, en een special heeft er ook een.
