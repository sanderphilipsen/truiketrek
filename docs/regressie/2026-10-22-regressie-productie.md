# Regressie vóór productie – Truitje Trek (22 oktober 2026)

Getest op commit `d5576b9` (main), lokaal op http://localhost:5174 met de dev-database, op gsm-formaat (375 px en 320 px).
Drie testers hebben elk een deel doorgespeeld in een eigen browsertab met een eigen testaccount; de ux-designer deed apart een ontwerpreview (zie [../ux-reviews/2026-10-22-ux-review-productie.md](../ux-reviews/2026-10-22-ux-review-productie.md)).

## Conclusie

**Bijna klaar voor productie.** De kernlus (daily, academie, klassement, badges, profiel, specials, admin, online) werkt zonder blokkerende fouten. Er zijn twee grote problemen die vóór de deploy opgelost moeten worden, en een reeks kleine die na de release kunnen.

| Deel | Oordeel |
|---|---|
| Build, tests, migraties, publish | ✅ groen |
| Spelerflows en daily (12 van 13 vraagtypes gespeeld) | ✅ klaar |
| Online partijtjes (9 types, derby, herladen, verlaten) | ⚠️ na fix van G1 |
| Admin en specials | ⚠️ na fix van G2 |

## Automatische controles

- `dotnet build -c Release`: 0 fouten, 0 waarschuwingen.
- `dotnet test`: **306 geslaagd, 0 gefaald, 1 overgeslagen.** De overgeslagen test is `PostgresMigrationTests` (Testcontainers vindt Podman niet, ook niet met `DOCKER_HOST`). De migraties zijn wel gecontroleerd: `dotnet ef migrations has-pending-model-changes` meldt geen openstaande modelwijzigingen; laatste migratie `20261021065908_MatchVersion`.
- `dotnet publish -c Release`: slaagt; de Blazor-output (219 bestanden in `_framework`) en de service worker worden gegenereerd.
- `GET /api/status` geeft de juiste versie (`d5576b9…`) en `updateAt: null`.
- Productie draait zonder `ASPNETCORE_ENVIRONMENT` en dus als Production: geen DeveloperExceptionPage, geen stacktraces met cookies naar de client (lokaal in Development wel, dat is normaal).

## 🟠 Groot: vóór productie oplossen

**Status (22/10, later op de dag): G1 en G2 zijn opgelost en gecontroleerd.** De zegeregel staat nu op één plek (`MatchRules.IsWin`: alleen eerste, met genoeg spelers die bleven) en wordt gebruikt door `OnlineService`, `MonthRankingService`, `ProgressService` en `ResultsService`; het eindscherm toont "Gelijkspel!" met een uitleg, en de beloningspopup geeft dan enkel de +25 XP. Negen tests erbij (315 groen). De admin-CSS kreeg `color: var(--text)` op `.surface-light`. Beide zijn nagespeeld in de browser: een 0–0 met twee spelers geeft voor beiden "Gelijkspel!" zonder zege-XP, en de spelerslijst in admin is donker op wit.

### G1. Gelijkspel online telt voor beide spelers als zege (misbruik mogelijk)
- **Waar:** `src/VoetbalQuiz.Server/Services/MatchScoring.cs:99` (gedeelde rang bij gelijke score) in combinatie met `OnlineService.cs:34`, `ProgressService.cs:202/217` en `ResultsService.cs:77` (`Won = Rank == 1 && Counts`).
- **Reproductie:** partijtje van 2, beide spelers passen op elke vraag (0–0) of eindigen gelijk. Beiden krijgen rang 1, "Online zeges +75 XP", een gewonnen wedstrijd in de stats en de reeksbadges (wout kreeg zo "Partijtjeskoning" na 3 op rij).
- **Daarbovenop:** het eindscherm (`Pages/Wedstrijd.razor`) kiest de winnaar met `FirstOrDefault(p => p.Rank == 1)`, zodat lotte "Jij wint!" ziet en wout "lotte wint!" terwijl zijn kicker "Wat een zege!" zegt.
- **Voorstel:** een gelijkspel is geen zege (geen zege-XP, geen tel in `MatchesWon`, reeks blijft staan zoals bij een niet-tellende wedstrijd) en het eindscherm zegt "Gelijkspel". Eventueel: een wedstrijd waarin niemand punten scoorde telt helemaal niet mee. Test `Finish_shares_ranks_on_a_tie…` uitbreiden met de zege-regel.

### G2. Tekst in het hele admin-paneel is bijna onzichtbaar
- **Waar:** `src/VoetbalQuiz.Client/wwwroot/css/app.css` regel 41 (`.surface-light`) en regel 69 (`html, body { color: var(--text) }`).
- **Wat je ziet:** op `/admin`, `/admin/spelers`, `/admin/vragen`, … staan spelersnamen, kaarttitels, chips, dashboardcijfers en labels in bijna wit-groen (`rgb(227,244,234)`) op een witte kaart. Enkel elementen met een eigen `color: var(--text)` zijn donker.
- **Oorzaak:** `.surface-light` zet de variabele `--text` wel terug op donker, maar `color` wordt op `html, body` berekend met de donkere-themawaarde en als berekende kleur overgeërfd; de variabele wordt niet opnieuw geëvalueerd.
- **Fix:** `color: var(--text);` toevoegen aan `.surface-light` (één regel).

## 🟡 Klein: kan na de release

