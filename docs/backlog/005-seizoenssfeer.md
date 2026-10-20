# 005 – Seizoenssfeer: kleine aanpassingen op feestdagen en voetbalmomenten

- **Soort:** ux
- **Status:** concept
- **Prioriteit:** midden
- **Moeite:** M
- **Aangemaakt:** 2026-10-20
- **Bron:** eigen idee

## Waarom

De app ziet er elke dag hetzelfde uit, ook op Halloween, met Kerstmis of op een Champions League-avond. Kleine sfeeraccenten laten de app leven en mee bewegen met de kalender en het voetbalseizoen. Spelers krijgen zo een "o, leuk"-moment bij het openen, zonder dat ze iets moeten doen.

## Wat

Op bepaalde dagen of periodes krijgt de app automatisch een subtiel andere sfeer. Het gaat om **kleine aanpassingen**, geen volledig ander design:

- een accentkleur of een rand in de sfeer van het moment;
- een klein decoratief element, zoals een pompoen bij het logo, sneeuw over het veld of slingers;
- een aangepaste begroeting of melding op Home;
- eventueel een licht effect, zoals vallende sneeuw of confetti bij nieuwjaar, met respect voor "minder beweging".

Mogelijke momenten:

| Soort | Momenten |
|---|---|
| Feestdagen | Halloween, Sinterklaas, Kerst, Nieuwjaar, Driekoningen, Carnaval, (Pasen, Valentijn?) |
| Voetbalkalender | Transfer Deadline Day (zomer en winter), Champions League-speeldagen, interlandperiodes |
| Toernooien | WK, EK (in de kleuren van het gastland of met toernooibranding zonder logo's) |
| Seizoenen / weer | Zomer, sneeuw/winter, eventueel herfst |

## Open vragen

- **Hoe klein is klein?** Enkel accenten (kleur, icoon, begroeting), of ook een seizoenstextuur voor het veld, zoals sneeuw op het gras of herfstbladeren?
- **Kalender:** vaste datums zijn eenvoudig (Halloween, Kerst). Carnaval en Pasen hangen af van de paasdatum. CL-speeldagen, interlandperiodes en WK/EK wisselen per jaar. Zetten we die in een vaste lijst in de code, of laten we de admin ze per seizoen beheren?
- **Duur:** enkel op de dag zelf of een periode errond (bv. 1-26 december voor Kerst, de hele duur van een WK)? Wat als twee momenten overlappen (Sinterklaas tijdens een CL-week)?
- **Koppeling met specials:** veel van deze momenten hebben al een special (`SpecialTemplates`: halloween, sinterklaas, kerst, nieuwjaar, deadline-day, champions-league, wk). Volgt de sfeer automatisch de actieve special, of staat ze er los van?
- **Sneeuw en zomer:** volgen die de kalender (seizoen) of het echte weer in België (weer-API)?
- **Uitschakelen:** kan een speler de sfeer uitzetten? Hoe gaat dit samen met de gekozen weergavestijl uit [004](004-weergavethemas.md)? Ligt de sfeer bovenop elk thema, of enkel op het standaarduitzicht?
- **Beeldmateriaal:** emoji's en CSS (licht, geen rechtenprobleem) of eigen illustraties? Geen officiële logo's van UEFA/FIFA.

## Aanpak (optioneel)

- **Shared:** een kalender van sfeermomenten (sleutel, start/eind of berekeningsregel, prioriteit), met tests voor de datums (vooral carnaval/Pasen en overlap).
- **Server/Client:** de server geeft het actieve moment mee (servertijd, zoals bij de daily). De client zet een `data-sfeer="halloween"`-attribuut op de app.
- **CSS:** per moment een klein blok dat enkele variabelen uit `app.css` overschrijft en een decoratie toevoegt; effecten via `animatie-bouwer`, met `prefers-reduced-motion`.
- **Admin:** eventueel datums beheren voor wisselende momenten (CL-speeldagen, interlandperiodes, toernooien).
- Verwant aan [004 – Weergavemodussen](004-weergavethemas.md) en de specials (`SpecialTemplates`).

## Notities

