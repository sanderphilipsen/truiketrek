# 037 – Academie als maandkalender met onderblad en dagklassement

- **Soort:** ux
- **Status:** bezig
- **Prioriteit:** midden
- **Moeite:** L
- **Aangemaakt:** 2026-10-26
- **Bron:** feedback van Sander ("de academie is niet zo overzichtelijk"), UX-review 2026-10-22 (N5)
- **Mockup:** [design-poc/academie-kalender.html](../design-poc/academie-kalender.html)

## Waarom

De academie ([007](007-gemiste-daily-naspelen.md)) is één lange lijst van 25 à 30 gelijke stroken per maand, nieuwste eerst. Daardoor:

- zie je een maand nooit in één blik, en is het verschil tussen "gemist" en "gespeeld" enkel een stip van 8 px en een knop;
- valt er niets te kiezen: per dag zie je alleen de datum;
- zitten drie toestanden in twee. *Gemist*, *echt gespeeld* en *enkel geoefend* zijn verschillend, maar de lijst kent alleen "open" en "played". De gemiste dagen zijn net de interessantste;
- toont de lijst rechts alleen je echte score. Wie een dag enkel oefende, ziet geen getal, hoewel de server `BestTraining` al meestuurt;
- gaan specials verloren tussen de gewone dagen;
- moet je via een detailpagina met één knop: een extra tik en een paginawissel;
- staat "telt niet mee" drie keer op het scherm;
- loopt het na een poging dood ("Iets anders kiezen" brengt je terug naar de lange lijst);
- merk je pas op de detailpagina dat een dag geblokkeerd is omdat een vraag in de daily van vandaag zit;
- kan je het klassement van een voorbije dag nergens meer zien. Het dagklassement toont enkel vandaag. Een gemiste dag naspelen voelt daardoor als spelen in het luchtledige: "4de van 19" is een getal, geen plaats tussen je maten.

## Wat

### 1. Kop

`PageHeader` "Academie", label "Oefenen · telt niet mee". Dat is de enige plek waar "telt niet mee" staat. Rechts in gele cijfers: het aantal gemiste dagen in de getoonde maand.

### 2. Aanrader

Een tegel boven de kalender: **"Gemist · donderdag 22 oktober"** met de facts van die dag en één gele knop **"Speel ›"**, die meteen een poging start (zonder onderblad). Dit is de snelste weg voor wie gewoon wil bijspelen.

- Welke dag: de meest recente gemiste dag die niet geblokkeerd is (zie open vraag 3).
- Niets meer gemist: "Alles bijgespeeld deze maand" met een secundaire suggestie, bv. de dag waar je toen het laagst eindigde: "Revanche op 3 oktober: je werd 11de van 14".

### 3. Maandkalender

Een rooster van 7 kolommen (maandag eerst), dat met kop en aanrader samen op één gsm-scherm past. Op 375 px is een cel ongeveer 46 × 52 px, dus elke dag is een tikvak van minstens `--tap`.

| Toestand | Cel |
|---|---|
| **Gemist** | gele ring, datum in geel (`--sb-digit`) |
| **Echt gespeeld** (op de dag zelf) | gevulde tegel, datum klein, eronder je echte score in gele scorebordcijfers |
| **Nagespeeld** (je eerste keer was in de academie; je staat zo in het dagklassement, zie 5) | groene rand (`--ok`), eronder de score van die eerste poging in groene cijfers |
| **Geblokkeerd** (vraag zit in de daily van vandaag) | slotje in plaats van score, gedimd |
| **Vandaag en later** | gedimd, niet tikbaar. Vandaag krijgt een kleine onderstreping |
| **Geen daily** | leeg |

De vroegere toestand "enkel geoefend" verdwijnt. Wie een gemiste dag voor het eerst speelt, staat voortaan in het dagklassement, en dan heet de dag *nagespeeld*. Extra pogingen daarna zijn oefening en veranderen de cel niet.

- **Specials:** een gekleurde band onder de dagen die ze beslaan, met het icoon van de special. Loopt de special over twee weken, dan loopt de band door op de volgende rij. Omdat zo'n band te klein is om op te tikken, staan de specials van de maand ook als stroken onder de kalender ("Specials in oktober").
- **Maandwissel:** ‹ en › als knoppen van 44 px naast de maandnaam, plus horizontaal swipen over het rooster. Bij een maandwissel blijft de aanrader staan (die gaat over alle maanden, zie open vraag 3).
- **Legende:** één regel onder het rooster: gemist · gespeeld · nagespeeld · special.

### 4. Onderblad per dag

Tik je op een dag (of op een special-strook), dan schuift een onderblad omhoog. Het gebruikt dezelfde `sheet` als `ConfirmSheet`.

