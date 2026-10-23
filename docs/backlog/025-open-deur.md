# 025 – Open Deur: vier trefwoorden per vraag

- **Soort:** feature
- **Status:** concept
- **Prioriteit:** midden
- **Moeite:** M
- **Aangemaakt:** 2026-10-23
- **Bron:** tweede brainstorm over puzzels die een hele daily vullen, naar de ronde Open Deur uit De Slimste Mens ter wereld

## Waarom

Een vraag met **meer dan één juist antwoord** waarbij je zelf moet bedenken wat belangrijk is: "Wat weet je over Enzo Scifo?". Dat is geen herkenning maar echte kennis ophalen. Elk gevonden trefwoord telt, dus wie iets weet, gaat nooit met lege handen naar huis. Het formaat is voor een Vlaming meteen herkenbaar.

## Wat

- Een **speciale daily** met **3 open vragen** ("deuren"). De speler kiest zelf in welke volgorde hij ze opent.
- Elke vraag is open, bv. **"Wat weet je over Enzo Scifo?"** of **"Wat weet je over het EK 2000?"**.
- Achter elke vraag zitten **4 trefwoorden**. Bij Scifo bv. *Anderlecht*, *Italiaanse ouders*, *Monaco*, *nummer 10*.
- De speler typt trefwoorden in, in willekeurige volgorde. Elk gevonden trefwoord **licht op**; een fout kost alleen tijd.
- Eén klok per vraag. Na de tijd worden de ontbrekende trefwoorden getoond.
- Na de dag: per trefwoord hoeveel % het vond.

## Open vragen

- **Formuleringen:** het moeilijkste punt. "Italiaanse ouders", "Italiaanse roots", "Italië" moeten allemaal goed zijn voor hetzelfde trefwoord. Een ruime lijst aliassen per trefwoord, of trefwoorden zo kiezen dat ze kort en eenduidig zijn (clubs, jaartallen, plaatsen)?
- **Antwoorden die niet in de lijst staan maar wel kloppen** ("Bordeaux" bij Scifo): fout, of een ⏳-review zoals bij het [Raster](009-vraagtype-raster.md)?
- **Punten binnen 1000:** 12 trefwoorden per dag, dus ± 80 per trefwoord, of een bonus voor een volledige deur?
- **Tijd:** vast per vraag (bv. 45 seconden), of zoals in het programma: de tijd die je overhoudt gaat mee naar de volgende deur?
- **Volgorde kiezen:** heeft dat zin als er geen tijd meegaat? Anders gewoon vaste volgorde.
- **Overlap met [018 Lijstdag](018-lijstdag.md):** daar is de lijst gesloten (namen); hier zijn het trefwoorden (feiten). Apart houden, of als één type met twee soorten inhoud bouwen?
- **Hoe vaak, online en Academie:** vaste dag, special, of via het admin-paneel?

## Aanpak (optioneel)

- **Data:** een JSON-bestand per open-deur-dag: 3 vragen, elk met 4 trefwoorden, aliassen per trefwoord en een bron. Te schrijven door `speler-redacteur`, na te kijken door `inhoud-controleur`.
- **Shared:** nieuw `QuestionType` (bv. `OpenDeur`), regels in `QuestionRules`. De dag via het mechanisme van specials.
- **Server-autoriteit:** de client krijgt de vraag en het aantal trefwoorden, nooit de trefwoorden zelf tot de tijd op is. De server controleert elke invoer met `AnswerMatcher` en rekent de punten.
- **Client:** 4 lege plaatsen die oplichten bij een treffer, een invoerveld boven het toetsenbord op 375 px. Kan hetzelfde onderdeel gebruiken als de Lijstdag.
- Volg de skill `nieuw-vraagtype`; laten nakijken door `spelregel-reviewer`.

## Notities

- 2026-10-23: op de backlog gezet op vraag van de eigenaar, samen met [024 Puzzeldag](024-puzzeldag.md).
