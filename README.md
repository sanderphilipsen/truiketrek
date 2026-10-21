# 👕 Truitje Trek – Voetbalquiz

Een mobiele quiz-app (Blazor WebAssembly PWA) om met vrienden te spelen. Elke dag is er een **daily challenge** van 10 voetballers die zichzelf in de ik-vorm beschrijven.

## Spelregels

- Een vraag start met 1 tip. Elke **5 seconden** komt er een tip bij, tot 5 tips. Je hebt **30 seconden** per vraag.
- Je kan meteen typen. Punten hangen af van het moment waarop je juist antwoordt: **100** (1 tip), **90**, **80**, **70**, **60** (5 tips), en de laatste 5 seconden nog **50**.
- Je mag zo vaak proberen als je wil, maar **elke foute poging kost 5 punten**. Niet geraden of gepast: 0.
- Voornaam + achternaam of enkel de achternaam is altijd juist ("Onana", "Lukaku", "Hazard" voor Eden én Thorgan).
- Tikfouten, hoofdletters en accenten maken niet uit ("aguero", "De Bruine" en "KDB" worden aanvaard). Ook wat **fonetisch juist** is telt: "kurtwa" (Courtois), "lukakoe", "kastanje" (Castagne), "mbape". Dat geldt voor elk vraagtype waar je een naam typt ([AnswerMatcher.cs](src/VoetbalQuiz.Server/Services/AnswerMatcher.cs)).
- Iedereen krijgt dezelfde 10 vragen per dag. Elke vraag kan je maar één keer spelen: herladen zet de klok niet terug. Je speelt de vragen in volgorde.
- Er is een klassement per dag en per maand.
- **Dagwinnaar:** na middernacht wordt de winnaar van de dag vastgelegd. Bij gelijke stand wint wie meer juist had, dan wie minder fout gokte, dan wie het eerst klaar was. De dagwinnaar krijgt **+50 punten** in het maandklassement (`DailyWinnerBonus`).
- **Statistieken:** je eigen cijfers, een eregalerij met records (meeste dagzeges, hoogste dagscore, eeuwige tweede, snelste vinger, scherpschutter, …), alle Gouden Ballen en de moeilijkste spelers.

- **Home:** start de daily, zie je voortgang, maak een online partijtje of doe mee met een open partijtje. Het daily-overzicht staat op `/daily`.
- **Menu onderaan:** Home, Klassement, Badges, Stats en Profiel. De daily en online spelen start je vanaf Home.
- **Profiel:** links je level, cijfers en badges; rechts de tabbladen Avatar, Level (XP-overzicht) en Account (wachtwoord, feedback, afmelden). Avatar kiezen: een bekende voetballer, of **zelf samenstellen**. Je kiest huidskleur, kapsel, haarkleur, gezichtsbeharing, shirtkleuren, patroon, rugnummer, bril en haarband. Je kan ook vertrekken van een voetballer en die aanpassen. Een zelfgemaakte avatar wordt als korte code opgeslagen (`c1:…`) met enkel waarden uit vaste paletten ([Avatars.cs](src/VoetbalQuiz.Shared/Avatars.cs)). De avatars zijn getekend (SVG), geen foto's.
- **Levels:** XP voor elke gespeelde vraag (+10), voor juiste antwoorden (punten / 5), voor een volledige dag (+50), voor 10 op 10 (+50) en per dagzege (+100). Elk level vraagt 100 XP meer dan het vorige en er is geen maximum. Titels gaan van Jeugdspeler tot Legende ([Levels.cs](src/VoetbalQuiz.Shared/Levels.cs)).
- **Beloningspopup:** na een volledige daily, special of online wedstrijd zie je met animaties hoeveel XP je bijkreeg (per onderdeel), hoe je levelbalk volloopt (met level-up en confetti) en welke badges je nieuw hebt. De server onthoudt wat je al gezien hebt (`SeenXpParts`, `SeenBadges`), dus XP of badges die later binnenkomen (bv. een dagzege na middernacht) verschijnen de volgende keer ([RewardPopup.razor](src/VoetbalQuiz.Client/Shared/RewardPopup.razor)).