- **Kop:** "Dinsdag 14 oktober" (of de naam van de special met de data).
- **Facts:** "8 vragen · 23 spelers speelden die dag", plus het vraagtype en de moeilijkheid (zie open vraag 1).
- **Scores:** drie cijfers naast elkaar, in de stijl van de scorebordtegels:
  - *Klassement*: je plaats in het dagklassement, met je score, of "–" als je de dag nog nooit speelde;
  - *Beste oefening*;
  - *Pogingen*.
- **Klassement van die dag:** een mini-stand in de stijl van het dagklassement: de top 3 en jouw rij (met de rij erboven en eronder als je niet in de top 3 staat), en daaronder de link **"Volledig klassement ›"** (zie 5). Speelde je de dag nog nooit, dan zie je de top 3 en een regel "Speel om erbij te komen".
- **Alle pogingen:** klapt open met de bestaande `TrainingScores`.
- **Voet:** de hoofdknop, altijd in duimbereik:
  - nog nooit gespeeld: **"Spelen › · je komt in het klassement"**;
  - al gespeeld: *Opnieuw spelen ›*, met "Oefenen, telt niet mee" klein eronder;
  - geblokkeerd: "Een van deze vragen zit in de daily van vandaag" en een knop *Naar de daily ›*.
- **Bladeren:** pijltjes ‹ › in de kop van het onderblad brengen je naar de vorige of volgende dag zonder het blad te sluiten (zie open vraag 7).

### 5. Dagklassement van een voorbije dag

Een nieuw scherm, `/academie/2026-10-14/klassement` (en `/academie/special/12/klassement` voor een special). Het opent vanuit het onderblad en vanuit het resultaat van je eerste poging.

- **Dezelfde lijst als het dagklassement van vandaag** (`board`), met de kop "Klassement · dinsdag 14 oktober" en terug naar de academie (met het onderblad van die dag open).
- **Wie erin staat:**
  - iedereen die de dag echt speelde;
  - iedereen die hem later voor het eerst speelde in de academie, met een klein petje en "nagespeeld";
  - per speler één rij: zijn echte poging, of anders zijn eerste academiepoging. Latere oefenpogingen komen er nooit in.
- **Punten en XP worden nooit herberekend.** Een nagespeelde rij:
  - verandert niets aan de dagwinnaar (`WinnerUserId`, die is bij het afsluiten vastgelegd), de podiumbonus, het maandklassement, XP, de reeks of badges;
  - wordt in de lijst wel op zijn plaats gezet volgens de punten (zie open vraag 8 voor de nummering);
  - een kroontje of podiumplek hoort enkel bij de echte top 3 van die dag.
- **Onderaan** dezelfde uitleg als in de kop van de academie, maar dan specifiek: "Nagespeelde scores staan erbij, maar tellen niet voor de dagwinnaar, het maandklassement of XP."
- Specials werken hetzelfde: hun klassement (`GetSpecialLeaderboardAsync`) toont na afloop ook de nagespeelde rijen.

### 6. Adressen en de oude detailpagina

`/academie/2026-10-14` en `/academie/special/12` blijven bestaan. Ze openen de kalender van die maand met het onderblad al open. Zo blijven links uit meldingen en de knop "terug" van een poging werken. De aparte detailpagina (`TrainingDag.razor`) verdwijnt.

### 7. Na een poging

Op het resultaat van een poging (`TrainingRun.razor`):

- **na je eerste poging op een gemiste dag:** "Je staat in het klassement van 14 oktober, **tussen de 3de en de 4de**", met de link *Bekijk het klassement ›*. Dat vervangt "Daarmee was je 4de geweest". Met nummering (a) uit open vraag 8 wordt dat "Je staat **4de van 20**";
- **na een oefenpoging:** zoals nu, "Daarmee was je 4de geweest", plus het verschil met je klassementsscore;
- de hoofdknop wordt **"Volgende gemiste dag ›"** (de volgende aanrader). Is er niets meer gemist, dan **"Terug naar de kalender"**;
- *Opnieuw spelen* wordt de tweede knop;
- terug in de kalender licht de net gespeelde dag even op, zodat je ziet dat de cel van kleur veranderde.

## Wat er technisch bij moet

