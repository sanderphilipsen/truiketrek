# 001 – Moeilijkheid automatisch herberekenen

- **Soort:** techniek
- **Status:** concept
- **Prioriteit:** midden
- **Moeite:** L (fase 1: M)
- **Aangemaakt:** 2026-10-20
- **Bron:** eigen idee

## Waarom

De moeilijkheid (1-5 sterren) van een speler of vraag wordt nu eenmalig vastgelegd door de AI (agent `moeilijkheid-inschatter` of bij het schrijven door `speler-redacteur`). Die inschatting klopt niet altijd: een "makkelijke" vraag die bijna niemand juist heeft, of een "echte kenner" die iedereen weet, verstoort de opbouw van een reeks (`Difficulty.Plan`: opwarmer van 1 ster, uitsmijter van 5). Zodra een vraag gespeeld is, weten we hoe moeilijk ze écht is.

## Analyse van de huidige toestand (2026-10-20)

### 1. De labels zitten op een oude schaal van drie

Goedgekeurde items per ster, uit de JSON-bestanden:

| Bestand | 1 | 2 | 3 | 4 | 5 |
|---|---|---|---|---|---|
| snelvuur (420) | 64 | 21 | 213 | 33 | 89 |
| jaar (272) | 39 | 8 | 133 | 26 | 66 |
| niet-bij (208) | 22 | 6 | 120 | 14 | 46 |
| opstelling (169) | 17 | 2 | 79 | 16 | 55 |
| emoji (140) | 38 | 2 | 77 | 3 | 20 |
| carriere (109) | 38 | 0 | 43 | 0 | 28 |
| wissel (31) | 17 | 0 | 14 | 0 | 0 |
| rugnummer (30) | 15 | 0 | 14 | 0 | 1 |
| mannetje (27) | 17 | 0 | 10 | 0 | 0 |
| spelers (387) | 83 | 17 | 205 | 33 | 49 |

Bijna alles staat op 1, 3 of 5. Het plan vraagt per daily twee vragen van 2 sterren en twee van 4; die zijn er nauwelijks, dus `PickByPlan` vult ze met de dichtstbijzijnde (3, of een tweede 1/5 die het juist probeert te vermijden). Bij wissel, mannetje en rugnummer is er zelfs bijna geen uitsmijter. **Dit probleem is groter dan "sommige labels kloppen niet"**, en meten lost het maar gedeeltelijk op (zie punt 3). Los daarvan is een herlabelronde door `moeilijkheid-inschatter` naar de volle schaal van 5 nodig.

### 2. De JSON overschrijft de database bij elke start

`PlayerSeeder` en `QuestionSeeder` zetten `Difficulty` bij elke opstart opnieuw uit de JSON (tenzij `EditedInAdmin`). Een berekende moeilijkheid in datzelfde veld zou dus bij de volgende deploy verdwijnen. → De gemeten waarde moet **apart** bewaard worden; de JSON blijft de AI-inschatting.

### 3. De meeste vragen worden maar één keer gespeeld

Een vraag komt via de rotatie in één daily en dan lang niet meer terug (bv. 420 snelvuurvragen, 10 per snelvuurdag). Per item hebben we dus ongeveer zoveel pogingen als er spelers waren die dag, en de meeste items zijn nog nooit gespeeld. Gevolg:

- Het directe nut per item zit in **hergebruik**: specials (die op tags vissen en dus vaker dezelfde vragen nemen), online matchen, de Academie (`DifficultyOf` op de dagkaartjes) en de volgende rotatieronde.
- Het grootste nut zit in **kalibratie**: als we zien dat de AI snelvuur-"3" systematisch te moeilijk inschat of opstelling-"5" eigenlijk onhaalbaar is, verbetert dat de labels van alle nog niet gespeelde items (bijsturen van de agent, of een correctie per type).

### 4. De lokale testdata kan het niet valideren

