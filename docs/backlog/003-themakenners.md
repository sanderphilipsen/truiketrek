# 003 – Themakenners in de statistieken

- **Soort:** feature
- **Status:** concept
- **Prioriteit:** midden
- **Moeite:** M
- **Aangemaakt:** 2026-10-20
- **Bron:** eigen idee

## Waarom

De statistieken tonen nu vooral algemene cijfers: jouw cijfers, de Eregalerij, de Gouden Ballen en de moeilijkste spelers. Wie het meest weet van één onderwerp, zoals de Premier League, de Rode Duivels of de Champions League, zie je nergens. Een titel als "Premier League-kenner" geeft spelers iets om naar te streven buiten het totaalklassement en zorgt voor gespreksstof ("jij kent niks van de Serie A").

## Wat

- Op het scherm Statistieken komt een sectie **Themakenners**: per thema de speler met de meeste juiste antwoorden op vragen en spelers met die tag, bv. *Premier League-kenner: Jan (87 juist)*.
- Een thema is gekoppeld aan een tag die al op spelers en vragen staat (`Tags`), bv. `rode-duivels`, `champions-league`, `wk`, `anderlecht` of `messi`.
- Tik je op een thema, dan zie je eventueel de top 5 en je eigen plaats.

## Open vragen

- **Welke thema's?** Een vaste lijst die de admin samenstelt (naam, icoon, tag of tags), of automatisch elke tag met genoeg inhoud? Een thema kan meerdere tags bundelen (bv. "Premier League" = clubtags uit Engeland).
- **Premier League bestaat nog niet als tag.** Er zijn wel `engeland`-spelers, maar geen tag `premier-league`. Die moet er dan bij, en bestaande inhoud moet getagd worden (skill `inhoud-voorraad` / `vraag-toevoegen`).
- **Meest juist of best percentage?** "Meeste juiste antwoorden" beloont wie veel speelt. Een percentage met een minimum aantal vragen (bv. 20) is eerlijker. Of een mix?
- **Periode:** all-time, per maand (zoals het maandklassement), of beide? Kan je de titel kwijtraken?
- **Welke antwoorden tellen?** Daily, specials en/of online matchen (`Attempt` en `MatchAnswer`)?
- **Beloning:** enkel vermelding in de statistieken, of ook een badge of titel naast je naam in klassementen en op je profiel?
- **Zichtbaarheid voor jezelf:** "Jij bent 3e in Rode Duivels". Toon je ook je eigen sterkste thema's op je profiel?

## Aanpak (optioneel)

- **Shared:** themadefinitie (naam, icoon, tags) en de regels (minimum aantal vragen, percentage of aantal) in Shared, met DTO's voor de sectie.
- **Server:** aggregatie van juiste antwoorden per gebruiker per tag uit `Attempt` (via `DailyQuestion` → vraag of speler → tags) en eventueel `MatchAnswer`. Dit gebeurt in de service achter Statistieken, met caching want de berekening is zwaar.
- **Client:** nieuwe sectie in `Statistieken.razor`, mogelijk hergebruik van `HallOfFame`- of `LeaderboardList`-kaartjes.
- **Inhoud:** ontbrekende tags toevoegen (zoals `premier-league`).
- **Badges:** eventueel een kennerbadge per thema.
- Verwant aan [002 – Herwerking van de klassementen](002-herwerking-klassementen.md) (klassement per thema).

## Notities