- **Waar de nagespeelde scores staan:** ze blijven trainingsruns (`TrainingRun`), nooit gewone pogingen (`Attempt`). Dit is het belangrijkste punt van dit item. `PodiumsAsync` berekent de podiumbonussen van het maandklassement live opnieuw uit de echte pogingen, en ook XP en badges lezen die. Een nagespeelde score die daar binnensluipt, verandert dus achteraf het maandklassement.
- **Welke run in het klassement komt:** de eerste trainingsrun van een speler op een dag die hij niet echt speelde. Gebouwd als afleiding, niet als kolom: de run waarin hij het eerst een vraag startte (`ResultsService.ReplaysAsync`). Runs en hun vragen worden nooit verwijderd, dus dat blijft stabiel, en het geldt meteen ook voor bestaande runs (open vraag 10). Zie open vraag 9 voor een run die je niet afwerkt.
- **Shared:**
  - `TrainingDay` krijgt `Blocked` (nu weet alleen `TrainingDayDetail` dat via `SharesWithToday`) en `BoardRank`/`BoardPoints` (je plaats en score in het dagklassement, echt of nagespeeld);
  - `TrainingArchive` krijgt `Suggestion`: de aanrader over alle maanden heen, met de datum of de special en waarom ("gemist" of "revanche");
  - `LeaderboardEntry` krijgt `Replayed` (nagespeeld in de academie);
  - `TrainingSummary` krijgt `CountsForBoard` en de plaats in het klassement.
- **Server:**
  - `TrainingService.GetArchiveAsync` vult `Blocked`, `BoardRank` en `Suggestion`. Let op de kosten: `OpenTodayAsync` één keer per archiefopvraging, niet per dag;
  - een nieuw endpoint `GET /api/leaderboard/day/{date}`, alleen voor dagen die voorbij zijn. Het voegt de echte stand (`ResultsService.RankDay`) en de eerste academieruns samen;
  - `GetSpecialLeaderboardAsync` neemt na afloop van de special de nagespeelde rijen mee.
- **Migratie:** niet nodig (zie hierboven).
- **Geen lekken:** vraagtypes en moeilijkheid stonden al in `TrainingDay`, de antwoorden niet. Het klassement van een voorbije dag toont scores, geen antwoorden. De `NoLeakTests` blijven groen.
- **Client:**
  - `Training.razor` wordt kalender, aanrader en specials;
  - een nieuwe component `TrainingCalendar` (rooster en cellen);
  - een nieuwe component `TrainingDaySheet` met de mini-stand, die bij het openen het detail-endpoint en het dagklassement oproept;
  - een nieuwe pagina `TrainingBoard.razor` die de bestaande lijst van het dagklassement hergebruikt;
  - `TrainingRun.razor` krijgt de nieuwe knoppen en zinnen;
  - `academie.css` wordt herschreven.
- **Toegankelijkheid:**
  - het rooster als `<table>` met kolomkoppen ma–zo, elke dag een `<button>` met een volledig `aria-label` ("14 oktober, gemist");
  - pijltjestoetsen om tussen dagen te bewegen;
  - het onderblad zet de focus op zijn kop en sluit met Escape;
  - in het klassement heeft "nagespeeld" ook tekst, niet enkel het petje;
  - kleur is nooit het enige signaal: de toestanden verschillen ook in vorm (ring, vulling, rand, slot);
  - geen animatie bij `prefers-reduced-motion`;
  - het lichte thema werkt via de tokens.
- **Tests** (`TrainingTests`, en waar nodig `MonthRanking`):
  - **het vangnet:** een nagespeelde score verandert niets aan de dagwinnaar, `PodiumsAsync`, het maandklassement, XP of badges;
  - alleen de eerste run telt, en nooit als je de dag echt speelde;
  - het dagklassement van vandaag bestaat niet via het nieuwe endpoint, alleen via `/today`;
  - `Blocked` en `Suggestion`: gemist eerst, geblokkeerd overgeslagen, over maanden heen, revanche als niets gemist.

## Open vragen (beslist op 2026-10-26)

Sander nam alle voorstellen hieronder over, ook nummering (b).

1. **Vraagtype en moeilijkheid tonen?** Die werden in de lijst bewust weggelaten ("dat zie je pas als je speelt"). Voorstel: tonen in het onderblad, niet in de cel. Dan heb je een reden om te kiezen, en blijft het rooster rustig.
2. **Wat staat er in een cel?** Voorstel: je score in het dagklassement (echt of nagespeeld), anders niets. Alternatief: enkel een kleur, zonder cijfers. Dat is rustiger, maar je ziet minder.
3. **De aanrader:** de meest recente gemiste dag, of de oudste? En over alle maanden, of alleen de getoonde maand? Voorstel: de meest recente, over alle maanden.
4. **Dagen vóór je inschrijving:** tellen die als "gemist"? Een nieuwe speler zou dan een kalender vol gele ringen zien, en het getal rechts in de kop wordt groot. Voorstel: die dagen krijgen een neutrale cel ("vóór je tijd", wel speelbaar) en tellen niet mee in het getal. Als je ze speelt, kom je wel in hun klassement.
5. **Specials:** een band in de kalender én stroken eronder, of enkel de stroken?
6. **Revanche als suggestie:** een goed idee, of liever niets tonen als alles bijgespeeld is?
7. **Bladeren in het onderblad** met ‹ ›: nodig, of is sluiten en een andere dag aantikken genoeg?
8. **Nummering in het klassement.** Wie nagespeeld heeft, schuift in de lijst tussen de echte spelers:
   - **(a) Iedereen hernummerd:** iedereen krijgt een nieuw nummer, maar kroontje en podium blijven bij de echte top 3.
   - **(b) Echte nummers blijven:** de nagespeelde rij krijgt geen nummer maar een petje, en de echte spelers houden hun oorspronkelijke plaats.

   Voorstel: (b). Wie die dag 3de werd, blijft 3de: de geschiedenis verandert niet, en dat sluit aan bij "nooit herberekend". In het onderblad en na je poging staat dan "zou 4de zijn van 20".
