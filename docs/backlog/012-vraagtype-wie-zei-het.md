# 012 – Vraagtype "Wie zei het?"

- **Soort:** feature
- **Status:** klaar
- **Prioriteit:** midden
- **Moeite:** M
- **Aangemaakt:** 2026-10-22
- **Bron:** brainstormgesprek over nieuwe vraagtypes

## Waarom

Voetbal leeft van uitspraken: persconferenties, interviews na de match, legendarische oneliners. Die kennen fans vaak beter dan statistieken, en ze roepen herinneringen en emotie op. Het type heeft ook een eigen toon: grappiger en menselijker dan een feitenvraag.

## Wat

- De speler ziet een **citaat** in groot lettertype (in scorebordstijl, tussen aanhalingstekens) en kiest uit **4 personen** wie het zei: spelers, trainers, voorzitters, scheidsrechters of commentatoren.
- Na het antwoord volgt de **context**: wie, wanneer en waarom ("Marc Wilmots na de uitschakeling tegen Wales, EK 2016"), en de bron.

### Voorstel spelregels

- **Tijd:** 15 seconden, zoals Snelvuur.
- **Punten:** max 100, dezelfde telling als Snelvuur (dan voelt het vertrouwd en is er geen nieuwe regel nodig).
- **Optionele twist** om het te onderscheiden van Snelvuur: na 7 seconden verschijnt een **contexttip** (bv. "na een verloren bekerfinale"). Wie vóór de tip antwoordt, kan de volle punten halen; daarna maximaal 70. Zie open vragen.
- **Server-autoriteit:** zoals Snelvuur: de client krijgt het citaat en de opties, niet het juiste antwoord of de context.

### Data (`wie-zei-het.json`)

```json
{
  "quote": "…",
  "original": "…",
  "options": ["Marc Wilmots", "Roberto Martínez", "Georges Leekens", "Michel Preud'homme"],
  "correct": 0,
  "context": "Na de uitschakeling tegen Wales op het EK 2016.",
  "hint": "Na een verloren kwartfinale",
  "difficulty": 3,
  "status": "review",
  "sources": ["..."],
  "tags": ["rode-duivels"],
  "checkedAt": "2026-10-22"
}
```

`original` is optioneel: de oorspronkelijke tekst als het citaat vertaald is (Frans, Engels, ...).

## Open vragen

1. **Puur meerkeuze of met contexttip?** Gewoon 4 opties (eenvoudig, maar dan lijkt het sterk op Snelvuur), of de tip na 7 seconden met minder punten erna?
2. **Vertaling:** veel citaten zijn in het Frans of Engels. Altijd in het Nederlands tonen, met het origineel achteraf? Of de originele taal tonen (moeilijker, maar echter)?
3. **Wie mag erin?** Alleen spelers en trainers, of ook voorzitters, scheidsrechters, commentatoren en analisten? Alleen Belgisch voetbal, of ook internationale klassiekers (Cruijff, Mourinho, Klopp)?
4. **Omgekeerde variant** "Over wie ging het?" (een uitspraak óver een speler) als tweede soort binnen hetzelfde type?
5. **Gevoelige citaten:** citaten die beledigend of controversieel zijn (racisme, ruzies) weren, ook als ze bekend zijn?
6. **Hoeveel per dag en waar:** 10 van 100? En net als bij [011](011-vraagtype-op-volgorde.md): alleen specials, online en mix, of een vaste dag in de week?
7. **Inhoud:** hoeveel citaten zijn er nodig voor de start (± 60 zou 6 dagen vullen)? Dit is het grootste risico: citaten worden vaak verkeerd toegeschreven of verkort. Elk citaat heeft daarom een primaire bron nodig (interview, krantenartikel, video) en moet door `inhoud-controleur`.

## Aanpak (optioneel)

- Shared: `QuestionType.WieZeiHet`, `QuestionTypeInfo`, tijd in `QuestionRules`, DTO's (citaat, opties; resultaat met context en eventueel origineel).
- Server: seeder en picker (kan grotendeels de Snelvuur-logica hergebruiken), antwoordverwerking, admin-paneel met velden voor context en origineel.
- Client: vraagcomponent die de Snelvuur-opties hergebruikt, met een eigen citaatblok en contextscherm.
- Inhoud: eerste batch via `speler-redacteur` / skill `vraag-toevoegen`, strikt met bronnen; controle door `inhoud-controleur`.
- Volg de skill `nieuw-vraagtype`; tests; review door `spelregel-reviewer`.

## Notities

- 2026-10-22: gekozen uit de brainstorm samen met [011](011-vraagtype-op-volgorde.md).
- 2026-10-22: voorstellen goedgekeurd. Beslissingen:
  - 4 opties, 15 seconden, punten zoals Snelvuur, met een contexttip na 7 seconden (daarna maximaal 70);
  - citaten in het Nederlands, het origineel en de context na het antwoord;
  - 10 vragen van 100 per dag; niet in de vaste weekplanning, wel in specials, online en de mix;
  - elk citaat met een primaire bron, gecontroleerd door `inhoud-controleur`.
- Auteursrecht: korte citaten van publieke uitspraken met bronvermelding; geen lange interviewfragmenten overnemen.
