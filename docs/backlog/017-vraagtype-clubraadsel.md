# 017 – Vraagtype "Clubraadsel"

- **Soort:** feature
- **Status:** concept
- **Prioriteit:** midden
- **Moeite:** M
- **Aangemaakt:** 2026-10-22
- **Bron:** brainstormgesprek over nieuwe feature-ideeën

## Waarom

Alle vragen draaien nu rond spelers. Clubs zijn minstens even herkenbaar voor een supporter: bijnamen, kleuren, stadions, liedjes, oprichtingsjaren en fusies. Met dit type komt er een nieuw onderwerp bij, terwijl de bekende mechaniek van "Wie ben ik?" blijft (tips die steeds makkelijker worden en een naam die je intypt). Er zijn geen logo's of foto's nodig, dus er zijn ook geen problemen met beeldrechten.

## Wat

- De speler krijgt **5 tips** over een club, van moeilijk naar makkelijk, en **typt de clubnaam**. Het werkt zoals "Wie ben ik?", maar de tips zijn in de wij-vorm ("Wij speelden ooit in de Europacup II-finale").
- Soorten tips: oprichtingsjaar of stamnummer, stadion en capaciteit, bijnaam, clubkleuren, bekende oud-spelers, fusies en naamswijzigingen, titels, ligging ("Onze thuisbasis ligt aan de Schelde").
- Na het antwoord volgt een korte **clubkaart** met de kerngegevens (stad, stadion, stamnummer, kleuren).

### Voorstel spelregels

- **Tijd en punten:** zoals "Wie ben ik?": max 100 punten, minder per getoonde tip. Er komt geen nieuwe regel bij.
- **Antwoord:** intypen, met dezelfde fuzzy matching (`AnswerMatcher`) en **aliassen** ("Club Brugge", "Club", "Brugge KV"; "RSCA", "Anderlecht", "Sporting Anderlecht").
- **Server-autoriteit:** de client krijgt alleen de tips die al zichtbaar zijn, nooit de clubnaam of de aliassen.

### Data (`clubraadsel.json`)

```json
{
  "club": "KAA Gent",
  "aliases": ["Gent", "AA Gent", "De Buffalo's"],
  "tips": [
    "Ons stamnummer is 7.",
    "We speelden lang in het Jules Ottenstadion.",
    "In 2015 werden we voor het eerst landskampioen.",
    "Onze bijnaam verwijst naar een rondreizend circus.",
    "We spelen in het blauw-wit in de Planet Group Arena."
  ],
  "card": { "city": "Gent", "stadium": "Planet Group Arena", "colors": "blauw-wit", "founded": 1898 },
  "difficulty": 2,
  "status": "review",
  "sources": ["..."],
  "tags": ["pro-league"],
  "checkedAt": "2026-10-22"
}
```

## Open vragen

1. **Welke clubs?** Alleen Belgische (huidige 1A/1B plus verdwenen of gefuseerde clubs zoals Beerschot VAC, Lierse en RWDM), of ook grote buitenlandse clubs (Ajax, Barcelona, Liverpool)? Alleen Belgische clubs geven een eigen gezicht, maar de voorraad is beperkt (± 40–60 clubs).
2. **Fusieclubs en heroprichtingen:** telt "Beerschot" goed als het antwoord "Beerschot VAC" of "K. Beerschot VA" is? Een voorstel is één item per club zoals de fan die kent, met ruime aliassen.
3. **Nationale ploegen** ook als raadsel ("Wij werden in 1986 tweede op het WK…"), of enkel clubs?
4. **Herhaling:** met een beperkte voorraad komen clubs sneller terug dan spelers. Mogen er meerdere tipsets per club zijn (bv. een "historische" en een "recente" set)?
5. **Waar en hoeveel:** 10 vragen van 100 per dag, zoals "Wie ben ik?"? Een vaste dag in de weekplanning, of net als [012](012-vraagtype-wie-zei-het.md) alleen in specials, online en de mix?
6. **Clubkaart:** willen we die na het antwoord tonen (meer werk aan de inhoud, maar leerrijk), of is "juist/fout + clubnaam" genoeg?
7. **Badge(s):** bv. "Groundhopper" voor 25 juiste clubraadsels?

## Aanpak (optioneel)

- Shared: `QuestionType.Clubraadsel`, `QuestionTypeInfo` (icoon bv. 🏟️), regels en DTO's (tips; resultaat met clubkaart).
- Server: aparte bron (`clubraadsel.json`) en seeder; de antwoordverwerking en het tipritme kunnen van "Wie ben ik?" komen, maar met vragen in plaats van spelers (`FromPlayers: false`); aliassen in `AnswerMatcher`; admin-paneel.
- Client: hergebruik van de "Wie ben ik?"-component, met eigen tekst en de clubkaart.
- Inhoud: eerste batch van ± 40 Belgische clubs via `speler-redacteur` / skill `vraag-toevoegen`, met bronnen; controle door `inhoud-controleur` en `moeilijkheid-inschatter`.
- Volg de skill `nieuw-vraagtype`; migratie indien nodig (skill `ef-migratie`); tests; review door `spelregel-reviewer`.

## Notities

- 2026-10-22: gekozen uit de tweede brainstormronde ("clubraadsel is leuk").
