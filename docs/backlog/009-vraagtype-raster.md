# 009 – Vraagtype "Raster" (3×3)

- **Soort:** feature
- **Status:** idee
- **Prioriteit:** midden
- **Moeite:** L
- **Aangemaakt:** 2026-10-22
- **Bron:** brainstormgesprek over nieuwe vraagtypes

## Waarom

Een open vraag met veel juiste antwoorden: kennis en creativiteit tellen, niet alleen herkennen. Met een bonus voor zeldzame antwoorden wordt het een sterke trekker in de online mode ("niemand anders dacht aan hem!").

## Wat

Een raster van 3×3. Boven de kolommen en naast de rijen staan clubs of landen (bv. Anderlecht, Club Brugge, Genk × Frankrijk, Nederland, België). Per vakje typt de speler een speler die bij beide past. Elke geldige speler telt; wie een speler kiest die weinig anderen kozen, krijgt extra punten.

## Open vragen

- Alleen club × club en club × land, of ook categorieën zoals "Rode Duivel", "speelde in de Premier League", "trainer geworden"?
- Mag je een speler maar één keer in het raster gebruiken?
- Zeldzaamheidsbonus: hoe binnen de daglimiet van 1000 punten? Pas na de dag berekend, of meteen?
- Hoeveel tijd voor 9 vakjes, en hoeveel pogingen per vakje?
- Hoe zorgen we dat elk vakje genoeg geldige spelers in de database heeft?

## Aanpak (optioneel)

- **Voorwaarde:** er is een clubgeschiedenis per speler nodig. `Player` heeft nu alleen `Country`, `Competition`, `Era` en tips. Nieuwe kolom (bv. `Clubs`) + migratie + aanvullen in `players/*.json` door `speler-redacteur`. Deelt die basis met [010](010-vraagtype-verbindingsman.md).
- Server: rasters genereren of handmatig samenstellen, antwoorden valideren via de spelersdatabase (`Answers` voor schrijfwijzen).
- Volg de skill `nieuw-vraagtype`; laten nakijken door `spelregel-reviewer` (punten, daglimiet).