**Online**
- Beloningspopup ontbrak één keer na een mix-wedstrijd bij beide spelers, terwijl de XP wel geregistreerd werd (`SeenXpParts`); niet reproduceerbaar. Verdachte: één claim die ophalen én "gezien" wegschrijven combineert (`POST /api/progress/reward`), zonder herstel bij een hernieuwd component.
- Eindscherm toont geen Elo-wijziging; die zie je pas in Klassement › Online.
- Kicker "WAT EEN ZEGE!" na een wedstrijd die expliciet niet als zege telt (tegenstander vertrokken).
- Mix is niet willekeurig: de volgorde is vast afwisselend in de volgorde van de gekozen chips, de eerste vraag is altijd het eerste type.
- De achtergrond `.pitch-fx` (`position: fixed`) werd tijdelijk 404 px breed op 375 px tijdens animaties; op een echte gsm kan dat een zijwaartse pan toelaten. Vermoedelijk testartefact (pane niet gerenderd), toch nakijken met `overflow-x: hidden` op `html`.
- "1 spelers" in "Recent gespeeld"; reveal van Counter zegt "Het was… Counter (10 spelers)"; "Plaats 1 van 2" in de reveal is dubbelzinnig (vraag of klassement).
- Ongeldige JSON naar `POST /api/matches` geeft een ruwe .NET-exception in de body (de app stuurt dat nooit).
- Niet getest: derby-wapens Trump, Verlenging en Propere handen (willekeurig toegewezen), lobby-timeout van 2 uur, een derde plaats (+15).

**Spelerflows**
- Elke paginalaad geeft een 404 op `/VoetbalQuiz.Client.styles.css` (`wwwroot/index.html:18`); er bestaat geen `.razor.css`. Regel verwijderen.
- "Zo werkt het"-vlag zit in localStorage zonder gebruiker: een tweede nieuw account op hetzelfde toestel ziet de uitleg niet.
- E-mail is verplicht bij registratie maar staat nergens aangegeven; te kort wachtwoord geeft een browserbubbel; na een mislukte login blijven naam en wachtwoord staan in het registratieformulier.
- Lege invoer of enkel spaties bij OK/Enter wordt stil genegeerd; niet-afgewerkte academiepoging heeft geen link om verder te spelen.
- Wie alles overslaat krijgt toch 150 XP (10 per vraag + 50 volledige dag).
- Paginatitel `/daily` is Engels ("Daily challenge"); daily-overzicht toont enkel het antwoord, niet de vraag; "Vraag 99 / 10" in de kop bij een onbestaande vraag.
- Feedbacktekst: "Op de paal!" bij 1 van 6 namen (Opstelling), "Rakelings!" bij 1 van 10 (Counter); statusregel van Counter breekt lelijk af naast de klokchip.
- Klassement Online: gelijke rating krijgt toch aparte rangnummers; bladeren in Maand heeft geen ondergrens.
- Inhoud: in de Clasico-special komt Youri Tielemans zowel bij Wissel als bij Kluts voor.
- Tikvlakken onder 44 px: tabs Klassement (37), Profiel (40/37), naamlinks in het klassement (21), jaar-schuifregelaar (42), "Top 3"-link in de specialkaart (19).

**Admin en specials**
- Thema-chips tonen soms de ruwe sleutel zonder label of emoji (ballon-dor, anderlecht, club-brugge, standard, oranje, legende, beker-van-belgie, maradona, pele, messi, …).
- Geen "opgeslagen"-bevestiging bij een nieuwe speler en bij een feedback-antwoord in admin.
- Placeholder van Carrièrepad toont letterlijk `&#10;`.
- AdminGuard-weigering heeft geen terugknop.
- Special-editor: de Verdeling-voorbeeldregel toont ook types die in Planning uit staan, hoewel de tekst zegt dat die overgeslagen worden; het aantal vragen van een special ligt vast op 10.
- Tikvlakken onder 44 px: admin-tabs (37), "‹ Terug naar de lijst" (21), chips (31–34).
- Niet getest: badge "Specialist" en de beker na afloop van een special (de special liep nog), servercontrole op te weinig vragen per type.

## Dekking

- **Vraagtypes gespeeld:** alle 13 (Wie ben ik, Snelvuur, Carrièrepad, Wie hoort er niet bij, Raad het jaar, Emoji, Opstelling, Wissel, Rugnummer, Mannetje, Kluts, Counter in daily/academie; Op volgorde enkel online, want het zit in geen enkele oude daily of special).
- **Admin:** alle 10 vraageditors geopend en opgeslagen; spelers zoeken, filteren, bewerken, goedkeur-validaties, nieuwe speler toevoegen en afwijzen; planning (mix, type uit/aan, weigering "minstens één type"); specials (alle sjablonen, aanmaken, bewerken, spelen, vergrendeling na spelen); feedback heen en terug; 403 op alle admin-API's voor een gewone speler.
- **Online:** 7 wedstrijden (mix van 5 types, Kluts, Counter, Carrièrepad/Opstelling, 2 derby's met Gokchinees, Bengaals vuur, Rode kaart en Wissel, 1 verlaten wedstrijd), 5 lobby-annuleringen, joinen via link, volzet, toeschouwer, herladen tijdens een vraag, Elo nagerekend (K=32).
- **Robuustheid:** herladen en browser-terug midden in een vraag (klok loopt door), onbekende routes, 320 px zonder horizontale scroll, geen JS-fouten, geen hangende spinners, geen zichtbare Razor-code.

## Sporen in de lokale dev-database

Accounts `regr_nieuw_4821`, `regr_pop_6173`, `regressieadmin`, `regrderde`, `ux_nieuw_7316`; special "Regressie-special" (id 11, gespeeld door fien, dus niet verwijderbaar); speler "Regressie Testspeler" (id 388, afgewezen); feedback id 8 en een testmelding; academiepogingen en online wedstrijden 80–90. `appsettings.Development.json` is teruggezet (de tijdelijke admin `regressieadmin` is er weer uit).
