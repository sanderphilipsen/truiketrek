# UX-review vóór productie – Truitje Trek (22 oktober 2026)

Review door de ux-designer op commit `d5576b9`, op 375 px en 320 px, met de seed-gebruiker sofie (ervaren speler) en een nieuw account (nieuwe speler). Geen code aangepast. Contrast, lettergroottes en tikvlakken zijn gemeten op de berekende stijlen. Getest 's avonds (avondmodus van het veld); de dagmodus is niet bekeken. De regressie van dezelfde dag staat in [../regressie/2026-10-22-regressie-productie.md](../regressie/2026-10-22-regressie-productie.md).

## 1. Samenvatting

1. De app ziet er sinds de scorebordstijl veel volwassener en eigener uit. De kernlus werkt vlot en eerlijk: vraag, resultaat en "Volgende vraag" staan meteen in beeld, en juist en fout tonen een icoon én tekst.
2. Bijna alle grote punten uit de audit van 19/10 zijn opgelost: de Razor-code bij open partijtjes, "Volgende vraag" onder de vouw, de lange naam in de header, het partijtje terugvinden, het contrast in de beloningspopup, aria op klok en resultaat, een eigen bevestiging bij opheffen.
3. Er zijn nieuwe problemen bijgekomen door de stijl zelf. De doorschijnende tegels op het "levende veld" met stadionlichten verliezen 's avonds contrast (geschat 1,6 tot 2,7:1). De voetballer-avatars in het profiel hebben een lichte tegel met lichtgroene naam (1,8:1). Op Home is er geen echte hoofdknop meer.
4. Taal en patronen zijn nog niet overal gelijk: drie tabstijlen, invoervelden en tabs in Arial, "OK", "Overslaan", "Daily challenge", "positiebonus", "dagzeges", en match, partijtje, wedstrijd en tafel door elkaar.
5. **Oordeel:** klaar voor productie op voorwaarde dat de vier S-fixes uit "Vóór productie fixen" er nog in gaan, plus bij voorkeur "Zo werkt het" per account (V5). De rest kan net na de lancering.

## 2. Status van de vorige audit (G1–G8)

| # | Status | Wat ik zag |
|---|---|---|
| G1 Razor-code en vals Derby-label | **Opgelost** | `OpenTables.razor` gebruikt nu `MatchMeta`; "Mix · 5 vragen" zonder code. De knop heet "Doe mee". |
| G2 Online reveal | **Opgelost** | "Jij +X · Plaats N van M" en de aftelling staan bovenaan; de tips staan ingeklapt in een `<details>`. De online-tester bevestigde: tussenstand en aftelling in beeld zonder scrollen. |
| G3 "Volgende vraag" onder de vouw | **Opgelost** | Direct onder de resultaatkaart, volledig in beeld op 375 en 320 px, ook bij Carrièrepad met 5 tips. |
| G4 Lange naam breekt de header | **Opgelost** | De topbar is weg; een lange begroeting loopt over twee regels, geen horizontale scroll op 320 px. |
| G5 Partijtjes terugvinden | **Opgelost** | Banner "Je partijtje wacht 1/4 Open ›" bovenaan Home; het formulier zit achter "Nieuwe match" in een bottom sheet. |
| G6 Contrast van badges en groene getallen | **Grotendeels opgelost** | Badgebeschrijvingen 13,1 px en 7–8:1 op donker; "+100" in de popup 9,3:1. Nieuw: vergrendelde badges (wit 8 %) onder een lichtbundel zakken naar ±2:1 (V1). Badgebeschrijving in de popup is 10,9 px. |
| G7 Schermlezers | **Grotendeels opgelost** | `h1`, `role="timer"`, aria-live "Nog minder dan 10 seconden", resultaat in `role="status"`, labels op kleurstalen, `aria-pressed` op toggles, `role="alert"` op de loginfout. Rest: na een antwoord valt de focus op `<body>`; de status leest "Juist!. Plus 62 punten". |
| G8 Wachtwoord vergeten | **Deels opgelost** | Link "Wachtwoord vergeten?" met "Contacteer de beheerder", maar zonder contactweg en zonder oogje om het wachtwoord te tonen. |

