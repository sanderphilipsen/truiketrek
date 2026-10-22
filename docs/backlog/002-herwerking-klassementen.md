# 002 – Herwerking van de klassementen

- **Soort:** feature
- **Status:** bezig
- **Prioriteit:** midden
- **Moeite:** L (fase 1 M-L, fase 2 M)
- **Aangemaakt:** 2026-10-20
- **Bron:** eigen idee

## Waarom

1. **Online hoort niet in de maand.** Het maandklassement moet over de dailies en de specials gaan. Vandaag tellen de 20 beste online wedstrijden van de maand mee (`MonthRules.OnlineBestMatches`), waardoor wie veel online speelt het maandklassement en dus de Gouden Bal kan beïnvloeden.
2. **Versnipperd, zonder vaste set.** Dag en maand op het klassementscherm, een klassement per special, het ratingklassement op de online-pagina, de Gouden Bal op Stats, bekers in de prijzenkast en je dagplaats op Home. We willen een beperkt aantal klassementen met elk een duidelijke rol, en geen wildgroei.
3. **Enkel een getal.** Het maandklassement toont één totaal en een vlakke lijst. Je vindt jezelf moeilijk terug, je ziet niet of je gestegen of gedaald bent, en je ziet niet wie in de prijzen valt of onderaan bengelt.

## Wat

### De vaste set: drie klassementen op één scherm

Het klassementscherm krijgt drie tabbladen. Meer komt er niet bij.

| Tab | Wat telt | Periode | Beloning |
|---|---|---|---|
| **Vandaag** | de daily van vandaag | dag | dagzege (bestaat) |
| **Maand** | daily-scores + special-scores + podiumbonussen | maand, met bladeren | beker top 3, Gouden Bal (bestaan) |
| **Online** | rating (het bestaande `OnlineLeaderboard`) | doorlopend | badges (bestaan) |

Wat er buiten valt en dat blijft:

- **Special:** elke special houdt haar eigen klassement op de specialpagina (beker + badge Specialist). Niet als tab, want ze is er maar af en toe; haar punten tellen in de maand.
- **Home-tegel** met je dagplaats en de **Gouden Bal** op Stats blijven; ze zijn geen klassement maar een verwijzing ernaar.
- **Geen** week, seizoen, all-time, per vraagtype of per thema.

### Online uit de maand

- `MonthRules`: online-punten verdwijnen uit de telling en uit `Explanation`. `OnlineBase`, `OnlinePerOpponent` en `OnlineBestMatches` vervallen (of blijven enkel voor de online-maandbeker, zie open vragen).
- `MonthEntry`: velden `Online`, `Matches`, `MatchesWon` verdwijnen uit het maandklassement.
- De Gouden Bal verandert niet van regel: wie bovenaan de maand staat, wint hem.

### De lijst als een tv-stand

Zoals de stand van de Jupiler Pro League op tv: zones, pijltjes en jezelf duidelijk in beeld.

1. **Jouw rij springt eruit** (Vandaag én Maand). Vandaag is er een lichte gouden tint met een streepje links (`.board li.me`); dat valt te weinig op. Nieuw: een duidelijke rand in de accentkleur, naam in vet, en een label "jij" naast de naam. Sta je niet in beeld, dan blijft je rij onderaan **vastgepind** tot je ernaartoe scrolt.
2. **Gestegen of gedaald** (Maand). Voor elke rij een pijltje tegenover gisteren: ▲ groen met het aantal plaatsen, ▼ rood, of een grijs streepje bij gelijk. Nieuw in het klassement: geen pijl. "Gisteren" is de stand op het einde van de vorige dag (na middernacht), zodat de pijl de hele dag hetzelfde blijft en pas verandert als jij of anderen die dag spelen.
3. **Zones** (Maand). De top 3 krijgt een groene zone-streep links en een bekertje bij de rang (de "prijzenzone": zij krijgen de beker op het einde van de maand; de nummer 1 de Gouden Bal). De onderste rijen krijgen een rode zone-streep met een rood pijltje omlaag (de "degradatiezone", zonder gevolg, enkel als prikkel). Hoeveel rijen onderaan: zie open vragen; voorstel 3, enkel als er minstens 10 spelers in de maand staan.

Dezelfde lijst (`LeaderboardList`) krijgt daarvoor optionele kolommen: verschuiving en zone. Vandaag toont geen pijltjes (een dag heeft geen "gisteren") en geen zones, enkel de medailles die er al zijn.

