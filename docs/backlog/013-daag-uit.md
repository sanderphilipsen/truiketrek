# 013 – Daag uit (asynchroon duel)

- **Soort:** feature
- **Status:** idee
- **Prioriteit:** midden
- **Moeite:** L
- **Aangemaakt:** 2026-10-22
- **Bron:** agent ideeen-brainstormer, gekozen door de gebruiker

## Waarom

Een online partijtje lukt alleen als iedereen tegelijk tijd heeft. Een duel lost dat op: je daagt een maat uit, die speelt wanneer het hem past, en toch vergelijk je exact dezelfde vragen. Het is ook een sterke reden om de app opnieuw te openen ("Jan heeft je uitgedaagd").

## Wat

1. De speler kiest **Daag uit** (op Home of vanuit het profiel van een andere speler) en speelt **5 vragen** uit de online pool (niet uit de daily van vandaag).
2. Na afloop krijgt hij een **link** om te delen (zelfde deelmechanisme als de tafels: WhatsApp of kopiëren).
3. De uitgedaagde opent de link, ziet "Jan daagt je uit: 5 vragen, hij haalde 410" en speelt **dezelfde 5 vragen** in dezelfde volgorde, met dezelfde timer.
4. Daarna zien beiden een **vergelijking per vraag**: wie juist had, wie sneller was, en de eindstand. Met een knop **Revanche** (de verliezer daagt terug uit, nieuwe 5 vragen).
5. Een duel vervalt na **24 of 48 uur** als de ander niet speelt.

## Open vragen

1. **Punten:** tellen niet voor de daglimiet van 1000. Alleen XP (bv. zoals een online wedstrijd volgens `MonthRules`), of helemaal niets en enkel eer?
2. **Wie mag je uitdagen?** Iedereen met de link (ook spelers zonder account?), of alleen bestaande gebruikers? Mag je meerdere mensen tegelijk uitdagen met dezelfde link (dan wordt het een mini-klassement)?
3. **Vraagtypes:** vaste mix van 5, of kiest de uitdager (bv. alleen Snelvuur)?
4. **Mag de uitdager de vragen zien vóór hij deelt?** Nee, want anders kan hij ze doorgeven. De uitdager speelt dus eerst en deelt pas daarna.
5. **Vervaltijd:** 24 uur, 48 uur, of geen?
6. **Badges:** "Duellist" (10 duels gewonnen)? Hoort een duelreeks bij de reeksen?
7. **Overzicht:** een lijst "Open uitdagingen" op Home met wat je nog moet spelen en wat op antwoord wacht?

## Aanpak (optioneel)

- **Data:** nieuwe tabellen `Duel` (uitdager, vragen, aangemaakt, vervalt) en `DuelRun` (gebruiker, punten per vraag, tijd per vraag). Niet in `Attempts`, om dezelfde reden als bij [007](007-gemiste-daily-naspelen.md): klassementen en statistieken blijven ongewijzigd.
- **Server:** hergebruik van de academiecode (`TrainingService`, `AttemptFlow`, `AttemptState`) voor een run op een vaste vragenreeks. Vragen komen uit de online pool. De server kiest de vragen en bewaart ze bij het duel; de client krijgt nooit antwoorden.
- **Client:** startscherm, speelflow (zelfde componenten als de daily, met label "Duel tegen Jan"), vergelijkingsscherm, deelknop via `TafelDelen.razor`.
- **Review:** `spelregel-reviewer` (punten, daglimiet, lekken van antwoorden); tests op vervaltijd en op "dezelfde vragen voor beiden".
- Eerst uitwerken tot `concept` met antwoorden op de open vragen, dan pas bouwen.

## Notities

- 2026-10-22: gekozen uit de brainstorm samen met [014](014-verzamelalbum.md), [015](015-pushmeldingen.md) en [016](016-revanche-online-wedstrijd.md).
