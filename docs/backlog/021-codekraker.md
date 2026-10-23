# 021 – Codekraker: een speurtocht naar één geheime naam

- **Soort:** feature
- **Status:** concept
- **Prioriteit:** midden
- **Moeite:** M
- **Aangemaakt:** 2026-10-23
- **Bron:** brainstorm over puzzels die een hele daily vullen (zoals [008](008-vraagtype-kaart.md) en [009](009-vraagtype-raster.md))

## Waarom

Dit is het formaat met het meeste "uitpakken"-gevoel: een speurtocht waarin elk antwoord een stukje van een groter raadsel onthult. Wie slim is, kan het eindantwoord vroeg raden en durft een gok te wagen. Dat geeft spanning en verhalen ("ik had hem na drie letters!").

## Wat

- Een **speciale daily** met **5 tot 8 vragen** (bestaande soorten: wie ben ik, snelvuur, emoji, carrièrepad...).
- Elk antwoord levert **één letter** op (bv. de beginletter). De letters vullen een **geheime naam** in, bv. `K _ M P A N Y`.
- De speler kan **op elk moment het eindantwoord raden**. Juist geraden: de overige vragen vervallen en hij krijgt een **bonus** voor elke vraag die hij oversloeg. Fout geraden: een kleine straf, en hij speelt verder.
- Bij een fout antwoord op een tussenvraag blijft de letter leeg; je moet de naam dan raden met een gat erin.
- Het eindantwoord is een speler, club, stadion of trainer, eventueel in het thema van de dag.

Voorbeeld:

| Vraag | Antwoord | Letter |
|---|---|---|
| Wie ben ik? | **K**evin De Bruyne | K |
| Carrièrepad (Genk, Lille, Liverpool) | **O**rigi | O |
| Snelvuur: wie scoorde het meest voor Napoli? | **M**ertens | M |
| Emoji: 🧤🇧🇪 (Gouden Schoen 1976) | **P**faff | P |
| Rugnummer | **A**lderweireld | A |
| Wie hoort er niet bij? | **N**ainggolan | N |
| Wie ben ik? | **Y**ouri Tielemans | Y |
| Geheime naam | **KOMPANY** | |

## Open vragen

- **Welke letter:** altijd de beginletter, of de letter op een aangeduide plaats (moeilijker, maar vrijer in het kiezen van vragen)?
- **Volgorde van de letters:** liggen de letters op volgorde, of door elkaar (een anagram, veel moeilijker)?
- **Punten binnen 1000:** hoeveel voor de tussenvragen, hoeveel voor het eindantwoord, en hoe groot de bonus per overgeslagen vraag?
- **Gokken:** hoeveel pogingen voor het eindantwoord, en hoe groot de straf voor een foute gok? Zonder straf gokt iedereen bij elke letter.
- **Tijd:** de gewone tijd per tussenvraag, plus een tijd voor het eindantwoord?
- **Samenstellen:** met de hand per dag in het admin-paneel (vragen kiezen die de juiste letters geven), of een hulpje dat bij een gekozen eindnaam passende vragen uit de vragenbank voorstelt?
- **Online en Academie:** doet de codekraker daar ook mee?

## Aanpak (optioneel)

- **Data:** een JSON-bestand of admin-record per codekraker: eindantwoord met aliassen, en per letter een verwijzing naar een bestaande vraag of speler (hergebruik van de vragenbank).
- **Shared:** nieuw `QuestionType` of een dagformaat bovenop bestaande types (bv. `Codekraker`), regels in `QuestionRules`. De dag via het mechanisme van specials.
- **Server-autoriteit:** de client krijgt alleen de lengte van de naam en de letters die al verdiend zijn, nooit het eindantwoord. De server controleert het eindantwoord met `AnswerMatcher` en rekent de bonus.
- **Admin:** een hulpje dat per letter vragen voorstelt waarvan het antwoord met die letter begint, zou het samenstellen veel makkelijker maken.
- Volg de skill `nieuw-vraagtype`; laten nakijken door `spelregel-reviewer` (bonus, daglimiet) en `spel-econoom` (straf en bonus voor gokken).

## Notities

- 2026-10-23: idee gekozen door de eigenaar uit een brainstorm (samen met [018](018-lijstdag.md), [019](019-matchverslag.md) en [020](020-toernooiboom.md)).
