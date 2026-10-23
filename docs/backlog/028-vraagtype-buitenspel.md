# 028 – Vraagtype "Buitenspel?"

- **Soort:** feature
- **Status:** idee
- **Prioriteit:** midden
- **Moeite:** S
- **Aangemaakt:** 2026-10-23
- **Bron:** brainstorm nieuwe vraagtypes (2026-10-23)

## Waarom

Een snelle opwarmer met een eigen gebaar: swipen. Tien stellingen in een halve minuut, zonder typen en zonder vier opties lezen. Het leent zich ook voor spelregels ("mag een keeper een terugspeelbal na een inworp oppakken?"), een hoek die de app nog niet aanraakt. Snel te bouwen en makkelijk van inhoud te voorzien.

## Wat

- **Tien stellingen** na elkaar, bv. "Vincent Kompany scoorde op het WK 2018."
- De speler **swipet naar rechts voor "Goal" (waar)** of **naar links voor "Buitenspel" (niet waar)**; tikken op twee knoppen kan ook.
- Snelheid telt, maar een **fout kost punten** zodat blind gokken niet loont.
- Na elke stelling één regel uitleg ("Nee, dat was Vertonghen tegen Japan").

## Open vragen

- Puntentelling: 10 punten per stelling met aftrek bij fout, of enkel de juiste tellen met een bonus voor tien op tien?
- Hoeveel seconden per stelling (5? 8?) en loopt één klok voor alle tien?
- Mengen we spelregels en feiten, of is "spelregels" een aparte tag voor een special?
- Werkt swipen goed genoeg in Blazor op een gsm, of houden we het bij twee grote knoppen met een swipe als extra?

## Aanpak (optioneel)

- **Data:** JSON met stelling, waar/niet waar, uitleg, tag, bron. Makkelijk aan te vullen door `speler-redacteur`.
- **Shared:** nieuw `QuestionType`, seconden en aftrek in `QuestionRules`.
- **Server:** elke swipe is een antwoord dat de server scoort; de uitleg komt pas daarna naar de client.
- **Client:** kaartenstapel, swipe-animatie door `animatie-bouwer`, respect voor "minder beweging".
- Volg de skill `nieuw-vraagtype`; `spelregel-reviewer` voor de aftrek-regel.

## Notities

- 2026-10-23: toegevoegd na de brainstorm, zie ook [026](026-vraagtype-hoger-of-lager.md).
