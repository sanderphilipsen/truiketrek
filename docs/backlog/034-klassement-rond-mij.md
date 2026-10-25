# 034 – Klassement: weergave "Rond mij"

- **Soort:** ux
- **Status:** idee
- **Prioriteit:** laag
- **Moeite:** S
- **Aangemaakt:** 2026-10-25
- **Bron:** eigen idee (UX-ronde klassement)

## Waarom

In een lange lijst blijft je eigen rij wel in beeld plakken, maar je ziet niet tegen wie je strijdt. Om te stijgen wil je weten wie net boven en net onder je staat, en hoeveel punten het scheelt.

## Wat

- Een schakelaar **Alles / Rond mij** boven de lijst (Vandaag en Maand).
- "Rond mij" toont de top 3, dan een rij "…", dan je twee buren boven en onder je, en (bij Maand) de degradatiezone als je daar dichtbij staat.
- Alleen zinvol bij lange lijsten: de schakelaar verschijnt pas vanaf ongeveer 20 spelers.

## Open vragen

- Vanaf hoeveel spelers tonen we de schakelaar? Voorstel: 20.
- Onthouden we de keuze per speler (localStorage) of staat het altijd op "Alles"?
- Ook bij Online, of enkel Vandaag en Maand?

## Aanpak (optioneel)

Enkel Client: `LeaderboardList` krijgt een optie om rijen in te klappen tot een "…"-rij; `Klassement.razor` de schakelaar. Geen serverwerk, de volledige lijst komt al binnen.

## Notities

- 2026-10-25: op de backlog gezet samen met het uitklappen van een rij (zie [002](002-herwerking-klassementen.md), fase 2).