**Kleine punten uit de vorige audit**
- K1 tikvlakken – deels: chips, sheet en Home ≥ 44 px. Nog te klein: tabs Klassement (37 px), tabs Profiel (40 px, met een tweede rij van 37 px), Bug/Idee in Feedback (40 px), −/+ in de avatar-builder (38 px breed), spelersnamen in het klassement (21 px hoog; de hele rij zou klikbaar moeten zijn).
- K2 "Ik pas" – deels: "Afgefloten" werd "Gepast."; passen krijgt nog dezelfde rode ✗ en rand als fout, de knop schuift bij Carrièrepad omlaag als er tips bijkomen, en er staat geen "(0 punten)" bij.
- K3 taal – deels: "Join" en "Stats" zijn weg; nog "OK" (`QuestionPlay.razor:118`), "Overslaan"/"Top!", "dagzeges", "positiebonus", paginatitel "Daily challenge".
- K4 lettertype – deels: `.btn` erft het lettertype, maar inputs, tabknoppen en de Bug/Idee-knoppen staan in Arial.
- K5 layout shifts – deels: Home heeft een skeleton; de loginfout duwt "Aanmelden" nog ±50 px omlaag.
- K7 bevestiging – opgelost (ConfirmSheet, geen `confirm()` meer). K10 voortgang op kleur – opgelost (0/10, ✓/✗), al zijn gepast en fout daar niet te onderscheiden. K11 Afmelden – opgelost. K14 dubbele naam en level – opgelost.
- K12 klok start meteen – open; nergens uitgelegd, ook niet in "Zo werkt het".
- K13 technische ruis – open: 404 op `/VoetbalQuiz.Client.styles.css`; nieuw: console-fout `<circle> attribute r: negative value (-0.02)` uit de veldanimatie.
- K6, K8, K9 – niet te toetsen of niet getoetst.

## 3. Nieuwe bevindingen

### Vóór productie fixen (snel en belangrijk)

**V1. Doorschijnende tegels verliezen contrast onder de stadionlichten (avondmodus)** — M
- Waar: `app.css`: `--surface-soft` (rgba wit .08), de special-kaart (`rgba(250,199,117,.1)`), `.tafel-new`, vergrendelde `.badge`, de "todo"-rijen in `/daily`, `.sb-*` met doorschijnende achtergrond. De bundels komen van `.pf-beam` (tot 45 % wit, kruisend in het midden van het scherm).
- Wat ik zag: op een donker stuk veld zit alles boven 7:1. Waar de bundels kruisen (achtergrond ±rgb(110,150,125)): "Nog niemand gespeeld. Pak jij goud?" 1,6:1; "Nieuw vandaag" 2,0:1; de specialnaam 2,7:1; "Nieuwe match" 2,7:1; vergrendelde badgetekst ±2:1; de nummers 1–10 in de lege daily-lijst 3,45:1 (zonder bundel). Het "Ik pas"-label werd even doorkruist door het balletje van de animatie.
- Waarom: principe 5 (4,5:1 voor tekst). Het contrast hangt af van het uur en van waar de tegel toevallig staat.
- Voorstel: elke tegel met tekst een **opake** `--card`-achtergrond (zoals de daily-kaart en het klassement al hebben); doorschijnend enkel voor decoratie zonder tekst; eventueel een variabele `--card-soft` (opaak, iets lichter dan `--card`) voor secundaire tegels; de bundels dimmen (bv. 0,25 i.p.v. 0,45) zolang er content over ligt.

**V2. Voetballer-avatars: naam onleesbaar (1,8:1)** — S
- Waar: Profiel › Avatar › Voetballer kiezen, `app.css` ±1600 (`.profile-main .avatar-option { background: #f3f7f4 }`) en ±581 (`small { color: var(--muted) }`).
- Wat ik zag: lichte tegels met lichtgroene namen van 11,2 px ("Kevin De Bruyne" 1,79:1). Het is ook de enige lichte tegel in een volledig donkere app.
- Voorstel: tegel donker (`--card` met rand `--sb-line`, gekozen = rand `--accent`), naam in `--text` en minstens 12 px, de twee losse hex-kleuren weg.

