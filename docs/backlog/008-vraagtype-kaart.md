# 008 – Kaartdag: "Wie speelt hier?"

- **Soort:** feature
- **Status:** bezig (gebouwd op branch `kaartdag`, nog niet gedeployed)
- **Prioriteit:** midden
- **Moeite:** M
- **Aangemaakt:** 2026-10-22
- **Bron:** brainstormgesprek over nieuwe vraagtypes; concept afgestemd met de eigenaar op 2026-10-23

## Waarom

Alle huidige types zijn intypen, meerkeuze of een getal schatten. Een kaart met spelden is een nieuwe manier van spelen, en weten waar een club thuis speelt is voetbalkennis die nog niet gevraagd wordt. Als aparte kaartdag is het een herkenbaar moment, bijvoorbeeld in een derbyweekend.

## Wat

- Een **speciale daily** met alleen dit type: de **kaartdag**. Het is geen type dat tussen de gewone daily-vragen opduikt.
- De speler ziet een **kaart van België met provinciegrenzen**. De **spelden** verschijnen **één voor één** op de stadions van een competitie (bv. alle clubs uit eerste klasse).
- Bij elke speld kiest de speler in een **keuzelijst** welke club daar speelt. Je tikt dus niet op de kaart, waardoor er geen probleem is met precisie op 375 px.
- Na het antwoord blijft de speld staan, groen (juist) of rood (fout), met de clubnaam. Zo vult de kaart zich gaandeweg.

### Beslissingen (2026-10-23)

- **De keuzelijst toont altijd alle clubs** van de competitie, ook de clubs die al geplaatst zijn. Zo is de laatste speld geen weggever en lukt gokken door eliminatie niet.
- **Gedeeld stadion** (bv. Jan Breydelstadion voor Club Brugge en Cercle Brugge, als beide in die competitie spelen): **één speld waarbij je beide clubs moet noemen**. Elke juiste club telt.
- **Punten per juiste speld en een tijdslimiet voor de hele kaart.**
- **Competities:** de **huidige eerste klasse** en de **Challenger Pro League** (tweede klasse), en sinds de uitbreiding ook Ligue 1, Serie A, LaLiga, Bundesliga en Premier League. Historische seizoenen nu niet.
- Het eerdere voorstel om te tikken op de kaart (afstand in km, puntenschijven) is vervallen.

### Beslissingen (2026-10-23, tweede ronde)

- **Twee delen op dezelfde kaart**, elk **max. 500 punten en max. 3 minuten**, met een eigen klok (deel 2 start pas als je eraan begint).
  - **Deel 1:** de spelden één voor één; kies de club(s) uit de lijst van alle clubs. Punten: floor(500 × juiste clubs / alle clubs).
  - **Deel 2:** dezelfde spelden, met de clubnaam (bij een gedeeld stadion beide clubs) erbij; kies het stadion uit de lijst van alle stadions (alfabetisch, altijd allemaal). Eén antwoord per speld; een gedeeld stadion is één speld en één stadion. Punten: floor(500 × juiste stadions / alle spelden). De naam die telt is `stadium` uit kaart.json (de huidige naam, sponsornaam inbegrepen).
- **Uitwerking:** twee vragen (`PerDay` 2 × 500) op hetzelfde item, met `DailyQuestion.Part` (migratie `KaartdagTweeDelen`). Zo krijgt elk deel vanzelf een eigen klok, een eigen poging en een eigen resultaat, en werken de daily, specials en de academie zonder extra code. Deel 1 noemt nooit een stadion, ook niet in het resultaat (anders verklapt het deel 2).
- **Volgorde:** geschud per deel (deel 2 anders dan deel 1), voor iedereen dezelfde.
- **Solo:** de kaart is een solo-type met `QuestionTypeInfo.Alone`, hetzelfde mechanisme als De Krant (019/592a453): nooit in een mix (in een combinatie valt ze weg, de admin weigert de combinatie), niet online, niet in de badge Allrounder of de "100 punten"-statistieken, enkel goedgekeurde items. Staat een solo-type uit, dan wordt een special met enkel dat type "Wie ben ik?". Er blijft altijd minstens één ander type aan. XP: de twee delen tellen samen als een volle dag (elk deel 5 gespeelde vragen) en de deelpunten tellen mee.
- **Niet online**, wel in de academie. Telt niet mee voor de badge Allrounder.
- **Enkel goedgekeurde kaarten** worden gespeeld. Een kaart die al in een reeks zit, verandert niet meer van spelden of clubs (admin en seeder); een nieuw seizoen is een nieuwe kaart.
- **XP (beslist door de eigenaar):** voor types met deelpunten (`QuestionRules.PartialScores`: kaart, opstelling, counter) tellen de behaalde punten mee voor "Juiste antwoorden", ook als de vraag niet volledig juist is. Een kaartdag van 900 punten levert zo evenveel XP voor juiste antwoorden als 900 punten op een gewone dag; het verschil zit enkel in "Gespeelde vragen" (2 tegenover 10 vragen). Het dagoverzicht toont zo'n deelscore als ½ in plaats van ✗. Raad het jaar en Op volgorde geven ook deelpunten, maar vallen (nog) niet onder deze regel.
- **Rotatie opgelost (zes landen):** zeven kaarten in zes landen (BE: Jupiler Pro League en Challenger Pro League, FR, IT, ES, DE, GB), elk met precies 10 spelden (`QuestionRules.KaartPins`, 50 punten per speld). De admin kiest per kaartdag in de weekplanning en per kaartspecial het land, of willekeurig (migratie `KaartLand`). De server neemt een goedgekeurde kaart van dat land; is er geen land gekozen of geen goedgekeurde kaart van dat land, dan een willekeurige goedgekeurde kaart, liefst niet van het land van de vorige kaartdag. De admin toont per land hoeveel goedgekeurde kaarten er zijn. De kaart zelf is `LandKaart` met een projectie per land (`KaartProjection.For(land)`), die ook de controle op de spelden gebruikt.

