# UX-audit kernflows – Truitje Trek (19 oktober 2026)

Screenshots staan in dezelfde map als `2026-10-19-kernflows-<nr>.jpg` (de browsertool levert jpg, geen png).

## 1. Samenvatting

Truitje Trek voelt op de gsm vlot en speels aan. De daily is in twee tikken gestart, de feedback bij een goed of fout antwoord ("GOAL!", "Naast! 'Messi' is fout (−5)") is duidelijk en een herlaad midden in een vraag verliest geen tijd of punten. De taal is consequent Vlaams en vriendelijk, en de beloning na de daily (XP, level-up, badge) geeft een echt "beloond"-gevoel.

De drie belangrijkste problemen:
1. In de lijst met open tafels staat letterlijke Razor-code (`@if (m.Derby) { 🔥 Derby }`) en elke tafel krijgt ten onrechte een "Derby"-label.
2. Het online-tussenscherm (reveal) duurt 6 s, terwijl de tussenstand en de aftelling onder de vouw staan. Ook "Volgende vraag" staat in de daily na elke vraag onder de vouw.
3. Een gebruikersnaam van 20 tekens (toegelaten) breekt de header: het logo, "Lv 1" en de begroeting breken af en op 320 px wordt de avatar afgesneden.

Belangrijkste sterkte: de kernlus (vraag, tips die bijkomen, punten die dalen, duidelijk resultaat) is helder, eerlijk en server-autoriteit (de klok loopt na een reload gewoon verder).

## 2. Scope & methode