**V3. Home heeft geen duidelijke hoofdactie meer** — S/M
- Waar: `Pages/Home.razor` (DailyCard en `.tafel-new`).
- Wat ik zag: "Start de daily ›" is tekst van 14,7 px in een tegel. "+ Nieuwe match" daaronder is het enige element dat eruitziet als een knop en trekt het oog. Bij een nieuwe speler duwen "Zo werkt het" en een special op de startdag de daily naar ±650 px. Na de daily is er op Home geen volgende stap (tenzij er een special loopt).
- Waarom: principe 2 (één blikvanger) en het homeconcept van 19/10 ("één grote gele hoofdknop").
- Voorstel: de daily-tegel krijgt een volle gele balk onderaan "Start de daily ›" / "Verder met vraag 4 ›" van ≥ 48 px (stijl van "Bekijk je score"); "Nieuwe match" wordt een gewone tegel, links uitgelijnd, icoon plus "Speel tegen je maten"; na de daily zonder special: "Klaar · 256 punten" plus een secundaire link "Oefen in de academie ›" of "Daag een maat uit ›".
  ```
  ┌ DAILY · VANDAAG ───────────── 0/10 ┐
  │ Wie hoort er niet bij?             │
  │ [██████ Start de daily  › ███████] │ ← geel, 48px
  └────────────────────────────────────┘
  [🏆 3de van 6]   [🔥 1 dag op rij]
  [👥 Speel tegen je maten          ›]  ← gewone tegel
  ```

**V4. Passen ziet eruit als fout** — S
- Waar: `Shared/QuestionPlay.razor` en `.result.fail` (rode rand, rood ✗, "GEPAST.", schudanimatie); `/daily`-overzicht (✗ 0, net als fout); "Je paste." wordt nog eens herhaald onder de keuzes.
- Waarom: principe 4 ("fout mag nooit vernederend zijn"). Bewust passen is een keuze.
- Voorstel: neutrale variant `.result.pass` (grijze rand, icoon ⏭ of "—", geen schudanimatie, "Gepast · 0 punten"); in het overzicht een "–" in plaats van ✗; de knop "Ik pas (0 punten)" vast onder het invoerveld in plaats van onder de groeiende tiplijst.

**V5. "Zo werkt het" verschijnt niet voor een nieuwe speler op een gedeeld toestel** — S
- Waar: `Home.razor`, `IntroHiddenKey = "tt-intro-hidden"` in localStorage, zonder gebruiker erin.
- Wat ik zag: na de daily van sofie stond de vlag op "1"; het nieuwe account kreeg daardoor géén uitleg.
- Voorstel: sleutel per gebruiker (`tt-intro-hidden:{userId}`) of een vlag op de speler (serverkant).

**V6. "Zo werkt het" klopt niet voor elk vraagtype** — S
- Waar: `Home.razor`, het intro-blok: "Een foute gok kost 5 punten" en "Hoe minder tips je nodig hebt".
- Wat ik zag: de daily van vandaag was meerkeuze; een fout gaf "+0 punten" en er waren geen tips. Dat de klok meteen start, staat er niet in (K12).
- Voorstel: algemene uitleg ("Hoe sneller je juist bent, hoe meer punten. De klok start zodra je de vraag opent.") plus een extra regel volgens het type van vandaag.

### Kort na productie

**N1. Drie verschillende tabstijlen, in Arial en te laag** — S/M. Klassement `.tabs` 37 px; Profiel `.profile-tabs` 40 px met daarin `.tabs.light` 37 px; Statistieken twee losse omlijnde knoppen 47 px; Feedback `.tabs.light` 40 px; de academie-kalender gebruikt nog andere pijlen. Voorstel: één segmented control `.tabs` met `min-height: var(--tap)` en `font: inherit`; de geneste tabs in Profiel vervangen door een schakelaar boven de grid of twee kaarten; `role="tablist"`/`tab` met `aria-selected`.

**N2. Invoervelden in Arial** — S. `input` en `textarea` (login, registratie, antwoordveld, wachtwoord wijzigen, feedbacktitel). Voorstel: `input, textarea, select, button { font: inherit; }` in de basisregels.

**N3. Klassement: rijen niet klikbaar, legende onvolledig, degradatiezone bij nieuwe spelers** — S. Alleen de naam is een link (21 px); "•" (nieuw) staat niet in de legende; in de degradatiezone staan twee keer ▼ met verschillende betekenis; spelers met 0 punten die net begonnen zijn, staan meteen in de rode zone. Voorstel: hele rij als link; "• nieuw" in de legende; zone met een rode rand of label in plaats van een tweede ▼; spelers met minder dan X gespeelde dagen uitsluiten van de zone.