- **Feedback:** via Profiel → "Bug of idee melden". Spelers zien hun eigen meldingen en de status ervan. Admins zien alles en kunnen een status en antwoord geven. Admins zijn gebruikersnamen in de configuratie onder `Admins` (lokaal: `sander` in `appsettings.Development.json`).

### Spelers controleren en toevoegen (admin)

- Alle 253 spelers zijn tip per tip gecontroleerd met bronnen (vooral Wikipedia, plus Transfermarkt en sportmedia). Elke speler heeft een status, bronnen, thema-tags en een controledatum.
- **Enkel goedgekeurde spelers komen in de quiz.** Goedkeuren kan pas met minstens één bron, en tips die de naam verklappen worden geweigerd.
- Via Profiel → ⚙️ Admin → **Spelers** kan je spelers zoeken, filteren op status of thema, bewerken, goedkeuren of afwijzen, en nieuwe spelers toevoegen. De editor heeft zoeklinks naar Wikipedia en Transfermarkt.
- Een speler die je in het admin-paneel aanpast, wordt niet meer overschreven door de JSON-bestanden.

### Vraagtypes en weekplanning

Er zijn 12 vraagtypes. Elke weekdag heeft in de daily een vast type **of een mix van types**, in te stellen via Admin → **Planning**:

| Type | Werking | Tijd | Punten |
|---|---|---|---|
| 🕵️ Wie ben ik? | 5 tips, typ de naam | 30 s | 100 → 50 |
| ⚡ Snelvuur | 4 opties, tik het juiste | 5 s | 50–100, sneller = meer |
| 🧭 Carrièrepad | clubs verschijnen één voor één, typ de speler | ±25 s | 100 → 50 |
| 🧩 Wie hoort er niet bij? | tik de vreemde eend | 6 s | 50–100 |
| 📅 Raad het jaar | kies het jaartal | 10 s | 100 / 75 / 50 / 25 |
| 😀 Emoji-raadsel | typ de speler of club | 15 s | 100 → 50 |
| 📋 Opstelling | vul 6 namen van een basiself aan | 60 s | tot 200 per opstelling (5 per dag) |
| 🔄 Wissel | een spraakmakende transfer, feit per feit (wanneer, bedrag, van, naar, tip) | 26 s | 100 → 50 |
| 🔢 Rugnummer | een shirtnummer, dan positie, land, club en een tip | 26 s | 100 → 50 |
| 👤 Mannetje | een getekende avatar: donker silhouet → wazig in kleur → scherp → 2 tips | 26 s | 100 → 50 |
| 🌀 Kluts in de zestien | de letters van een spelersnaam door elkaar; elke 4 s valt per woord een letter juist | 26 s | 100 → 50 |
| 🏃 Counter | zoveel mogelijk spelers in 60 s, met telkens hun duidelijkste tip; doorspelen mag, fout kost niets | 60 s | 20 per speler, tot 200 (5 per dag) |

- **Mix:** de vragen gaan om beurten over de gekozen types, zolang de dag onder 10 vragen en 1000 punten blijft (een opstelling en een counter tellen voor twee). Een mix van álle types geeft 9 vragen en laat de laatste types van de lijst vallen; kies dus liever een paar types. De verdeling staat in `QuestionTypes.Mix`.
- **Kluts en counter** gebruiken de spelers (net als "Wie ben ik?"), niet de vragenbank. Een speler komt nooit twee keer voor in dezelfde reeks. Wissel, rugnummer en mannetje staan in de vragenbank (`wissel.json`, `rugnummer.json`, `mannetje.json`), nog met status "te keuren".
- **Feedback in voetbaltaal:** een foute gok is "Voorlangs!", "Naast!", …; juist is "Binnen!" of "Zitten!", met de maximumscore "In de winkelhaak!", deelpunten "Tegen de lat!" en niets "Afgefloten." ([Commentary.cs](src/VoetbalQuiz.Shared/Commentary.cs)).
- **Type uitschakelen:** in Planning zet je een type aan of uit. Een type dat uit staat, komt niet meer in de daily of specials (de vragen blijven bewaard). Een dag met enkel dat type wordt een mix van de types die nog aan staan. Minstens één type blijft altijd aan. Een daily die al gestart is, verandert niet.

