# 035 – Vraagtype "Spelersfiche"

- **Soort:** feature
- **Status:** bezig
- **Prioriteit:** midden
- **Moeite:** M
- **Aangemaakt:** 2026-10-26
- **Bron:** brainstorm nieuwe vraagtypes (2026-10-26), idee "Cijferkaart" bijgestuurd

## Waarom

Een fiche zonder naam en foto, zoals op Transfermarkt of in een Panini-album, laat de speler redeneren met cijfers in plaats van met verhaaltjes: "1,94 m, links, 1987, centrale verdediger, 120 caps... dat moet Vertonghen zijn." Dat is een ander soort kennis dan de tips van "Wie ben ik?".

## Wat

- Een fiche zonder naam. De velden klappen één voor één open, van vaag naar verraderlijk.
- De speler typt op elk moment een naam. Fout kost niets, maar opent het volgende veld.
- Een gewoon type in de daily: 10 vragen × 100 punten, uit de spelersdatabase.

| # | Veld | Punten bij juist antwoord |
|---|---|---|
| 1 | Lengte · voorkeursvoet | 100 |
| 2 | Geboortejaar | 90 |
| 3 | Positie | 80 |
| 4 | Caps · interlandgoals | 70 |
| 5 | Nationaliteit | 60 |
| 6 | Laatste of huidige club | 50 |

- Wie het antwoord weet, krijgt minstens 50 punten. Pas na zes velden en nog fout (of de tijd om) is het 0.
- Spelers met 0 caps mogen meedoen.
- Caps en goals van actieve spelers krijgen een peildatum, klein op het fiche ("stand op 1/10/2026").

## Beslissingen

- Gewicht valt weg (bronnen spreken elkaar tegen, het verandert tijdens een carrière); voorkeursvoet komt in de plaats naast de lengte.
- De club komt als laatste veld, omdat die meestal alles verraadt.
- Overlap met Rugnummer (positie, land, club) is aanvaardbaar: daar draait het om het nummer, hier om de cijfers.

## Aanpak

- **Data:** in de vragenbank (`Data/questions/fiche.json`, `FicheData`), zoals Rugnummer: zo heeft elk fiche zijn eigen status, bronnen en peildatum, en is er geen migratie nodig.
- **Shared:** nieuw `QuestionType`, punten per veld en tijd in `QuestionRules`.
- **Server:** de server geeft een veld pas vrij als het vorige verlopen of fout beantwoord is.
- **Client:** een fichekaart waarvan de rijen openklappen, invoerveld onderaan, 375 px.
- Volg de skill `nieuw-vraagtype`.

## Notities

- 2026-10-26: concept afgestemd met de gebruiker; bouwen gestart.
- 2026-10-26: gebouwd. Velden openen elke 4 s (zoals Rugnummer) of na een foute naam; 30 s per vraag. Eerste 30 fiches in `fiche.json`, 13 goedgekeurd (bij de andere 17 staat de voorkeursvoet nog in geen bron).
- 2026-10-26: onderhoud. `asOf` = peildatum van interlands én club bij elke actieve speler. Veralgemeend naar alle vragen en spelers (kolom `AsOf`, migratie `Peildatum`): ouder dan `QuestionRules.MaxAgeDays` (120) = verouderd, speelt niet en staat als "verouderd" in het admin-paneel. Bijwerken met de agent `inhoud-onderhoud` na elke interland- en transferperiode, na het seizoen en na prijsuitreikingen.
- Open: de badge Allrounder telt nu 14 types in plaats van 13; wie hem had, verliest hem tot hij een fiche speelde.