## Open vragen

- Hoe vaak komt de kaartdag: vaste dag in een special (derbyweekend), maandelijks, of handmatig via het admin-paneel? (Nu: via de weekplanning of een special.)
- Meer kaarten per land (vorige seizoenen, tweede klasses) zodat een land niet altijd dezelfde kaart geeft.

## Aanpak (optioneel)

- **Data:** `clubs.json` met per club een naam, aliassen, competitie(s), stadion en coördinaten (lat/lon), elk met een bron. Een kleine lijst (± 34 clubs) die je met de hand bijhoudt, geen Wikidata-koppeling nodig. Deze lijst kan [017 Clubraadsel](017-vraagtype-clubraadsel.md) hergebruiken (aliassen, stadion). Bij promotie of degradatie pas je de competitie aan.
- **Shared:** nieuw `QuestionType` (bv. `Kaart`) in `QuestionTypes.cs`, regels (tijd, punten) in `QuestionRules`. De kaartdag als dag met alleen dit type, via het mechanisme van specials (`Types` in `SpecialTemplates`).
- **Client:** SVG van België met provinciegrenzen (open data, bv. Statbel/NGI), een eenvoudige projectie van lat/lon naar x/y, spelden en een keuzelijst. Mobile-first, getest door `mobiel-tester` op 375 px.
- **Server-autoriteit:** de client krijgt alleen de coördinaten van de huidige speld en de lijst clubnamen, nooit welke club bij welke speld hoort. De server controleert en rekent de punten.
- Volg de skill `nieuw-vraagtype`; laten nakijken door `spelregel-reviewer`.

## Notities

- 2026-10-23, review landenkaarten: de sleutel van een kaart is land|competitie|seizoen (oude kaarten zonder land gaan via de seeder netjes op afgewezen); een speld moet binnen de grens van haar land liggen (`KaartGrenzen`, vereenvoudigde omtrek met 5 km marge, gegenereerd uit de buitenlijn van `KaartPaden`); de picker speelt nooit een kaart met problemen en kiest eerst het land, dan het thema van de special, dan een ander land dan de vorige kaartdag; een ongewijzigde kaart blijft in de admin beheersbaar, ook als ze niet meer aan de regels voldoet.
- **Bekende hints (aanvaard):**
  - Deel 2: wie de stadions kent die al geplaatst zijn, kan de laatste spelden door eliminatie raden (de lijst toont wel altijd alle stadions).
  - Deel 1: twee keuzelijsten bij een speld verraden een gedeeld stadion, en dus vrijwel altijd Brugge (Club en Cercle).
  - Een kaartspecial en een kaartdag kunnen dezelfde kaart gebruiken (er is maar één goedgekeurde kaart per competitie): wie de ene speelde, kent de antwoorden van de andere.

- 2026-10-23: eerste versie gebouwd (één vraag van 1000 punten), daarna op vraag van de eigenaar omgebouwd tot twee delen van 500 punten, met de bevindingen van de spelregelreview (nummer van de speld bij elke gok, kaart vast zodra gespeeld, enkel goedgekeurde kaarten, terugval bij een uitgeschakelde kaart) en van de mobiele test (vast actiepaneel onderaan, vaste plaats voor de reactie).
- 2026-10-23: de planning stelde voor om dit item te schrappen (tikprecisie, overlap met Clubraadsel). Met het concept met spelden en een keuzelijst vervalt het precisieprobleem en daalt de moeite van L naar M. De eigenaar wil het behouden.