**N4. Spelersprofiel en Badges zijn erg lang** — M. `Speler.razor` (4688 px) en `Badges.razor` (4303 px): alle 57 badges onder elkaar, "13/57" drie keer, geen actie op andermans profiel, "1 wedstrijden" (`Speler.razor:47`). Voorstel: verdiende badges plus de 3 die het dichtst bij zijn ("Bijna: 1162/1200"), de rest achter "Toon alle 57"; één teller; meervoud corrigeren.

**N5. Academie: weinig onderscheid en jargon** — S. Special-rijen doorschijnend en licht, gewone dagen donker (V1); een emojirij "🎲 Mix 🔄 🔢 👤 🌀 🏃" zonder legende; een leeg cirkeltje dat op een aanvinkvakje lijkt; "Echt: 680 (4de van 9)"; de detailpagina herhaalt de kop. Voorstel: "Toen: 680 · 4de van 9"; "Mix · 6 types"; cirkeltje weg; vanuit de lijst meteen spelen.

**N6. Vraagscherm: antwoorden buiten de duimzone** — M. Bij meerkeuze staan de vier keuzes tussen y=310 en 470 op 812 px; de onderste helft is leeg. Voorstel: vraag bovenaan, keuzes plus "Ik pas" onderaan (flex met `margin-top: auto`, safe-area). Bij een invoerveld blijft het veld boven het toetsenbord, dat is goed.

**N7. Registratie: regels pas achteraf via een browserbubbel** — S. Te kort wachtwoord geeft de native Chrome-melding (`minlength=6`); geen hint bij de velden; paginatitel blijft "Aanmelden"; de loginfout duwt de knop omlaag. Voorstel: hulptekst onder de velden ("Minstens 6 tekens", "Max. 20 tekens, zo zien anderen je"); eigen foutmelding; ruimte vooraf reserveren; oogje bij het wachtwoord; concrete contactweg bij "Wachtwoord vergeten?".

**N8. Taal en woordkeuze** — S. "OK" → "Antwoord" of "Schiet"; "Overslaan"/"Top!" → één knop "Verder"; `<PageTitle>Daily challenge` → "Daily"; "dagzeges" → "dagen gewonnen"; "positiebonus" → "snelheidsbonus" of een info-icoon; "Middagpauze 13/10 · Uitslag" leest als een score, schrijf "13 okt"; **één woord** voor online spelen: nu "Nieuwe match", "Zet de match klaar", "Je partijtje wacht", "Partijtje opheffen", "Je wedstrijd is bezig" en "Vol partijtje" door elkaar. Voorstel: "partijtje" voor de lobby, "match" niet gebruiken.

**N9. Focus en schermlezer in de kernlus** — S. Na een antwoord gaat de focus naar `<body>`: zet ze op "Volgende vraag" (of op de resultaatkop met `tabindex=-1`); de dubbele punt in "Juist!. Plus 62 punten" weg; controleer of `DifficultyStars` een label heeft ("Moeilijkheid 3 van 5").

**N10. Laadscherm zonder merk** — S. `wwwroot/index.html`: bij een koude start alleen een ring met "0%" op het veld. Voorstel: logo en "Truitje Trek" boven de ring (het tegelicoon in scorebordstijl bestaat al).

### Later / groter

- **L1. Navigatie zonder tabbalk** — M/L. Alles loopt via Home. Voor korte sessies prima; meet na de lancering hoe vaak spelers heen en weer gaan. Indien vaak: een compacte tabbalk met 3 items (Spelen, Klassement, Ik).
- **L2. Daag-uit vanuit een spelersprofiel en het klassement** — L. "Jij tegen jens" is doodlopend; een knop "Daag jens uit" past bij backlog 013 en 016 en vraagt server-werk.
- **L3. Dag- en avondmodus testen op contrast** — M. Het veld wisselt na zonsondergang; zet een automatische contrastcheck (bv. axe) op beide modi.
- **L4. Opbouw van de beloning** — M, met animatie-bouwer. De badge "Aftrap" had een beschrijving van 10,9 px en de knop wisselt van tekst tijdens het afspelen. Eén vaste knop "Verder" en 12 px volstaan.
- **L5. Klok-cliff zichtbaar maken** — M, voor animatie-bouwer. De aria-live voor de laatste 10 s is er; visueel kan er een subtiele puls bij.