### Fase 2: uitklappen en samenvatten (optioneel)

- Tik op een rij om ze uit te klappen: daily / specials / bonussen, dagen gespeeld, podiums, link naar het spelersprofiel.
- Samenvatting boven de lijst: "Je staat 7e, 140 punten achter de top 3, +2 sinds gisteren".

### Later (fase 3, apart item als het zover is)

- **Divisies** met echte promotie en degradatie per maand, pas als er genoeg actieve spelers zijn (~15 per divisie). De degradatiezone uit fase 1 is daar de visuele voorbode van.

## Beslissingen

- 2026-10-22: online telt niet meer mee in de maand; enkel dag en maand als periodes (geen week/seizoen); online als tab op het klassementscherm; divisies later.
- 2026-10-22: top 3 groen met bekertje; onderste 3 rood vanaf 10 spelers; online uit de maand vanaf 1 oktober 2026 (dus ook de lopende maand; enkel september houdt de oude telling); online-maandbeker geschrapt; geen pijltjes bij afgelopen maanden. Fase 1 wordt gebouwd.
- 2026-10-22: eigen rij duidelijk gemarkeerd in Vandaag en Maand; in Maand pijltjes gestegen/gedaald tegenover gisteren; top 3 (of 5) groen met bekertje, onderste rijen rood met pijltje omlaag.

## Open vragen

_Allemaal beslist op 2026-10-22, zie Beslissingen._

- **Top 3 of top 5** in de groene zone? Voorstel: top 3, want dat zijn de spelers die effectief een beker krijgen (`MonthRules.PodiumBonus` en de maandbeker gaan ook tot 3).
- **Hoeveel rijen in de rode zone**, en vanaf hoeveel spelers? Voorstel: de onderste 3, pas vanaf 10 spelers in de maand.
- **Vanaf wanneer?** Vanaf 1 oktober 2026, dus ook de lopende maand; september houdt de oude telling. (Eerst was 1 november voorgesteld.) Toegekende Gouden Ballen liggen al vast (`GoldenBalls`), dus die schuiven niet. Schakeldatum in `MonthRules.OnlineCountsUntil`.
- **Online-maandbeker.** De README noemt een beker voor de top 3 van het online maandklassement (`TrophyKind.Online`), maar enkel Month- en Special-bekers worden toegekend. Voorstel: de online-maandbeker schrappen en online belonen via rating en badges.
- **Pijltjes bij vorige maanden:** bij bladeren naar een afgelopen maand geen pijltjes tonen (de stand is definitief). Akkoord?

## Aanpak (optioneel)

- **Fase 1 (M-L):**
  - Shared: `MonthRules` zonder online (+ schakeldatum), `MonthEntry` zonder online-velden maar met `PreviousRank` en `Zone` (of de zone-grenzen als regel in `MonthRules`: `PrizeZone = 3`, `DropZone = 3`, `DropZoneMinPlayers = 10`), `Explanation` aanpassen.
  - Server: `MonthRankingService` zonder online-berekening; de stand van gisteren meegeven. Eerst herberekenen (dezelfde query tot en met gisteren); blijkt dat te traag, dan een dagelijkse snapshot (tabel `MonthRankSnapshots`, migratie).
  - Client: `Klassement.razor` met drie tabs (Vandaag, Maand, Online); `LeaderboardList` met optionele kolommen verschuiving en zone en een sterkere `.me`-stijl; vastgepinde eigen rij; de Online-tab hergebruikt de ratinglijst van `Online.razor`.
  - README en `MonthRules.Explanation` bijwerken. Tests voor de maandtelling, de zones en de verschuiving.
- **Fase 2 (M):** uitklapbare rij en samenvatting. Ontwerp door `ux-designer`.
- `spelregel-reviewer` erover laten kijken (regels in Shared, server-autoriteit, tests).

## Notities

- 2026-10-22: eerste concept (vier periodes, verrijkte rijen) besproken en teruggebracht tot drie vaste klassementen, met een tv-stand-opmaak voor de maand.
- 2026-10-22: fase 1 gebouwd en lokaal nagekeken: `MonthRules` (schakeldatum, zones), `MonthRankingService` (stand van gisteren), endpoint `/api/leaderboard/online`, `Klassement.razor` met drie tabs, `LeaderboardList` met pijltjes, zones en vastgepinde eigen rij, online-maandbeker geschrapt (`TrophyKind.Online`), README. Fase 2 (uitklappen, samenvatting) staat nog open.