Elke dag is maximaal 1000 punten waard. De vragen voor de nieuwe types staan in de **vragenbank** (`Data/questions/*.json` en Admin → **Vragen**), met bronnen en status, net zoals spelers. De planning toont per type hoeveel weken voorraad er nog is. Zijn er te weinig vragen van een type, dan wordt het die dag "Wie ben ik?".

### Klassementen

Er zijn twee klassementen:

- **Vandaag:** enkel de daily van vandaag.
- **Maand:** daily, specials en online samen. Maandpunten = daily-scores + special-scores + online-punten + podiumbonussen.
  - Online: 100 per wedstrijd + 100 per tegenstander die je achter je liet (een gedeelde plaats telt half). Enkel je 20 beste wedstrijden van de maand tellen.
  - Podiumbonus: de top 3 van elke daily en special krijgt +100 / +50 / +25, zodra die dag of special afgelopen is.
  - De top 3 van de maand krijgt een beker. Alle waarden staan in [MonthRules.cs](src/VoetbalQuiz.Shared/MonthRules.cs).

### Gouden Bal

- Wie op het einde van de maand **bovenaan het maandklassement** staat, wint de **Gouden Bal**. Bij een gedeelde eerste plaats krijgen ze hem allebei.
- Elke Gouden Bal is voor altijd een **kleine ster op het shirt** van je avatar (drie per rij, max. 9 getekend).
- De winnaar draagt de **hele volgende maand een gouden rand** rond de avatar; daarna gaat die over naar de volgende winnaar.
- Op de **eerste dag van de maand** staat de nieuwe Gouden Bal groot op het dashboard ("Gouden Bal oktober"). Op Stats staat de lijst van alle Gouden Ballen.
- De server legt de winnaar één keer vast bij het eerste verzoek nadat de maand ook in UTC voorbij is (in België tussen 1 en 2 uur op de eerste). Loopt er nog een special die in die maand startte, dan wacht hij tot die afgelopen is; de aankondiging komt dan op de dag van toekenning. De winnaar wordt bewaard in de tabellen `GoldenBalls` en `GoldenBallMonths` ([GoldenBallService.cs](src/VoetbalQuiz.Server/Services/GoldenBallService.cs)), zodat sterren niet meer verschuiven. Badges: Gouden Bal, Zilveren Bal (tweede), Titelverdediger (twee maanden na elkaar), Gouden hattrick (3) en Messi-niveau (8).

### Specials

- Via Admin → **Specials** plan je een themadag. Er zijn sjablonen voor Champions League, Christmas, New Year, Halloween, Sinterklaas, Gouden Schoen en "eigen special". Je kiest de datum (van … tot …) en de vragen: alle spelers met een of meer thema's, of zelf een selectie.
- **Affiches:** De Topper (Anderlecht – Club Brugge), de Clasico (Standard – Anderlecht) en de Derby der Lage Landen (België – Nederland). Die combineren twee thema's en verschijnen op de homepagina als wedstrijdbanner in de clubkleuren. De spelers hebben daarvoor de tags `anderlecht`, `club-brugge`, `standard` en `oranje`.
- **Thema's:** Rode Duivels Special, Legendes Special (alle klassiekers, tag `legende`) en Transfer Deadline Day (1 september: wissel en carrièrepad). Elk sjabloon stelt zelf de passende vraagtypes voor.
- Op die dag(en) verschijnt de special bovenaan de homepagina. Elke special heeft een eigen klassement dat niet meetelt voor de daily. De winnaar krijgt een beker en de badge "Specialist".
- Zolang niemand de special speelde, kan je alles wijzigen of hem verwijderen.

### Badges, seizoenen en head-to-head

