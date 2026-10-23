# 019 – De Krant (was: Matchverslag): een artikel invullen

- **Soort:** feature
- **Status:** bezig
- **Prioriteit:** midden
- **Moeite:** M
- **Aangemaakt:** 2026-10-23
- **Bron:** brainstorm over puzzels die een hele daily vullen (zoals [008](008-vraagtype-kaart.md) en [009](009-vraagtype-raster.md))

## Waarom

Losse vragen over een match zijn weetjes; een verslag met gaten laat de speler de match **opnieuw beleven**. Het is het meest eigen formaat van de vier ideeën uit de brainstorm, en het past perfect bij een special rond een verjaardag van een match (België–Japan, de bekerfinale, een titelmatch).

## Wat

- Een **speciale daily** rond één match, bv. **België–Japan 3-2 (WK 2018)**.
- De speler leest een kort verslag in stukjes, met **8 tot 10 gaten**: "In de 69e minuut kopte ___ de aansluitingstreffer binnen na een voorzet van ___."
- De gaten komen **één voor één**: het verhaal loopt verder zodra het gat is ingevuld (of de tijd op is). Het juiste antwoord verschijnt dan in het verhaal, groen of rood.
- Soorten gaten: doelpuntenmaker, aangever, invaller, trainer, minuut, eindstand, stadion, scheidsrechter.
- Aan het eind staat het volledige verslag, met een bron en eventueel een link naar de samenvatting.

### Beslissingen (2026-10-23)

- **Naam: De Krant.** Het is niet alleen een matchverslag: ook **interviews** en artikels over **schandalen** (bv. Propere Handen) mogen. Het scherm ziet eruit als een krantenartikel.
- **Eén klok voor het hele artikel.** De speler ziet meteen **de hele tekst** en tikt op een gat om het in te vullen, **in vrije volgorde**. Wie vastzit, gaat naar een ander gat. Dit vervangt het verloop "gat per gat" hierboven. Gevolg voor de redactie: de tekst rond een gat mag het antwoord van een ander gat niet verraden.
- **Invoer:** namen, clubs en plaatsen **intypen** (`AnswerMatcher`); minuten, jaartallen, bedragen en standen als **getal**, met gedeeltelijke punten als je er net naast zit (zoals bij "Raad het jaar").
- **Punten: gelijk per gat** (1000 / aantal gaten).
- **Eerste artikel:** België–Japan 3-2 (WK 2018).
- **Schandalen:** alleen feiten die vaststaan (vonnis, officiële sanctie, breed bericht in de pers) en altijd met een bron. Geen gaten die iemand een schuld toeschrijven die niet bewezen is; een lopend onderzoek wordt zo beschreven.

## Open vragen

- **Soort invoer:** elk gat intypen, of sommige gaten als meerkeuze (bv. de minuut of het stadion)?
- **Getallen:** telt een minuut die er net naast zit nog gedeeltelijk (zoals bij "Raad het jaar")?
- **Tijd:** per gat (bv. 20 seconden) of voor het hele verslag?
- **Punten binnen 1000:** gelijk per gat, of moeilijke gaten (aangever, invaller) meer waard?
- **Doorwerking:** als een gat fout is, toont het verhaal het juiste antwoord, zodat het volgende gat niet weggegeven of onmogelijk wordt. Moeten we gaten vermijden die van elkaar afhangen?
- **Welke matchen:** enkel Rode Duivels en Belgische clubs, of ook grote buitenlandse matchen (Istanbul 2005)?
- **Online en Academie:** doet het matchverslag daar ook mee?

## Aanpak (optioneel)

- **Data:** een JSON-bestand per verslag: titel, datum, bron, en een lijst stukjes tekst met per gat het juiste antwoord en aliassen. Het verslag wordt **zelf geschreven** door `speler-redacteur` (geen overgenomen persteksten) en gecontroleerd door `inhoud-controleur`.
- **Shared:** nieuw `QuestionType` (bv. `Matchverslag`), regels in `QuestionRules`. De dag via het mechanisme van specials, net zoals de kaartdag.
- **Server-autoriteit:** de client krijgt telkens alleen de tekst tot aan het volgende gat, nooit de antwoorden of de tekst erna. De server controleert met `AnswerMatcher` en rekent de punten.
- **Client:** het verhaal groeit naar beneden, het actieve gat is gemarkeerd, het invoerveld blijft boven het toetsenbord op 375 px.
- Volg de skill `nieuw-vraagtype`; laten nakijken door `spelregel-reviewer`. Bij een special: skill `special-voorbereiden`, en `special-scout` kan geschikte matchen en datums zoeken.

## Notities

- 2026-10-23: gebouwd als `QuestionType.Krant` (`592a453`). Eén vraag van 1000 punten met `Alone = true`: nooit in een mix of online. Klok 20 s per gat (min. 60 s), gaten in de tekst als `[[antwoord / ook goed : hint]]` of `[[#69 : minuut]]` (`KrantText` in Shared), gegokte getallen als paren in `AttemptState.Skipped` (geen migratie). Eerste artikel België–Japan in `krant.json`, nagekeken door `inhoud-controleur` (1970-zin herschreven, bronnen aangevuld). Inplannen via de weekplanning of een special met enkel dit type.
- 2026-10-23: omgevormd tot de vaste rubriek **"De Zondag"**: de daily van elke zondag (niet wijzigbaar in de planning) met 4 artikels van precies 5 gaten, 50 punten per gat. Klok blijft 20 s per gat (100 s per artikel). XP: de 4 artikels tellen samen als een volle dag. België–Japan gesplitst in twee artikels; ± 20 artikels geschreven door `speler-redacteur` (status "review") voor 5 zondagen, met onderwerpen van de eigenaar (Antwerp-titel 2023 met Alderweireld, de gokchinees, de OneLove-armband in Qatar, het Heizeldrama, WK 1986). Een test controleert dat geen artikel een antwoord van een ander toont.
- 2026-10-23: beslist door de eigenaar: een foute naam kost niets, zoals bij de opstelling (geen limiet per gat, enkel `KrantMaxGuesses` = 100 voor het hele artikel). De review wees erop dat iemand zo per gat bekende namen kan proberen; dat risico is aanvaard.
- 2026-10-23: idee gekozen door de eigenaar uit een brainstorm (samen met [018](018-lijstdag.md), [020](020-toernooiboom.md) en [021](021-codekraker.md)).