- Flows: login/registratie en foutmelding, Home, daily spelen (fout antwoord, juist antwoord, typfout-tolerantie, passen, tijd op, herladen midden in de vraag, lege invoer), beloning/badge, daily-overzicht, klassement (Vandaag/Maand), profiel met avatar (kiezen en zelf maken) en account-tab, badges, statistieken, online tafel aanmaken, joinen, starten, spelen en verlaten (met twee testaccounts).
- Formaten: mobile 375 px (hoofdformaat), 320 px (Home, vraag), tablet 768 px (Home). Venster is achteraf teruggezet.
- Omgeving: poort 5174 was bezet door een al draaiende server (proces uit de ochtend, bouwt de DLL's vast). Daarom gestart met de configuratie `voetbalquiz-release` (poort 5176, database `voetbalquiz_preview`, Release-build van de huidige werkmap, dus mét de niet-gecommitte wijzigingen en de nieuwe `Spinner`). Testaccounts: een bestaand preview-account (`previewtafel`) en twee nieuwe lokale testaccounts (`bestaatnietuxtester2`, `uxtester3`). Geen wachtwoorden in dit verslag.
- Datum: 2026-10-19. Metingen (tikdoelen, lettergroottes, contrast) zijn gedaan met `javascript_tool` op de berekende stijlen.
- Bij de meting werd de browserklik soms in het verkeerde coördinatenstelsel geïnterpreteerd (HiDPI-screenshots); dat is een toolkwestie en geen appprobleem. Eén keer ("Start de wedstrijd") leek een echte klik niets te doen terwijl dezelfde knop via script wel werkte. Dat kon ik niet reproduceren en neem ik niet als bevinding op.

## 3. Scorekaart

| Criterium | Score | Uitleg |
|---|---|---|
| Duidelijkheid & feedback | 4 | Resultaat, tips en foutmeldingen zijn helder; het reveal-scherm online en de uitleg van punten/XP schieten tekort. |
| Efficiëntie | 3 | Daily starten is snel, maar "Volgende vraag" onder de vouw en lobbylijsten onder een lang formulier kosten scrollen. |
| Duimvriendelijk | 3 | Hoofdknoppen groot; veel secundaire doelen 20-42 px en chips net onder 44 px. |
| Leesbaarheid | 3 | Hoofdtekst goed (contrast 6-11:1); vergrendelde badges (~2:1, 11 px) en groene +XP (3,1-3,5:1) falen. |
| Consistentie | 4 | Kaarten, chips en kleuren hangen mooi samen; inputs/knoppen in Arial, enkele Engelse labels. |
| Taal | 4 | Kort, Vlaams en warm; uitzonderingen: "Join", "Stats", "OK", "Overslaan", "dagzeges", "positiebonus". |
| Spelbeleving | 4 | Eerlijke tijdsdruk, goede commentaar ("GOAL!"), beloningsmodal; punten/XP-onderscheid is onduidelijk. |
| Toegankelijkheid | 2 | Geen `aria-live`/`role=timer` bij score en klok, tabs zonder rol, kleurloze kleurkeuzes, geen koppen op vraagschermen. Spinner en dialoog zijn wel goed. |
| Robuustheid | 3 | Reload en lege invoer zijn netjes; lange namen breken de header, achtergebleven tafels blijven in de lijst. |

## 4. Bevindingen

### 🟠 Groot

#### G1. Open tafels tonen Razor-code en elke tafel een "Derby"-label
- Waar: Home en `/online`, `Shared/OpenTables.razor` regel 29 en 52.
- Wat ik zag: onder "Open tafels" en "Jouw tafels" staat bij elke tafel "10 vragen@if (m.Derby) { 🔥 Derby }", mét een rood Derby-label, ook bij tafels die geen derby zijn.
- Waarom: de speler ziet code, en een vals "Derby"-label stuurt de keuze om te joinen. Heuristiek: consistentie en systeemstatus. Oorzaak waarschijnlijk dat Razor `vragen@if` als e-mailadres leest.
- Aanbeveling: een spatie of `@(" ")` tussen "vragen" en `@if`, of de badge in een eigen `<span>` buiten de tekst zetten.
- Screenshot: `2026-10-19-kernflows-13.jpg`.

#### G2. Online reveal: 6 s, en tussenstand plus aftelling onder de vouw
- Waar: `Pages/Wedstrijd.razor` (fase Reveal, `MatchRules.RevealSeconds = 6`).
- Wat ik zag: na een ronde verschijnt "Het was… Ronaldo Nazário", daarna vijf tipkaarten en pas daaronder "Tussenstand" en "Volgende vraag over 6 s". Op 375 px zie je enkel de naam en de tips; wie scoorde en de stand zie je niet zonder scrollen, terwijl het scherm na 6 s verdwijnt.
- Waarom: de belangrijkste informatie (jouw punten en positie) ligt buiten beeld terwijl er een harde klok loopt. Heuristiek: zichtbaarheid van systeemstatus, efficiëntie.
- Aanbeveling: toon bovenaan een compacte regel "Jij +80 · plaats 1" en de tussenstand vóór de lange tiplijst, of plaats de tips ingeklapt. Overweeg de reveal wat langer als je onderaan scrolt.
- Screenshot: `2026-10-19-kernflows-17.jpg`.

#### G3. "Volgende vraag" staat in de daily na elke vraag onder de vouw
- Waar: `Pages/Question.razor` (resultaatweergave), 10 keer per daily.
- Wat ik zag: de resultaatkaart plus alle 5 tipkaarten vullen meer dan het scherm (op 375×812 is de gele knop enkel een randje boven de navigatiebalk). De speler moet elke vraag scrollen om verder te gaan.
- Waarom: de primaire actie ligt buiten beeld en buiten duimbereik. Heuristiek: efficiëntie, duimvriendelijk.
- Aanbeveling: maak de knop "Volgende vraag ›" `position: sticky` boven de bottom-nav (met safe-area), of zet hem direct onder de resultaatkaart en de tips daarna.
- Screenshots: `2026-10-19-kernflows-03.jpg`, `2026-10-19-kernflows-04.jpg`.

#### G4. Lange gebruikersnaam breekt header en begroeting
- Waar: gedeelde header (`Layout`/`app.css`), `Pages/Home.razor`; naamlimiet `maxlength="20"` in `Login.razor`.
- Wat ik zag: met "bestaatnietuxtester2" (20 tekens) breekt "Truitje Trek" in twee regels, "Lv 1" breekt in "Lv" en "1" op twee regels, de begroeting "Hoi …" breekt de emoji naar een eigen regel. Op 320 px staat de naam tegen het logo aan en wordt de avatar rechts afgesneden.
- Waarom: de header is het eerste wat elke speler ziet; 20 tekens is toegelaten. Heuristiek: robuustheid.
- Aanbeveling: naam in de header met `text-overflow: ellipsis` en `max-width`, `white-space: nowrap` op logo-tekst en levelbadge, of de naam in de header weglaten (hij staat al in de begroeting); begroeting `overflow-wrap: anywhere`.
- Screenshots: `2026-10-19-kernflows-11.jpg`, `2026-10-19-kernflows-15.jpg`, `2026-10-19-kernflows-18.jpg` (320 px).

#### G5. Tafels joinen of terugvinden zit onder een formulier van 1500+ px
- Waar: Home en `/online` (`Shared/CreateTableForm.razor`, `Shared/OpenTables.razor`).
- Wat ik zag: "Jouw tafels" en "Open tafels" staan pas onder het volledige aanmaakformulier (naam, plaatsen, 13 vraagtypes, aantal vragen, Derby). Wie een tafel had en even weg ging (ik logde uit en in als organisator) vindt zijn tafel enkel door te scrollen; Home toont bovenaan niets als "Je zit aan een tafel (2/5)". Joinen is voor de meeste spelers vaker nodig dan aanmaken. Lobbypagina en Home tonen bovendien hetzelfde lange formulier.
- Waarom: kernactie "meedoen" is verstopt. Heuristiek: efficiëntie, herkennen in plaats van onthouden.
- Aanbeveling: zet "Jouw tafels" en "Open tafels" boven het formulier, of klap het formulier standaard in achter een knop "Nieuwe tafel maken"; toon op Home een banner "Je tafel wacht (2/5) ›" zolang je aan een tafel zit.
- Screenshots: `2026-10-19-kernflows-02.jpg`, `2026-10-19-kernflows-13.jpg`.

#### G6. Vergrendelde badges en groene bonusgetallen halen geen contrast
- Waar: `Pages/Badges.razor`/`app.css` (badge-kaart), beloningsmodal.
- Wat ik mat: beschrijving van een vergrendelde badge ("Speel 100 dagen de daily") is 10,9 px, kleur rgb(93,111,101) op een doorschijnend vlak met kaartopacity 0,6: geschat ongeveer 2:1 (AA vraagt 4,5:1). Groene "+100 / +32 / +50" in de beloning: 3,5:1 op 14 px. "+182 XP" is 3,1:1 (enkel acceptabel omdat het 48 px is, maar de kleine "XP" ernaast is 17,6 px en faalt). Groene "Klaar voor vandaag!" (3,5:1, 17,6 px) faalt ook.
- Waarom: net de tekst die uitlegt hoe je een badge verdient is het slechtst leesbaar. Heuristiek: leesbaarheid, WCAG AA.
- Aanbeveling: vergrendeld = grijs icoon maar volle tekstkleur (geen opacity op de hele kaart), beschrijving minstens 12-13 px; gebruik een donkerder groen (bv. `#157a43`, ≥4,5:1) voor tekst en bewaar `--ok` voor vlakken.
- Screenshots: `2026-10-19-kernflows-08.jpg`, `2026-10-19-kernflows-06.jpg`.

#### G7. Score, klok en feedback zijn niet toegankelijk voor schermlezers
- Waar: `Pages/Question.razor`, `Pages/Klassement.razor`, `Pages/Profiel.razor`, `Pages/Statistieken.razor`.
- Wat ik zag (accessibility tree): "25s" en punten zijn losse `generic`-elementen zonder `role="timer"` of `aria-live`; de melding "Naast! … is fout (−5)" en het resultaat ("GOAL!") worden niet aangekondigd; de vraagschermen hebben geen kop (h1); de tabbalken (Vandaag/Maand, Avatar/Level/Account, Daily/Online) zijn knoppen zonder `role="tab"`/`aria-selected` of `aria-pressed`; kleurkeuzes in de avatar-editor zijn kleurcirkels zonder naam; de sterren "★★☆☆☆" op Home hebben geen uitleg; foutmelding op de login heeft geen `role="alert"`. Positief: `Spinner` heeft `role="status"` met "Laden…", de beloningsdialoog heeft `role="dialog"` met label, en Derby-meldingen gebruiken `aria-live`/`role="alert"`.
- Waarom: een speler met schermlezer weet niet of zijn antwoord juist was. Heuristiek: toegankelijkheid.
- Aanbeveling: resultaat en foutmelding in een `role="status"` (`aria-live="polite"`), klok niet voortdurend aankondigen maar bij 10 s en 3 s; `aria-label` op swatches ("Huidskleur 2"); `aria-pressed`/`aria-selected` op de toggles; een visuele of verborgen `<h1>` per scherm.

#### G8. Geen zichtbare weg bij een vergeten wachtwoord
- Waar: `Pages/Login.razor`.
- Wat ik zag: login heeft enkel gebruikersnaam/e-mail, wachtwoord, "Aanmelden" en "Registreer je". Er is geen "Wachtwoord vergeten?", geen tonen/verbergen van het wachtwoord. (Ik kon enkel de UI toetsen; als er serverzijdig een reset bestaat, is die niet vindbaar.)
- Waarom: een speler die zijn wachtwoord kwijt is, raakt zijn streak en rating kwijt en haakt af. Heuristiek: foutherstel en controle voor de gebruiker.
- Aanbeveling: link "Wachtwoord vergeten?" onder het wachtwoordveld en een oogje om het wachtwoord te tonen.
- Screenshot: `2026-10-19-kernflows-12.jpg` (de foutmelding zelf is wel helder: "Onbekende gebruiker of verkeerd wachtwoord.").

### 🟡 Klein

- **K1. Tikdoelen onder 44 px.** Gemeten op 375 px: seats- en aantal-vraagchips 42×42; vraagtype-chips 149×42; "Tafel aanmaken" 42 hoog; "Ik pas" 53×23; "‹ Overzicht" 76×21; "‹ Lobby" 53×21; "Tafel opheffen/verlaten" 112×23; "Bekijk het overzicht van vandaag ›" en "Lobby & rating ›" 20 px hoog; maandpijltjes 33×34; tabs (Vandaag/Maand 40, Avatar/Level/Account 40, Voetballer kiezen/Zelf maken 37); kleurstalen in de avatar-editor 34×34; header-links 34 hoog. Aanbeveling: `min-height: 44px` en wat extra padding (`::after`-trick) op linkish knoppen en chips.
- **K2. "Ik pas" forfeit zonder bevestiging en zonder uitleg.** Eén tik geeft 0 punten en een rood kruis met "AFGEFLOTEN."; de knop staat onder een lijst die verschuift als er tips bijkomen. Aanbeveling: knop vaste plaats (onder de invoer) en kort "Ik pas (0 punten)"; "Afgefloten" is voor spelers die gewoon passen een beetje straffend.
- **K3. Taalinconsistenties.** "Join" (Engels) naast "Nieuwe tafel"; tabbalk zegt "Stats" terwijl de pagina "Statistieken" heet; knoppen met enkel "OK" (liever "Antwoord"); beloningsmodal sluit met "Overslaan" bij een beloning, terwijl het einde "Top!" zegt (zelfde knop wisselt van tekst); jargon: "dagzeges", "online rating 1000" zonder uitleg, "punten + positiebonus", "Tafel opheffen".
- **K4. Inputs en knoppen gebruiken Arial, de rest system-ui.** Tekst in invoervelden en op knoppen als "Aanmelden" en "Start de wedstrijd" (`font-family` niet geërfd) staat in een ander lettertype dan labels. Aanbeveling: `input, button, select { font: inherit; }`.
- **K5. Layout shifts bij laden en foutmeldingen.** De daily-kaart op Home start als spinner van ~125 px en groeit naar ~260 px, waardoor alles eronder naar beneden springt; de loginfout schuift de "Aanmelden"-knop 50 px omlaag; "Je avatar is opgeslagen." verschuift de avatar-grid. Aanbeveling: min-height op de dagelijkse kaart, foutruimte reserveren.
- **K6. Verlopen of verlaten tafels blijven in "Open tafels".** De tafel van een organisator die was uitgelogd bleef "1/4 spelers" tonen. Spelers joinen dan een tafel die nooit start. Aanbeveling: tafel automatisch sluiten na x minuten zonder organisator of toon "organisator offline".
- **K7. Bevestiging bij vertrek uit een lopende wedstrijd is een native `confirm()`.** (`Wedstrijd.razor` regel 461.) Goed dat er een waarschuwing is ("je verliest al je punten"), maar het systeemdialoogvenster is lelijk in een PWA en kon ik in de testbrowser niet zien. Een eigen dialoog met de knoppen "Blijven spelen" en "Toch verlaten" is duidelijker. "Tafel opheffen" in de lobby heeft dan weer helemaal geen bevestiging.
- **K8. Online na het eerste juiste antwoord springt de klok naar 5 s zonder uitleg** (screenshot 16). Aanbeveling: label "Nog 5 s voor de anderen".
- **K9. Stale melding in het profiel.** "Je avatar is opgeslagen." blijft staan na wisselen van sub-tab naar "Zelf maken" (screenshot 07 toont de editor met de oude melding erboven). Wis de melding bij tabwissel.
- **K10. Daily-voortgangsbolletjes en maandklassement leunen op kleur.** De tien bolletjes op Home (en de cijfers in het overzicht) zijn rood of groen; een gepasste vraag is rood. Rood/groen alleen is slecht voor kleurenblinden (bij het overzicht staan er wel cijfers). Voeg ✓/✗ of cijfer toe in het bolletje.
- **K11. Account-tab: "Afmelden" staat onder het wachtwoordformulier.** Drie tikken en scrollen; geen bevestiging. Zet "Afmelden" bovenaan of in een eigen sectie.
- **K12. Klok loopt zodra je de pagina opent.** "Start de daily" opent meteen vraag 1 en de klok loopt (ook na wegnavigeren en herladen, wat eerlijk is). Dit staat nergens uitgelegd. Aanbeveling: onder de startknop "De klok start meteen" of een korte "Klaar?"-stap.
- **K13. Technische ruis.** Elke paginalading geeft een 404 op `/VoetbalQuiz.Client.styles.css` in de console; Google Fonts (Noto Color Emoji) wordt extern geladen. Geen invloed op spelen, wel op offline/PWA.
- **K14. Winnaarsbanner op Home toont "testhost was dagwinnaar" met een kleine avatar; "Level 1"-badge en naam staan dubbel in header en begroeting.** Lichte herhaling; kan korter.

### 🔵 Suggesties

- **S1. Tafel delen.** In de lobby geen manier om vrienden uit te nodigen (WhatsApp-link of code). Deel-knop met `navigator.share` toevoegen (screenshot 10, 14).
- **S2. Uitleg van punten.** Bij "GOAL! +80 punten" kort tonen waarom: "Na 6 s en 2 tips". Verduidelijkt ook het onderscheid punten (dagscore) versus XP (level).
- **S3. Lege klassement.** "Vandaag" met enkel jezelf kan een uitnodiging tonen ("Nog niemand anders vandaag. Daag een vriend uit"). Medailles 🥇🥈🥉 zijn in de rij klein (≈14 px); groter maken geeft meer feestgevoel (screenshot 05).
- **S4. Onboarding.** Een nieuwe account landt direct op Home zonder uitleg over punten die dalen, tips en de daglimiet van 1000 punten. Eén dismissable kaart "Zo werkt het" volstaat.
- **S5. Resultaatcommentaar.** Het commentaar ("GOAL!", "BINNEN!") is leuk; extra variatie bij een reeks juiste antwoorden of een "streak van 3" houdt het vers.
- **S6. Tijdsdruk zichtbaar maken.** Het getal springt van bv. 45 punten naar 0 als de tijd op is; een subtiele "laatste seconden"-puls op het puntengetal maakt de cliff voorspelbaar. Respecteer `prefers-reduced-motion` (nu al goed gedaan).

## 5. Wat goed werkt

- Eerste indruk van Home: begroeting, dagwinnaar, daily-kaart met één grote gele knop; bij een halve daily staat er "Verder met vraag 2" (herstelpunt, screenshot tablet-test).
- Vraagscherm: groot, helder, puntenteller en tijdsbalk zijn duidelijk; invoer is 17,6 px en krijgt automatisch focus; de laatste tip staat bovenaan met een gele rand.
- Feedback bij een fout antwoord is concreet ("Naast! 'Messi' is fout (−5)") en bij juist is er een echt doelpunt-moment; typfouten in namen worden ruimhartig aanvaard ("onuachu" werd goedgekeurd).
- Server-autoriteit: na herladen midden in een vraag liep de klok gewoon door (24 s i.p.v. 30 s), en dubbel versturen of lege invoer deed niets vervelends.
- Beloningsmodal: duidelijk opgebouwd (XP-opbouw, level-up, badge), `role="dialog"`, en `prefers-reduced-motion` schakelt confetti en animaties uit.
- Spinner (nieuw): heeft `role="status"` met "Laden…" en een rustiger animatie bij verminderde beweging. Het laadscherm van de app toont een voortgangspercentage.
- Maandklassement met uitklapbare uitleg ("Hoe wordt het maandklassement berekend?") en eigen rij gemarkeerd.
- Avatar-editor: live preview blijft vastzitten bovenaan met een grote "Opslaan"-knop; veel keuzes zonder overweldigend te worden. Kiezen van een voetballer-avatar is één tik en wordt bevestigd.
- Lobby: heldere plaatsen ("Vrije plaats"), wachtstatus ("Wachten tot de organisator start…"), organisator-label.
- Taal: kort, Vlaams, vriendelijk; foutmelding op de login is vriendelijk en concreet.
- Geen horizontale scroll op 320, 375 en 768 px; safe-area-insets worden gebruikt in header en navigatiebalk.

## 6. Quick wins (< 1 uur)

1. `OpenTables.razor` regel 29 en 52: `@m.Questions vragen@if` oplossen (spatie of `@(…)`), zodat de code en het valse Derby-label verdwijnen (G1).
2. `input, button, select { font: inherit; }` in `app.css` (K4).
3. Header: `white-space: nowrap` op logo-tekst en `.lvl`-badge, `max-width` + ellipsis op de naam, `overflow-wrap: anywhere` op de H1 (G4).
4. Badgekaart: opacity niet op de hele kaart en beschrijving ≥12 px met volle tekstkleur; groene tekst donkerder (G6).
5. "Join" wordt "Doe mee"; "Stats" wordt "Statistieken" of "Cijfers"; "Overslaan" in de beloningsmodal wordt "Verder"; "OK" wordt "Antwoord" (K3).
6. `min-height: 44px` op `.linkish`, chips en tabs (K1).
7. `role="status"` rond resultaat en foutmeldingen, `aria-pressed` op de toggles, `aria-label` op kleurstalen (G7).
8. "Volgende vraag" sticky boven de bottom-nav in `Question.razor` (G3).
9. Melding "Je avatar is opgeslagen." wissen bij tabwissel (K9).
10. Link "Wachtwoord vergeten?" en een oogje bij het wachtwoordveld toevoegen (G8).

## 7. Niet getest

- **Special**: er was geen actieve special in de gebruikte database (`/api/specials` gaf een lege lijst en Home toont geen special-kaart). Niet beoordeeld.
- Afronding van een online-wedstrijd (eindscherm en beloning), omdat de tweede speler niet meespeelde en ik de wedstrijd niet wou laten uitlopen. De reveal na een ronde en het verlaten-bevestigingsvenster (native `confirm`) zag ik enkel deels.
- Echt toetsenbord (invoer die achter het schermtoetsenbord valt), notch en trage verbinding: de browserpane kan dat niet nabootsen.
- Admin-schermen vielen buiten de kernflows.