- **57 badges** (brons, zilver, goud): regelmaat (7 dagen op rij, volle week), snelheid, per vraagtype (Historicus, Scout, Bondscoach, Emoji-expert, …), thema's (Rode Duivels-fan, Champions League-kenner), online (Partijtjeskoning, Vol partijtje, Elo-meester), Gouden Bal (Zilveren Bal, Titelverdediger, Gouden hattrick, Messi-niveau), lange adem (Seizoenkaart, IJzeren man, GOAT) en karakter (Clean sheet, Doorzetter, Op de valreep). Bekijk ze via **🎖️ Badges** in het menu, met je voortgang per badge.
- **Seizoenen:** elke maand is een seizoen. De top 3 van het daily- en het online maandklassement krijgt een beker in de prijzenkast.
- **Spelersprofiel:** tik op een naam in een klassement. Je ziet hun badges en bekers, en **jij tegen hen**: onderlinge daily-dagen en online wedstrijden.

### Online mode

- Maak een partijtje (naam + 2 tot 8 plaatsen) en kies een **vraagtype**, of met 🎲 **Mix** meerdere types. Enkel de types die aan staan in Admin → Planning kan je kiezen. Anderen zien het partijtje (met het type) live in de lobby en kunnen meedoen. De organisator start.
- De vragen, tijden en punten zijn dezelfde als in de daily (`QuestionEngine`); een mix volgt dezelfde verdeling (`QuestionTypes.Mix`, bv. 5 vragen bij enkel opstellingen). Bovenop je punten komt een **positiebonus** voor wie het eerst juist is: 1e +50, 2e +30, 3e +15 ([MatchRules.cs](src/VoetbalQuiz.Shared/MatchRules.cs)). Deelpunten (een jaartal er net naast, een half ingevulde opstelling of counter) tellen ook, maar zonder bonus.
- Een vraag stopt zodra iedereen klaar is (juist, gepast of een keuze gemaakt). Daarna zie je 6 seconden het antwoord en de volgorde.
- Spelers en vragen uit de daily van vandaag komen niet voor, zodat online spelen de daily niet verklapt. Online kiest willekeurig en verstoort de rotatie van de daily niet.
- Wordt een type uitgeschakeld voor het partijtje start, dan valt het uit de mix; blijft er niets over, dan kan het partijtje niet starten.
- Live updates gaan via SignalR (`/hubs/match`). De klok en de punten zitten op de server; `MatchEngine` zet de wedstrijden elke 250 ms verder.
- Het online klassement staat los van de daily: een **Elo-rating** (iedereen start op 1000) en een maandklassement. In de stats staan online records: meeste zeges, hoogste rating, snelste schutter, onklopbaar, … Een wedstrijd levert ook XP op (+25, winst +75).

**Vragenrotatie:** voor een nieuwe dag kiest de app de spelers die het langst geleden gevraagd werden; nooit gebruikte spelers komen eerst. Met 197 spelers duurt het ongeveer 20 dagen voor een speler terugkomt. Hoe meer spelers in de database, hoe langer ze wegblijven.

De timing en puntentelling zitten op de server. De app krijgt alleen de tips die al vrijgegeven zijn, en het antwoord pas nadat je geantwoord hebt.

Alle regels staan op één plek: [GameRules.cs](src/VoetbalQuiz.Shared/GameRules.cs). Wil je bijvoorbeeld 3 seconden tussen de tips? Pas `HintIntervalSeconds` aan.

## Projectstructuur

| Map | Inhoud |
|---|---|
| `src/VoetbalQuiz.Client` | Blazor WebAssembly PWA (schermen, installeerbaar op je gsm) |
| `src/VoetbalQuiz.Server` | ASP.NET Core API + hosting van de app, Identity (accounts), EF Core |
| `src/VoetbalQuiz.Shared` | Spelregels en gedeelde datatypes |
| `src/VoetbalQuiz.Server/Data/players/*.json` | De spelersdatabase met de tips |
| `tests/VoetbalQuiz.Tests` | Unit tests (puntentelling, antwoordherkenning, quizverloop) |

