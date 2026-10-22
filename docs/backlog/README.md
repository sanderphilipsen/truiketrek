# Backlog

Mogelijke features en werk voor Truitje Trek. Elk item is een eigen bestand in deze map, zodat je een idee kan uitwerken, bespreken en afvinken zonder één grote lijst te moeten bijhouden.

## Hoe het werkt

- **Nieuw item:** kopieer [_sjabloon.md](_sjabloon.md) naar `NNN-korte-naam.md` (volgend vrij nummer, kleine letters, koppeltekens), bv. `007-weekklassement.md`. Vul minstens titel, soort, status en "Waarom" in; de rest mag later.
- **Status** staat bovenaan in het bestand en volgt deze stappen:

  | Status | Betekenis |
  |---|---|
  | `idee` | Losse gedachte, nog niet bekeken |
  | `concept` | Uitgewerkt: wat, waarom, open vragen. Klaar om af te stemmen |
  | `klaar` | Afgestemd en duidelijk genoeg om te bouwen |
  | `bezig` | Er wordt aan gewerkt |
  | `gedaan` | Gebouwd en gecommit (vermeld de commit) |
  | `geschrapt` | Niet doen (zet erbij waarom, dan komt het niet terug) |

- **Soort:** `feature` (nieuw voor de speler), `inhoud` (spelers, vragen, specials), `ux` (bestaand scherm beter), `techniek` (refactor, performantie, tooling), `bug`.
- **Prioriteit:** `hoog`, `midden` of `laag`. **Moeite:** `S` (uurtje), `M` (dagdeel), `L` (meerdere dagen).
- Gedane en geschrapte items blijven staan; verplaats ze naar [archief/](archief/) als de map te vol wordt.
- Grote features krijgen eerst status `concept` met open vragen, en worden pas gebouwd na afstemming.

## Overzicht

Houd deze tabel kort bij (enkel open items). Bij twijfel is de status in het bestand zelf de waarheid.

| # | Titel | Soort | Status | Prio | Moeite |
|---|---|---|---|---|---|
| [001](001-moeilijkheid-herberekenen.md) | Moeilijkheid automatisch herberekenen | techniek | concept | midden | L |
| [002](002-herwerking-klassementen.md) | Herwerking van de klassementen | feature | idee | midden | L |
| [003](003-themakenners.md) | Themakenners in de statistieken | feature | concept | midden | M |
| [004](004-weergavethemas.md) | Weergavemodussen: de speler kiest een thema | feature | concept | midden | L |
| [005](005-seizoenssfeer.md) | Seizoenssfeer op feestdagen en voetbalmomenten | ux | concept | midden | M |
| [006](006-streaker.md) | Streaker die over het scherm loopt | feature | concept | laag | M |
| [007](007-gemiste-daily-naspelen.md) | Academie: oude dailies en specials terugspelen | feature | bezig | midden | M |
| [008](008-vraagtype-kaart.md) | Vraagtype "Kaart van België" | feature | idee | laag | L |
| [009](009-vraagtype-raster.md) | Vraagtype "Raster" (3×3) | feature | idee | midden | L |
| [010](010-vraagtype-verbindingsman.md) | Vraagtype "Verbindingsman" | feature | idee | laag | L |
| [011](011-vraagtype-op-volgorde.md) | Vraagtype "Op volgorde" | feature | bezig | midden | M |
| [012](012-vraagtype-wie-zei-het.md) | Vraagtype "Wie zei het?" | feature | klaar | midden | M |
| [013](013-daag-uit.md) | Daag uit (asynchroon duel) | feature | idee | midden | L |
| [014](014-verzamelalbum.md) | Verzamelalbum (Panini-stijl) | feature | idee | midden | L |
| [015](015-pushmeldingen.md) | Pushmeldingen | feature | idee | laag | L |
| [016](016-revanche-online-wedstrijd.md) | Revanche na een online wedstrijd | feature | idee | midden | S |
| [017](017-vraagtype-clubraadsel.md) | Vraagtype "Clubraadsel" | feature | concept | midden | M |
