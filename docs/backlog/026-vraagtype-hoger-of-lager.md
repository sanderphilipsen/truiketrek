# 026 – Vraagtype "Hoger of lager"

- **Soort:** feature
- **Status:** idee
- **Prioriteit:** midden
- **Moeite:** S
- **Aangemaakt:** 2026-10-23
- **Bron:** brainstorm nieuwe vraagtypes (2026-10-23)

## Waarom

Een duel-mechaniek die de app nog niet heeft: niet "weet je het?", maar "wie van de twee?". De vraag is in één oogopslag te begrijpen, een gok is altijd mogelijk en toch beloont het echte kennis. De data is klein (één getal per speler per categorie) en het "nog één" van een reeks maakt het verslavend. Verschilt van Snelvuur (vier opties) en van Op volgorde (vier kaartjes tegelijk).

## Wat

- Twee spelers naast elkaar en één vraag: **wie heeft er meer caps, meer goals voor België, de duurste transfer, meer landstitels?**
- De speler tikt op de winnaar. Die **blijft staan** en krijgt een nieuwe uitdager; de categorie kan per duel wisselen.
- **Tien duels** na elkaar binnen één vraag. Punten per juist duel, met een bonus voor een foutloze reeks.
- Na elk duel verschijnen de echte getallen kort in beeld (daar leer je van).

## Open vragen

- Is het één vraag van 100 punten met tien duels, of tien losse vragen van 10 punten?
- Wat bij **gelijkstand** (allebei 2 titels)? Niet opnemen, of allebei goed rekenen?
- Welke categorieën starten we mee, en waar komen de getallen vandaan (bestaande spelersdata, handmatig, Wikidata)? Getallen van actieve spelers verouderen.
- Snelheid laten meetellen zoals bij Snelvuur, of enkel juist/fout?

## Aanpak (optioneel)

- **Data:** per speler een paar getallen (caps, interlandgoals, duurste transfer, titels) in de spelersdatabase of een apart JSON-bestand.
- **Shared:** nieuw `QuestionType`, aantal duels en punten per duel in `QuestionRules`.
- **Server:** de server kiest de paren en controleert elke tik; de client krijgt de getallen pas na het antwoord.
- **Client:** twee grote kaarten, één tik, korte animatie waarbij de winnaar naar links schuift.
- Volg de skill `nieuw-vraagtype`.

## Notities

- 2026-10-23: toegevoegd na de brainstorm, samen met [027](027-vraagtype-truitje.md) t/m [030](030-vraagtype-schat-het-getal.md).
