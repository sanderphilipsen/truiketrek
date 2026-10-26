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

Lopende planning: [planning-2026-10-23.md](planning-2026-10-23.md) (week 44 t/m 49, voorstel). De kolom "Planning" verwijst ernaar.

| # | Titel | Soort | Status | Prio | Moeite | Planning |
|---|---|---|---|---|---|---|
| [001](001-moeilijkheid-herberekenen.md) | Moeilijkheid automatisch herberekenen | techniek | concept | midden | L | niet nu |
| [002](002-herwerking-klassementen.md) | Klassementen fase 2: uitklappen en samenvatting | feature | fase 1 gedaan, fase 2 open | midden | M | terugval wk 48 |
| [003](003-themakenners.md) | Themakenners in de statistieken | feature | concept | midden | M | terugval wk 48 |
| [004](004-weergavethemas.md) | Weergavemodussen: de speler kiest een thema | feature | concept | midden | L | niet nu |
| [005](005-seizoenssfeer.md) | Seizoenssfeer op feestdagen en voetbalmomenten | ux | concept | midden | M | wk 46 (lichte versie) |
| [006](006-streaker.md) | Streaker die over het scherm loopt | feature | concept | laag | M | niet nu |
| [008](008-vraagtype-kaart.md) | Kaartdag: "Wie speelt hier?" | feature | concept | midden | M | niet nu |
| [009](009-vraagtype-raster.md) | Vraagtype "Raster" (3×3) | feature | concept | midden | L | niet nu |
| [010](010-vraagtype-verbindingsman.md) | Vraagtype "Verbindingsman" | feature | idee | laag | L | voorstel: bij 009 |
| [012](012-vraagtype-wie-zei-het.md) | Vraagtype "Wie zei het?" | feature | klaar | midden | M | inhoud wk 45-46, bouwen wk 47 |
| [013](013-daag-uit.md) | Daag uit (asynchroon duel) | feature | idee | midden | L | concept wk 45, bouwen wk 48-49 |
| [014](014-verzamelalbum.md) | Verzamelalbum (Panini-stijl) | feature | idee | midden | L | niet nu |
| [015](015-pushmeldingen.md) | Pushmeldingen | feature | bezig | midden | M | gebouwd, sleutels op Azure nog te zetten |
| [016](016-revanche-online-wedstrijd.md) | Revanche na een online wedstrijd | feature | idee | midden | S | wk 45 |
| [017](017-vraagtype-clubraadsel.md) | Vraagtype "Clubraadsel" | feature | concept | midden | M | na 012, of terugval |
| [018](018-lijstdag.md) | Lijstdag: "Noem ze allemaal" | feature | concept | midden | S | niet gepland |
| [019](019-matchverslag.md) | Matchverslag: een legendarische match invullen | feature | concept | midden | M | niet gepland |
| [020](020-toernooiboom.md) | Toernooiboom: de knock-outfase invullen | feature | concept | laag | M | niet gepland |
| [021](021-codekraker.md) | Codekraker: speurtocht naar één geheime naam | feature | concept | midden | M | niet gepland |
| [023](023-inzetdag.md) | Inzetdag: punten inzetten per vraag | feature | concept | midden | S | niet gepland |
| [024](024-puzzeldag.md) | Puzzeldag: 12 woorden, 3 verbanden | feature | concept | midden | M | niet gepland |
| [025](025-open-deur.md) | Open Deur: vier trefwoorden per vraag | feature | concept | midden | M | niet gepland |
| [026](026-vraagtype-hoger-of-lager.md) | Vraagtype "Hoger of lager" | feature | idee | midden | S | niet gepland |
| [027](027-vraagtype-truitje.md) | Vraagtype "Het Truitje" | feature | idee | midden | M | niet gepland |
| [028](028-vraagtype-buitenspel.md) | Vraagtype "Buitenspel?" | feature | idee | midden | S | niet gepland |
| [029](029-vraagtype-estafette.md) | Vraagtype "Estafette" | feature | idee | midden | M | niet gepland |
| [030](030-vraagtype-schat-het-getal.md) | Vraagtype "Schat het getal" | feature | idee | midden | S | niet gepland |
| [031](031-correctie-gespeelde-dag.md) | Correctie op een gespeelde dag: vraag schrappen of neutraliseren (admin) | feature | concept | midden | M | niet gepland |
| [032](032-vraag-betwisten.md) | Een vraag betwisten vanuit de daily | feature | concept | midden | M | samen met 031 |
| [033](033-divisies.md) | Divisies met promotie en degradatie | feature | concept | midden | L | niet gepland |
| [034](034-klassement-rond-mij.md) | Klassement: weergave "Rond mij" | ux | idee | laag | S | niet gepland |
| [035](035-vraagtype-spelersfiche.md) | Vraagtype "Spelersfiche" | feature | bezig | midden | M | gebouwd, inhoud in review |
| [036](036-schaakklok.md) | Schaakklok: om beurten een naam noemen (online) | feature | idee | midden | M | niet gepland |
| [037](037-academie-kalender.md) | Academie als maandkalender met onderblad en dagklassement | ux | bezig | midden | L | nu |

Recent gedaan: [002](002-herwerking-klassementen.md) fase 1 (`97fad5a`), [007](007-gemiste-daily-naspelen.md) Academie (`0e18e7b`), [011](011-vraagtype-op-volgorde.md) Op volgorde (`4803333`), [022](022-detectivedag.md) Detectivedag (`5471602`).