## 4. Consistentie van het design system

- **Afwijkingen van de scorebordstijl:** de lichte avatar-tegels (V2); doorschijnende special-kaarten en vergrendelde badges naast opake tegels elders (V1). De beloningspopup is opaak, goed zo.
- **Iconen:** Home, PageHeader, CreateTableForm en RecentMatches gebruiken de lijniconen van `Icon`; Speler, Profiel, Badges, Statistieken, Academie en Feedback gebruiken emoji als koppictogram (⚔️ 🏆 🎖️ 🎨 ⭐ 🔒 🐞 🚪). Kies per laag: lijniconen voor navigatie en koppen, emoji enkel als inhoud (badges, vraagtypes).
- **Losse hex-kleuren:** buiten `:root`/`.surface-light` staan nog ±190 hex-waarden in `app.css`: 72× `#fff` (vooral `.tabs button`, `.segmented` en knoppen; moet `var(--text)` zijn), 11× `#1b1600` en 8× `#f6f8f7` (lichte restjes), `#f3f7f4`/`#fff8d6` bij de avatar-tegels, een reeks rode tinten (`#b3121c`, `#ff3b3b`, `#ff4d57`, `#ff3b45`, `#ff5a5f`, `#7a0a11`); de confetti-kleuren staan hard in `RewardPopup.razor`. Voorstel: één opruimronde met `--fail` en `--derby-*` voor rood en `--text` voor wit.
- **Typografie:** inputs, tabs en de Bug/Idee-knoppen in Arial (N1, N2). Stat-labels zijn 11,2 px en scorebordlabels 11,5 px in hoofdletters; leg één minimum (12 px) vast in een variabele (bv. `--fs-label`).
- **Knoppen:** "Partijtje opheffen" is een omlijnde knop met onderstreepte tekst (mengvorm van link en knop); "Kopieer link" in de kleedkamer heeft dezelfde gele rand als een hoofdknop; de gele rand is ook de stijl van "Aanmelden", "Volgende vraag" en "Versturen". Hou geel voor één hoofdactie per scherm.
- **Lege en laadtoestanden:** goed op Home (skeleton), Daily en Klassement (Spinner), "Nog geen bekers…", "Je speelde deze daily nog nooit", "Nog niemand gespeeld. Pak jij goud?". Niet gezien: een lege "Vandaag" in het klassement en een lege lijst "Jouw meldingen".
- **Taal:** zie N8. Het aanspreken is consequent "je".
- **Huishouding:** de agentbeschrijving van ux-designer zegt dat de app "enkel een lichte modus" heeft, maar `:root` is nu `color-scheme: dark` met `.surface-light` als uitzondering. Werk de instructies van de designer- en reviewer-agents bij.

## 5. Top 5 die ik eerst zou aanpakken

1. **V2 + N2 (S):** de avatar-tegels donker met leesbare naam, en `font: inherit` op inputs en tabs. Twee kleine CSS-fixes met grote zichtbare winst.
2. **V1 (M):** opake tegels voor alles met tekst, plus zachtere bundels. Het grootste toegankelijkheidsrisico van de nieuwe stijl.
3. **V3 (S/M):** één gele hoofdknop in de daily-tegel op Home, "Nieuwe match" ondergeschikt, en een volgende stap na de daily.
4. **V4 + V6 (S):** passen neutraal tonen, "Ik pas (0 punten)" op een vaste plaats, en een eerlijke "Zo werkt het" (inclusief "de klok start meteen").
5. **V5 + N8 (S):** de introvlag per account en één taalronde ("Antwoord", "Verder", "Daily", één woord voor partijtje).

**Meest geraakte bestanden:** `src/VoetbalQuiz.Client/wwwroot/css/app.css`, `Pages/Home.razor`, `Shared/QuestionPlay.razor`, `Pages/Klassement.razor`, `Pages/Profiel.razor`, `Pages/Login.razor`, `Shared/RewardPopup.razor`, `src/VoetbalQuiz.Shared/Commentary.cs` (de tekst "Gepast.").

**Niet getoetst:** een lege klassementstoestand, de dagmodus van het veld, een echt schermtoetsenbord.