## Lokaal draaien

Vereist: .NET 10 SDK en Podman.

1. Start de database: een PostgreSQL-container `voetbalquiz-db` op poort 5433. De data staat in het volume `voetbalquiz-pgdata` en blijft bewaard.

   ```bash
   pwsh scripts/start-db.ps1
   ```

2. Start de app:

   ```bash
   dotnet run --project src/VoetbalQuiz.Server --launch-profile http
   ```

Open daarna http://localhost:5174. Bij elke start voert de server de databasemigraties uit en laadt hij de spelers uit de JSON-bestanden. De connection string voor ontwikkeling staat in `appsettings.Development.json`.

Opnieuw beginnen met een lege database: `podman rm -f voetbalquiz-db` en `podman volume rm voetbalquiz-pgdata`, en daarna het script opnieuw.

Testen:

```bash
dotnet test
```

## Spelers toevoegen of tips verbeteren

Pas een JSON-bestand aan in `src/VoetbalQuiz.Server/Data/players/` (of voeg een nieuw bestand toe) en herstart de server. Spelers worden op naam bijgewerkt.

```json
{
  "name": "Kevin De Bruyne",
  "answers": ["De Bruyne", "KDB"],
  "country": "België",
  "competition": "Premier League",
  "era": "modern",
  "difficulty": 1,
  "hints": ["5 zinnen in de ik-vorm, van vaag naar duidelijk"]
}
```

- `answers`: extra aanvaarde antwoorden naast de volledige naam (achternaam, bijnaam).
- `era`: `modern` of `classic`. Er komen maximaal 2 klassiekers per dag in de challenge.
- `difficulty`: 1 = iedereen kent het, 2 = makkelijk, 3 = voetbalfan, 4 = moeilijk, 5 = echte kenner. Een challenge loopt van makkelijk naar moeilijk; het beginscherm toont het gemiddelde als 0–5 sterren.

Bij een nieuwe dag worden 10 spelers gekozen die nog het minst gevraagd zijn. De dag wisselt om middernacht Belgische tijd.

## Hosten op Azure

De app draait op https://truitjetrek.azurewebsites.net (abonnement "Azure subscription 1", resourcegroep `tbooking-rg`).

| Onderdeel | Resource | Kost |
|---|---|---|
| Web-app | `truitjetrek` op het bestaande Linux-plan `tbooking-plan` (B1) | geen extra kost |
| Database | PostgreSQL Flexible Server `truitjetrek-db` (Burstable B1ms, 32 GB, versie 17), database `voetbalquiz` | ±€15–16 per maand |

Een nieuwe versie publiceren:

```bash
pwsh scripts/deploy-azure.ps1
```

De omgeving opnieuw opzetten of de instellingen herstellen: `pwsh scripts/setup-azure.ps1`. Het script slaat over wat al bestaat.

- Bij het opstarten voert de app zelf de migraties uit en laadt ze de spelers en vragen.
- **Always On** staat aan. Anders slaapt de app in en lopen de online wedstrijden en de dagafsluiting niet. **WebSockets** staan aan voor SignalR, en de app werkt alleen via HTTPS (nodig voor de PWA).
- **Eén instantie, niet uitschalen.** De app gaat ervan uit dat er precies één serverinstantie draait: `MatchEngine` zet de wedstrijden verder in die ene instantie, de SignalR-groepen hebben geen backplane en een paar gegevens (avatars, levels) staan in een geheugencache. Gelijktijdige schrijvers binnen die instantie (de engine en verzoeken) vangt `Match.Version` op: wie botst, begint opnieuw vanaf een verse toestand. Met twee instanties kan een vraag twee keer afgerekend worden en missen spelers seintjes; dat vraagt eerst een backplane en een gedeelde cache.
- De connection string (`ConnectionStrings__Quiz`) en de admins (`Admins__0`, …) staan in de app-instellingen van de web-app, niet in git. Alleen Azure-diensten mogen met de database verbinden.
- Logs bekijken: `az webapp log tail -g tbooking-rg -n truitjetrek`.
