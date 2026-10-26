# 004 – Weergavemodussen: de speler kiest een thema

- **Soort:** feature
- **Status:** concept
- **Prioriteit:** midden
- **Moeite:** L
- **Aangemaakt:** 2026-10-20
- **Bron:** eigen idee; ontwerpen in [docs/design-poc/](../design-poc/index.html)

## Waarom

Truitje Trek heeft nu één uitzicht: het groene veld met witte kaarten. Als je zelf een stijl kiest, voelt de app meer als de jouwe. Het geeft ook variatie voor wie elke dag speelt, en sommige stijlen kunnen we later als beloning vrijspelen.

## Wat

Bij het profiel (of de instellingen) kiest de speler een thema dat de hele app een andere look geeft. Er liggen al elf designrichtingen klaar in [docs/design-poc/](../design-poc/index.html):

| Thema | Sfeer |
|---|---|
| [Matchprogramma](../design-poc/matchprogramma.html) | Gedrukt wedstrijdboekje: papier, inkt, grote cijfers, ticketjes |
| [Stadionbord](../design-poc/stadionbord.html) | Lichtbord op een avondmatch: donker, amber lampjes, klapcijfers |
| [Stickeralbum](../design-poc/stickeralbum.html) | Elke dag een albumpagina; juist antwoord = sticker inplakken |
| [Tactiekbord](../design-poc/tactiekbord.html) | Kleedkamer-whiteboard: tien vragen als magneetjes in een 4-3-3 |
| [Teletekst](../design-poc/teletekst.html) | Pagina 801: acht kleuren, één lettertype, gekleurde knoppen |
| [Sjaal & tifo](../design-poc/sjaal-tifo.html) | Gebreide sjaals, je dag kleurt een tifo in |
| [Pixelvoetbal](../design-poc/pixelvoetbal.html) | Voetbalspel uit 1993: menu met cursor, tekstballonnen, GOAL!! |
| [Kantine](../design-poc/kantine.html) | Krijtbord aan de muur, punten geturfd op een bierviltje |
| [Krijtlijnen](../design-poc/krijtlijnen.html) | Het scherm is het veld: score in de middencirkel, tips in de zestien |
| [Scheidsrechter](../design-poc/scheidsrechter.html) | Boekje van de ref en VAR-beelden: fout = gele kaart |
| [Oud krijtbord](../design-poc/krijtbord.html) | Versleten schoolbord: krijtstof, uitgeveegde tactiek |

Het huidige uitzicht ("Veld") blijft de standaard.

## Open vragen

- **Hoe ver gaat een thema?** Enkel kleuren, lettertypes en texturen (een "skin" via de CSS-variabelen in `css/tokens.css`)? Of ook andere layouts en mechanieken, zoals het stickeralbum dat stickers inplakt, het tactiekbord met magneetjes of de gele kaart bij een fout? Die tweede soort is per thema veel meer werk.
- **Welke thema's eerst?** Elf is veel. Starten we met 2-3 die vooral een skin zijn, zoals Stadionbord, Teletekst of Matchprogramma?
- **Gratis of vrij te spelen?** Iedereen kiest vrij, of je verdient thema's met XP, badges, reeksen of de Gouden Bal?
- **Waar bewaren we de keuze?** Op het account (nieuw veld op `AppUser` + migratie, zodat de keuze meegaat naar elk toestel) of lokaal in de browser?
- **Donkere modus:** wordt die een eigen thema, of volgt elk thema `prefers-color-scheme`?
- **Toegankelijkheid:** elk thema moet voldoende contrast halen (≥ 4,5:1) en "minder beweging" respecteren. Wie bewaakt dat per thema?
- **Naamgeving:** "thema" betekent in de code al iets anders (`SpecialEvent.Theme`, en de themakenners in [003](003-themakenners.md)). Noemen we dit "stijl", "look" of "modus"?

## Aanpak (optioneel)

- **Client:** de kleuren staan al als variabelen op `:root` in `css/tokens.css` (± 240 keer `var(--…)` gebruikt). Een thema kan als `[data-theme="stadionbord"]` die variabelen overschrijven, eventueel met een eigen klein CSS-bestand per thema. Harde kleuren die nu nog in de CSS staan, moeten eerst variabelen worden.
- **Profiel:** een kiezer met een voorbeeldkaartje per thema.
- **Server/Data:** eventueel een veld voor het gekozen thema op de gebruiker + EF-migratie (skill `ef-migratie`).
- **Ontwerp en test:** uitwerken met `ux-designer`, testen op gsm met `mobiel-tester`, effecten (sticker, gele kaart) via `animatie-bouwer`.

## Notities

