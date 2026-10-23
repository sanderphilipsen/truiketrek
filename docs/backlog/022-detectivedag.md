# 022 – Detectivedag: "Wie is de dader?"

- **Soort:** feature
- **Status:** gedaan (`5471602`)
- **Prioriteit:** midden
- **Moeite:** M
- **Aangemaakt:** 2026-10-23
- **Bron:** tweede brainstorm over puzzels die een hele daily vullen (zoals [008](008-vraagtype-kaart.md) en [009](009-vraagtype-raster.md))

## Waarom

Een daily die voetbalkennis en logisch redeneren combineert, verpakt in een verhaaltje. Dit formaat komt in geen enkele voetbalquiz voor. Het verhaal ("iemand heeft de Gouden Schoen gestolen") maakt de dag een moment waar spelers over napraten, en omdat je alleen tikt, werkt het goed op een gsm.

## Wat

- Een **speciale daily** met één zaak, bv. **"Iemand heeft de Gouden Schoen gestolen."**
- De speler ziet **6 tot 8 verdachten**: echte spelers, met naam en eventueel een klein kaartje (positie, periode).
- Hij opent **getuigenverklaringen** een voor een. Elke verklaring is een voetbalfeit over de dader:
  - "De dader heeft nooit in Italië gespeeld."
  - "De dader is linksvoetig."
  - "De dader won de Beker van België met een Waalse club."
- Met elke aanwijzing **schrapt hij verdachten** (tikken = doorstrepen, opnieuw tikken = terugzetten). Het schrappen is alleen een hulpmiddel en wordt niet gecontroleerd.
- Op elk moment kan hij **de dader aanwijzen**. Hoe **minder aanwijzingen** hij nodig had, hoe meer punten.
- Achteraf toont het dossier per verdachte welke aanwijzing hem uitsloot, zodat de speler ook iets bijleert.

### Beslissingen (2026-10-23)

- **Twee beschuldigingen.** Is de eerste fout, dan halveren de punten; de tweede fout sluit de zaak met 0.
- **Punten:** de eerste 2 verklaringen staan open bij de start. De dader aanwijzen met enkel die open: 1000. Elke extra verklaring kost 150, met een minimum van 250. Bij 6 verklaringen: 1000, 850, 700, 550, 400.
- **Vaste volgorde** van de verklaringen, van vaag naar scherp.
- **Eén klok van 3 minuten** voor de hele zaak. Is de tijd op, of geeft de speler op: 0.
- **Schrappen is vrijblijvend:** een tik streept door, nog een tik zet terug. De server weet niet wie je doorstreepte; enkel het aantal geopende verklaringen en de beschuldiging tellen.
- **Waterdicht:** elke verklaring vermeldt in de data zelf welke verdachten ze uitsluit (niet berekend). De controle bij het opslaan eist: na alle verklaringen blijft precies de dader over, geen verklaring sluit de dader uit, elke verklaring sluit minstens één verdachte uit. Vaste woordenlijst: "speelde voor" = minstens één officiële wedstrijd, een uitleenbeurt telt mee.
- **Verhaaltjes:** een vaste set sjablonen (gestolen trofee, verdwenen rugnummer, gelekte opstelling, ...), met per zaak nieuwe verdachten en verklaringen.
- **Hoe vaak:** zoals De Krant, als dag op zich via specials en het admin-paneel, niet in de vaste weekplanning.
- **Online nee, Academie ja** (een gemiste zaak naspelen).
- **Eerste zaak:** "De gestolen Gouden Schoen" (dader Hans Vanaken). Zaken met actieve spelers verouderen: vóór elke inzet opnieuw nakijken.

## Open vragen

- **Pogingen:** één beschuldiging, of meer met puntenverlies? Met één poging is de spanning groter, maar een vergissing is dan meteen fataal.
- **Punten binnen 1000:** bv. 1000 met 1 aanwijzing, aflopend per extra aanwijzing? Hoeveel bij de laatste aanwijzing, als alleen de dader overblijft?
- **Volgorde van de aanwijzingen:** vast (van vaag naar scherp), of kiest de speler zelf welke getuige hij ondervraagt (meer strategie, moeilijker te balanceren)?
- **Tijd:** een klok voor de hele zaak (bv. 3 minuten), of geen klok en alleen punten per aanwijzing?
- **Waterdichtheid:** elke aanwijzing moet voor álle verdachten juist te beoordelen zijn. Hoe vermijden we discussie (bv. "speelde in Italië": telt een uitleenbeurt van een half jaar mee)?
- **Verhaaltjes:** een vaste set zaken ("gestolen trofee", "verdwenen rugnummer", "gelekte opstelling"), of elke keer een nieuw verhaal?
- **Hoe vaak:** maandelijks, in een special, of handmatig via het admin-paneel?
- **Online en Academie:** doet de detectivedag daar ook mee?

## Aanpak (optioneel)

- **Data:** een JSON-bestand per zaak: verhaal, verdachten, dader, aanwijzingen in volgorde, en per aanwijzing welke verdachten ze uitsluit, met een bron per feit. Te schrijven door `speler-redacteur` en **zeker** na te kijken door `inhoud-controleur`, want één fout feit maakt de puzzel onoplosbaar.
- **Controle bij het opslaan:** een test of admin-check die nagaat dat na alle aanwijzingen precies één verdachte overblijft, en dat geen aanwijzing de dader zelf uitsluit.
- **Shared:** nieuw `QuestionType` (bv. `Detective`), regels in `QuestionRules`. De dag via het mechanisme van specials (`Types` in `SpecialTemplates`), net zoals de kaartdag.
- **Server-autoriteit:** de client krijgt de verdachten en alleen de aanwijzingen die al geopend zijn, nooit de dader of welke verdachten een aanwijzing uitsluit. De server houdt bij hoeveel aanwijzingen geopend zijn en rekent de punten.
- **Client:** raster met verdachtenkaartjes, een lijst geopende verklaringen en een knop "Wijs de dader aan". Laten ontwerpen door `ux-designer`; `animatie-bouwer` kan de ontknoping wat beleving geven.
- Volg de skill `nieuw-vraagtype`; laten nakijken door `spelregel-reviewer`.

## Notities

- 2026-10-23: idee gekozen door de eigenaar uit een tweede brainstorm (samen met [023](023-inzetdag.md)).
