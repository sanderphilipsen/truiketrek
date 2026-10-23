# 029 – Vraagtype "Estafette"

- **Soort:** feature
- **Status:** idee
- **Prioriteit:** midden
- **Moeite:** M
- **Aangemaakt:** 2026-10-23
- **Bron:** brainstorm nieuwe vraagtypes (2026-10-23)

## Waarom

Een ketting waarbij elk antwoord de volgende vraag wordt, geeft een verhaal in plaats van losse feiten: je loopt door de geschiedenis van de bondscoaches, de aanvoerders of de Gouden Schoen. Wie een schakel mist, valt niet uit maar krijgt de schakel te zien en speelt verder. Dat voelt eerlijk en houdt de spanning tot het einde.

## Wat

- Eén opdracht, bv. **"Van Advocaat tot vandaag: de bondscoaches van België."**
- De eerste naam staat er; de speler **typt de opvolger**. Die naam wordt de volgende vraag, enzovoort, **vijf schakels** lang.
- Punten per schakel. Bij een fout of na de tijd verschijnt de juiste naam en gaat de ketting verder.
- Thema's: bondscoaches, aanvoerders van de Rode Duivels, trainers van een club, Gouden Schoen-winnaars, nummer 10 van een club, recordtransfers van een club.

## Open vragen

- Vast vijf schakels, of variabel per ketting (en dan punten naar rato)?
- Tijd per schakel of één klok voor de hele ketting?
- Bij een gemiste schakel: enkel de naam tonen, of ook één tip zodat de speler nog kan scoren?
- Is dit een gewoon type tussen de daily-vragen, of eerder een speciale dag met twee of drie kettingen?

## Aanpak (optioneel)

- **Data:** JSON per ketting: titel, geordende namen met aliassen, periode per schakel, bron.
- **Shared:** nieuw `QuestionType`, aantal schakels en punten in `QuestionRules`.
- **Server:** de server onthult een schakel pas als de vorige beantwoord of verlopen is; de client krijgt de ketting nooit vooraf.
- **Client:** verticale tijdlijn die van boven naar beneden invult, invoerveld dat boven het toetsenbord blijft op 375 px.
- Volg de skill `nieuw-vraagtype`.

## Notities

- 2026-10-23: toegevoegd na de brainstorm, zie ook [026](026-vraagtype-hoger-of-lager.md).