`tools/VoetbalQuiz.TestData` laat testgebruikers juist antwoorden met een kans die uit het label zelf komt (`Know(p, difficulty)`). Een meting op die data vindt dus gewoon het label terug. Om de formule te testen moet de simulator een verborgen "echte" moeilijkheid krijgen (label ± ruis), zodat we kunnen nagaan of de herberekening die terugvindt.

### 5. Er bestaat al een eerste meting

`ResultsService.Hardest` toont in de statistieken de vijf spelers met het laagste percentage juist (enkel "Wie ben ik?", minstens 2 pogingen, enkel afgelopen dagen). Dat is dezelfde berekening in het klein; die kan straks de nieuwe meting gebruiken.

### 6. Eén speler, drie vraagtypes

`Player.Difficulty` geldt voor "Wie ben ik?", kluts én counter. Een naam ontwarren (kluts) of een speler herkennen uit één tip (counter) is iets anders dan vijf tips in de ik-vorm. De meting voor een speler moet daarom uit "Wie ben ik?" komen; kluts en counter tellen voorlopig niet mee.

## Voorstel

### Kennisscore per poging (0-1)

"Juist/fout" is te grof, en de punten mengen kennis met snelheid (bij snelvuur is 50 of 100 punten reactiesnelheid, geen moeilijkheid). Per vraagtype een kennisscore in `Shared`:

| Type | Kennisscore |
|---|---|
| Wie ben ik?, carrièrepad, wissel, rugnummer, mannetje, emoji | juist bij stap *k* van *n*: `1 − 0,5·(k−1)/(n−1)`; fout of tijd op: 0 (foute gokken tellen niet mee: dat is typgedrag) |
| Snelvuur, niet-bij | juist 1, fout 0, daarna **gokcorrectie** per item: `(p − 1/opties) / (1 − 1/opties)` |
| Jaar | `JaarPoints / 100` (1 jaar ernaast = 0,75, ...) |
| Opstelling | gevonden / open plaatsen |
| Counter, kluts | voorlopig niet |

Een poging die gestart is maar nooit beantwoord (tijd op, app gesloten) telt als 0: de speler heeft de vraag gezien.

### Welke pogingen tellen

- **Fase 1:** enkel `Attempt` (daily en specials), van afgelopen dagen.
- **Niet:** `TrainingAttempt` (oude daily opnieuw: het antwoord kan gekend zijn), admins en testaccounts.
- **Later:** `MatchAnswer`, zonder vragen waarop een derby-wapen gebruikt is (Trump, Bengaals vuur, Wissel, rode kaart, gokchinees).

### Mengformule: het label als vertrekpunt

Elke ster krijgt een verwachte kennisscore. Het AI-label telt als *k* "denkbeeldige pogingen" met die verwachte score; elke echte poging schuift het resultaat op.

```
verwacht(label) = 1: 0,90 · 2: 0,72 · 3: 0,50 · 4: 0,28 · 5: 0,10
score = (k · verwacht(label) + som kennisscores) / (k + n)        k = 10
```

- 0 pogingen → het label. 10 pogingen → half label, half meting. 40 pogingen → 80 % meting.
- Terug naar sterren met de grenzen halfweg (≥ 0,81 = 1 ster, ≥ 0,61 = 2, ≥ 0,39 = 3, ≥ 0,19 = 4, anders 5), met een **marge van 0,03** voor een label wijzigt, zodat een item op een grens niet heen en weer springt.
- Bewaard als kommagetal (bv. 3,6): `PickByPlan` kiest het dichtst bij het doel, dus een fijnere waarde helpt. Op het scherm afgerond op hele sterren.
- Formule, verwachtingen, *k* en grenzen staan in `Difficulty` (Shared) met unit tests.

Dit lost het 2/4-tekort op voor gespeelde items: een "3" die 70 % juist scoort wordt vanzelf een 2.

### Waar het resultaat terechtkomt

Nieuwe kolommen op `Player` en `QuizItem` (EF-migratie), waar de seeders niet aan komen:

- `MeasuredDifficulty` (double?), `MeasuredAttempts` (int), `MeasuredAt` (DateTime?).
- Een helper `EffectiveDifficulty` = gemeten waarde als die er is, anders het label. `QuizService` (selectie en `DifficultyOf`) gebruikt die.
- De JSON en `Difficulty` blijven de AI-inschatting: zo kunnen we AI en praktijk blijven vergelijken.

### Wanneer het draait

Bij het afsluiten van een dag (`ResultsService`, dat al per afgelopen dag rekent): de items van die dag worden herberekend. Altijd volledig opnieuw uit alle pogingen, dus geen opgestapelde afrondingsfouten. Plus een knop "Alles herberekenen" in het admin-paneel.

Een reeks die al aangemaakt is, verandert niet: de vragen van een `DailyChallenge` liggen vast. Enkel nieuwe reeksen en de getoonde gemiddelde moeilijkheid (Academie, specialkaartjes) verschuiven.

### Admin en agent

- **Admin:** een overzicht "Moeilijkheid in de praktijk": per item label → gemeten (sterren en kennisscore), aantal pogingen, en een filter "grote afwijking" (≥ 1,5 ster bij ≥ 15 pogingen).
- **Kalibratie per type:** per vraagtype en label de gemiddelde gemeten score. Toont of de AI per type systematisch te streng of te mild is.
- **Agent:** `moeilijkheid-inschatter` krijgt dat overzicht als input: bij grote afwijkingen kijkt hij opnieuw (misschien verraadt een tip te veel, of zijn de foute opties onzin), en de kalibratie stuurt zijn inschatting van nog niet gespeelde items bij.

### Gefaseerd

1. **Meten en tonen (M).** Kennisscore + formule in Shared met tests, kolommen + migratie, herberekening bij afsluiten van een dag, adminoverzicht. Nog **geen** invloed op de selectie: eerst zien of de cijfers kloppen. TestData krijgt een verborgen echte moeilijkheid om de formule te toetsen.
2. **Gebruiken (S).** `QuizService` kiest op `EffectiveDifficulty`; `Hardest` en `DifficultyOf` gebruiken de meting.
3. **Verfijnen (M).** Online matchen meetellen, correctie voor het niveau van wie antwoordde (zie open vragen), kalibratie in de agent-instructies.

Los hiervan, en eerder te doen: een herlabelronde naar de volle schaal (punt 1), want die helpt ook alle nooit gespeelde items.

## Open vragen (om af te stemmen)

1. **Fase 1 zonder invloed op de selectie:** akkoord om eerst enkel te meten en te tonen?
2. **Kennisscore bij tips:** mag een late juiste gok (bij tip 5) nog 0,5 waard zijn, of moet "pas bij de laatste tip" zwaarder doorwegen als moeilijk?
3. ***k* = 10 en de verwachte scores per ster:** goed vertrekpunt? Met hoeveel spelers per dag mogen we in productie rekenen? Bij 5 spelers per dag blijft het label nog lang domineren.
4. **Niveau van de spelers:** een daily wordt door een vaste, eerder sterke groep gespeeld, dus alles lijkt makkelijker dan voor "de Vlaamse fan". Is dat goed (de sterren beschrijven ons eigen publiek), of willen we corrigeren? Corrigeren kan later met een eenvoudig spelersniveau (een soort Elo/Rasch), maar maakt het minder uitlegbaar.
5. **De speler ziet sterren:** blijven die het AI-label tonen tot er genoeg pogingen zijn, of meteen de gemengde waarde?
6. **Kluts en counter:** akkoord dat ze voorlopig niet meetellen? Counter geeft wel per speler een signaal (gevonden/overgeslagen), dat kan later een aparte "herkenbaarheid" worden.
7. **Herlabelronde eerst?** Zal ik `moeilijkheid-inschatter` loslaten op bv. carriere, wissel, rugnummer en mannetje (geen enkele 2 of 4) voor we aan de meting beginnen?

## Notities

- 2026-10-20: analyse toegevoegd (labelverdeling, seeder overschrijft `Difficulty`, eenmalig gebruik per item, testdata is circulair) en een voorstel per open vraag.
