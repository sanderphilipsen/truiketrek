# 027 – Vraagtype "Het Truitje"

- **Soort:** feature
- **Status:** idee
- **Prioriteit:** midden
- **Moeite:** M
- **Aangemaakt:** 2026-10-23
- **Bron:** brainstorm nieuwe vraagtypes (2026-10-23)

## Waarom

De app heet Truitje Trek en heeft nog geen vraag over truitjes. Een getekend shirt is herkenbaar, voelt nostalgisch (de sponsor van toen!) en Mannetje bewijst dat een stapsgewijze onthulling van een tekening goed werkt. Geen foto's nodig, dus geen rechtenprobleem.

## Wat

- Een **shirt getekend in SVG**: eerst alleen de kleuren en het patroon (strepen, banen, kraag), dan stap voor stap de **sponsor**, de **fabrikant** en het **seizoen**.
- De speler **typt de club** (of bij nationale shirts het land). Hoe vroeger, hoe meer punten, zoals bij Carrièrepad en Mannetje.
- Variant voor later: een shirt met rugnummer en vraag naar de speler.

## Open vragen

- Hoeveel shirtvormen hebben we nodig om de meeste iconische shirts te tekenen (effen, verticale strepen, horizontale banen, helften, diagonaal, kraagtypes)?
- Mogen sponsornamen en fabrikantlogo's getoond worden als tekst, of alleen als gestileerd kader?
- Per club één shirt, of meerdere seizoenen zodat de vraag vaker terugkomt?
- Werkt het voor kleurenblinde spelers (patroon en kraag als extra onderscheid)?

## Aanpak (optioneel)

- **Data:** JSON per shirt: club, seizoen, kleuren, patroon, sponsor, fabrikant, aliassen van de club, bron.
- **Shared:** nieuw `QuestionType` met de bestaande reveal-logica (`RevealSteps`, `RevealPoints`).
- **Client:** één SVG-component die een shirt tekent uit de data; de stappen onthullen lagen.
- **Server:** de clubnaam blijft op de server tot het antwoord; controle via `AnswerMatcher` met aliassen.
- Volg de skill `nieuw-vraagtype`; `ux-designer` voor de shirt-component.

## Notities

- 2026-10-23: toegevoegd na de brainstorm, zie ook [026](026-vraagtype-hoger-of-lager.md).
