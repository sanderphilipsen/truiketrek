# 014 – Verzamelalbum (Panini-stijl)

- **Soort:** feature
- **Status:** idee
- **Prioriteit:** midden
- **Moeite:** L
- **Aangemaakt:** 2026-10-22
- **Bron:** agent ideeen-brainstormer, gekozen door de gebruiker

## Waarom

De app denkt nu vooral per dag en per maand. Een album geeft een **lange termijn**: elke voetballer die je ooit juist raadde, wordt een sticker, en een half gevulde pagina roept om de ontbrekende. Dat speelt op nostalgie (het stickerboek van vroeger) en verzameldrang, en het maakt oude inhoud opnieuw waardevol.

## Wat

1. Een pagina **Album** (tegel op Home of via het profiel) met **pagina's per thema**: club, land of era (Rode Duivels, legendes, Anderlecht, Club Brugge, ...), afgeleid van de tags op de spelers.
2. Elke speler die je juist raadde in "Wie ben ik?" (en eventueel Kluts en Counter) is een **sticker**: de SVG-avatar met naam, club en land. Niet geraden = een grijs silhouet met een vraagteken.
3. **Glimmende sticker** als je hem raadde bij tip 1. Een gewone sticker wordt glimmend als je het later alsnog in één tip doet (bv. in training of online).
4. Een **volle pagina** geeft een badge ("Rode Duivels compleet") en een kleine animatie.
5. Bovenaan: "Je hebt 87 van 387 stickers. Nog 3 voor de pagina Legendes."

## Open vragen

1. **Welke vraagtypes tellen?** Alleen "Wie ben ik?" (de speler is daar de kern), of ook Kluts en Counter? En training en online, of alleen de echte daily?
2. **Nog niet gespeelde spelers:** zie je hoeveel stickers een pagina heeft (en dus hoeveel spelers de database kent)? Dat verklapt niets over de vragen zelf, maar wel dat een speler bestaat.
3. **Pagina-indeling:** uit de tags of uit `Country`/`Competition`/`Era`? Een speler op meerdere pagina's (Hazard bij Rode Duivels én bij legendes)?
4. **Ruilen:** een "dubbele sticker" bestaat niet. Toch een sociaal element, bv. zien welke stickers een vriend heeft en jij niet?
5. **Beloning:** alleen badges, of ook XP per volle pagina? Geen punten, de daglimiet blijft intact.
6. **Nieuwe spelers in de database:** een pagina die compleet was, wordt weer onvolledig. Aanvaarden, of een "compleet op datum" vastleggen?

## Aanpak (optioneel)

- **Data:** de basis is af te leiden uit `Attempts` plus de spelers en hun tags (geen migratie). Voor de glimmende sticker is de tipstap nodig; die zit al in de poging. Een aparte tabel pas als afleiden te traag wordt.
- **Server:** `AlbumService` dat per gebruiker de stickers en pagina's berekent, met caching. Badges in `ProgressService`.
- **Client:** albumpagina met raster van stickers, hergebruik van `Avatar.razor` en `Avatars.cs`; pagina's als tabs of als bladerbare "dubbele pagina". Animatie bij een volle pagina via `animatie-bouwer`.
- **Verband:** de pagina's volgen dezelfde thema-tags als [003](003-themakenners.md); stem de indeling af.

## Notities

- 2026-10-22: gekozen uit de brainstorm samen met [013](013-daag-uit.md), [015](015-pushmeldingen.md) en [016](016-revanche-online-wedstrijd.md).
- Stickers zijn de eigen SVG-avatars, geen foto's: geen rechtenprobleem.