9. **Een eerste poging die je niet afwerkt:** telt die met de punten tot dan, zoals bij de echte daily? Of telt dan de eerste afgewerkte poging? Voorstel: de eerste poging telt, ook onafgewerkt. Anders kan je beginnen, de vragen bekijken, stoppen en daarna met de antwoorden in je hoofd in het klassement komen.
10. **Bestaande trainingsruns:** krijgen de runs die al gespeeld zijn op gemiste dagen achteraf een plaats in het klassement? Voorstel: ja, de eerste per dag. Het was toen ook de eerste keer.
11. **Zichtbaar voor iedereen?** Zien andere spelers de nagespeelde rijen in het klassement van die dag, of alleen jij je eigen rij? Voorstel: iedereen, gemarkeerd. Het maakt de academie zichtbaarder, en met (b) verliest niemand zijn plaats.

## Aanpak

1. Afstemmen op de open vragen, status naar `klaar`.
2. **Regels en vangnet** (wat telt, wat nooit herberekend wordt): `spel-econoom` kort laten meekijken.
3. **Server, Shared en migratie** (`CountsForBoard`, `Blocked`, `Suggestion`, dagklassement-endpoint) met tests: `test-bouwer`, daarna een check door `spelregel-reviewer`.
4. **Client** (kalender, onderblad, dagklassement, nieuwe knoppen na een poging): `ux-designer`.
5. **Overgangen** (onderblad, maandswipe, de cel die oplicht): `animatie-bouwer`.
6. **Doorspelen** op 375 px, in het donkere en het lichte thema: `mobiel-tester`.

## Notities

- 2026-10-26: concept met klikbare mockup. Een lichtere variant in de bestaande lijst (aanrader, filter Gemist/Gespeeld/Specials, rijkere rijen) werd ook voorgesteld, maar Sander koos voor de volledige herwerking.
- 2026-10-26: Sander voegde toe dat je vanuit de academie het klassement van die dag moet kunnen openen, en dat je bij je eerste keer spelen in dat klassement komt. Punten en XP worden daarbij nooit herberekend. Daardoor vervalt de toestand "enkel geoefend" en komt "nagespeeld" in de plaats.
- 2026-10-26: alle voorstellen bij de open vragen overgenomen, ook nummering (b). Status naar `klaar`, en meteen `bezig`.
- 2026-10-26: gebouwd, nog niet gecommit.
  - **Server:** `ReplaysAsync` en `GetPastDayLeaderboardAsync` (ResultsService); het archief met `Blocked`, `Replayed`, `BeforeYourTime` en `Suggestion`; `GET /api/training/days/{date}/board`; het specialklassement met de nagespeelde rijen.
  - **Client:** `TrainingCalendar`, `TrainingDaySheet`, `TrainingBoard.razor`, en het nieuwe resultaat in `TrainingRun.razor`. `TrainingDag.razor` is weg.
  - **Tests:** 7 nieuwe in `TrainingTests`, waaronder het vangnet (dagwinnaar, podium, maandklassement en XP veranderen niet).
  - **Afwijkingen:** het onderblad openen en sluiten verandert het adres niet, want navigeren liet de pagina schuiven en naar boven springen. Een link naar een dag opent het blad wel.
  - **Label:** `TrainingRules.Label` is nu "telt niet voor het maandklassement of XP".
  - **Nog niet gebouwd:** de animaties van de aanpak (stap 5, `animatie-bouwer`). Er is enkel het oplichten van de net gespeelde dag.
- 2026-10-26: review door `spelregel-reviewer`. Er lekt niets naar dagwinnaar, podium, maandklassement of XP, en er lekken geen antwoorden. De randgevallen zijn opgelost, met tests:
  - een verlengde (weer lopende) special toont geen nagespeelde rijen;
  - wie de echte daily begon zonder een vraag af te sluiten, telt als echt gespeeld (`Started`): niet gemist en nooit nagespeeld;
  - een lege run telt niet als gespeeld;
  - "dicht" staat enkel bij dagen die je nooit zag, anders verraadt het welke antwoorden vandaag terugkomen;
  - de aanrader bekijkt 20 kandidaten in plaats van 5.
