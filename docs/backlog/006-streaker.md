# 006 – Streaker die over het scherm loopt

- **Soort:** feature
- **Status:** concept
- **Prioriteit:** laag
- **Moeite:** M
- **Aangemaakt:** 2026-10-20
- **Bron:** eigen idee

## Waarom

Een streaker op het veld is een klassiek, grappig voetbalmoment dat iedereen herkent. Laat je er af en toe onverwacht eentje over het scherm lopen, dan krijg je een verrassing waar spelers om lachen en over praten ("had jij vandaag ook de streaker?"). Het maakt de app speelser zonder het spel te verstoren.

## Wat

Af en toe, onvoorspelbaar, sprint er een cartoonstreaker over het scherm: bv. van links naar rechts over het veld, met een steward die erachteraan holt. Mogelijke momenten:

- ergens in de app (Home, klassement, statistieken);
- tijdens de daily, **tussen twee vragen** (op het tussenscherm na een antwoord), nooit tijdens een vraag waarop de tijd loopt;
- eventueel in een special of online match.

Hij is **gecensureerd en familievriendelijk**: een cartoonfiguurtje met een balk, sjaal of voetbal op de juiste plek, of een figuur in een rood-zwart-geel lijfje met "STREAKER". Geen realistisch naakt.

## Open vragen

- **Hoe vaak?** Kans per sessie of per daily (bv. 1 op 20 dailies), maximaal één keer per dag? Hoe zeldzamer, hoe leuker.
- **Alleen kijken of ook iets doen?** Mag je op de streaker tikken? Bv. om hem te "tackelen" met de steward, voor een badge ("Streaker gevangen") of XP?
- **Punten?** Als het iets oplevert, moet de server bepalen wanneer hij verschijnt en of de tik telt (server-autoriteit). De daglimiet van 1000 punten blijft gelden. Of houden we het bewust buiten de puntentelling (enkel een badge of niets)?
- **Tijdens de daily:** stoort het niet? Enkel op het tussenscherm, en de volgende vraag start pas als hij voorbij is (de tijd mag niet lopen terwijl hij in beeld is)?
- **Varianten:** verschillende streakers (in clubkleuren, met een vlag tijdens een WK/EK, een kerststreaker met muts), te combineren met de seizoenssfeer uit [005](005-seizoenssfeer.md)?
- **Geluid:** een fluitsignaal of gejoel van het publiek, of stil (de app heeft nu geen geluid)?
- **Toegankelijkheid:** bij "minder beweging" (`prefers-reduced-motion`) niet tonen of als stil plaatje? Kan je hem uitzetten?
- **Smaak:** past het bij de toon van de app voor alle leeftijden? Hoe gecensureerd?

## Aanpak (optioneel)

- **Client:** een herbruikbare `Streaker`-component (SVG + CSS-animatie) die bovenop de layout over het scherm loopt, zonder klikken te blokkeren (`pointer-events: none`, tenzij hij aantikbaar is). Uitwerken met `animatie-bouwer`.
- **Trigger:** willekeurig op de client als het alleen voor de sfeer is. Komt er een beloning aan te pas, dan beslist de server (bv. een vlag in de daily-respons voor de vraag na X).
- **Daily-flow:** tonen op het tussenscherm in `Daily.razor`; controleren dat de vraagtimer pas start na de animatie.
- **Badges/tests:** eventueel een badge "Streaker gespot/gevangen" met tests. Laten nakijken door `spelregel-reviewer` als het punten of tijd raakt.

## Notities

