# 011 – Vraagtype "Op volgorde"

- **Soort:** feature
- **Status:** gedaan (`4803333`, `6d5fd4a`; 250 vragen in `85d98eb`); nog in geen daily of special gespeeld
- **Prioriteit:** midden
- **Moeite:** M
- **Aangemaakt:** 2026-10-22
- **Bron:** brainstormgesprek over nieuwe vraagtypes

## Waarom

Alle huidige types vragen één antwoord: een naam, een optie of een jaartal. Bij "Op volgorde" moet je verschillende dingen tegen elkaar afwegen ("was Hazard duurder dan Lukaku?"). Daardoor krijg je ook punten voor een deel van het antwoord: wie het ongeveer weet, gaat niet met lege handen naar huis. Het onthullen van de echte waarden achteraf (bedragen, jaartallen) leert de speler ook iets bij.

## Wat

- De speler ziet een opdracht, bv. **"Van duurste naar goedkoopste transfer"**, en 4 kaartjes in willekeurige volgorde.
- Hij tikt de kaartjes aan in de juiste volgorde; elk kaartje krijgt een nummer (1, 2, 3, 4). Nog eens tikken haalt het nummer weg. Na het vierde kaartje bevestigt hij met **Klaar** (of de tijd loopt af).
- Daarna toont de server de juiste volgorde mét de waarde per kaartje (bv. "€ 115 mln"), en kleurt hij juiste en foute plaatsen.

Voorbeeldopdrachten:

| Opdracht | Kaartjes |
|---|---|
| Van duurste naar goedkoopste transfer | Hazard naar Real, Lukaku naar Chelsea, De Bruyne naar City, Tielemans naar Leicester |
| Van oudste naar jongste | vier Rode Duivels van 2018 |
| Chronologisch | vier Belgische bekerfinales of WK-momenten |
| Meeste titels eerst | Anderlecht, Club Brugge, Standard, Union |
| Grootste stadion eerst | Jan Breydel, Constant Vanden Stock, Sclessin, Bosuil |

### Voorstel spelregels

- **Tijd:** 20 seconden (minimum in `QuestionRules` is 15; tikken op 4 kaartjes en nadenken vraagt iets meer).
- **Punten (max 100):** per paar dat in de juiste volgorde staat. Bij 4 kaartjes zijn er 6 paren, dus 100 × (juiste paren / 6), afgerond. Eén verwisseling van twee buren kost zo maar één paar (83 punten), terwijl je bij "per juiste plaats" dan meteen de helft kwijt bent. Uitleg aan de speler: "Hoe dichter bij de juiste volgorde, hoe meer punten."
- **Niet afgewerkt** als de tijd om is: wat aangetikt is, telt in die volgorde. Niet aangetikte kaartjes komen achteraan in hun getoonde volgorde (of: dan 0 punten, zie open vragen).
- **Server-autoriteit:** de client krijgt de kaartjes gehusseld en zonder waarden; hij stuurt alleen de gekozen volgorde (id's). De server berekent de punten en geeft daarna pas de waarden terug.

### Data (`op-volgorde.json`)

```json
{
  "question": "Van duurste naar goedkoopste transfer",
  "items": [
    { "label": "Eden Hazard naar Real Madrid (2019)", "value": "± € 115 mln" },
    { "label": "Romelu Lukaku naar Chelsea (2021)", "value": "€ 113 mln" },
    { "label": "Kevin De Bruyne naar Man City (2015)", "value": "± € 76 mln" },
    { "label": "Youri Tielemans naar Leicester (2019)", "value": "± € 45 mln" }
  ],
  "difficulty": 3,
  "status": "review",
  "sources": ["..."],
  "tags": ["rode-duivels", "transfers"],
  "checkedAt": "2026-10-22"
}
```

`items` staat in de juiste volgorde; de server husselt. `value` is alleen tekst om te tonen. (Bedragen hierboven zijn een voorbeeld, nog niet gecontroleerd.)

## Open vragen

1. **Tikken of slepen?** Voorstel: tikken in volgorde. Werkt betrouwbaar op een gsm en met een schermlezer; slepen in Blazor op touch is lastiger. Of willen we toch slepen voor het gevoel?
2. **4 of 5 kaartjes?** 4 houdt het rustig op 375 px; 5 maakt het moeilijker (10 paren). Eventueel 5 voor moeilijkheid 4-5?
3. **Puntentelling:** juiste paren (voorstel) of per juiste plaats? En niet afgewerkt = gedeeltelijk tellen of 0?
4. **Hoeveel per dag:** 10 vragen van 100 zoals de meeste types, of 5 van 200 omdat het langer duurt?
5. **Waar komt het?** Alle 7 dagen hebben al een vast type. Alleen in specials, online en mix, of een dag vervangen in de weekplanning?
6. **Veranderlijke feiten:** aantal interlands of goals verandert nog. Alleen vaste feiten gebruiken (transfers, geboortedata, afgelopen seizoenen), of mogen lopende cijfers met een datum erbij ("op 1 oktober 2026")?
7. **Derby en badges:** mag het in de derby mode (`DerbyRules`)? Een badge zoals "4 keer na elkaar foutloos"?

## Aanpak (optioneel)

- Shared: `QuestionType.Volgorde`, `QuestionTypeInfo`, `VolgordeSeconds` en puntenformule in `QuestionRules`, DTO's (kaartje met id en label; resultaat met juiste volgorde en waarden).
- Server: seeder en picker voor `op-volgorde.json`, antwoordverwerking, admin-paneel.
- Client: vraagcomponent in scorebordstijl, tik-nummering, onthulling met kleur (eventueel animatie via `animatie-bouwer`).
- Inhoud: eerste batch van ± 60 vragen via `speler-redacteur` / skill `vraag-toevoegen`, controle door `inhoud-controleur`.
- Volg de skill `nieuw-vraagtype`; tests voor de puntentelling; review door `spelregel-reviewer`.

## Notities

- 2026-10-22: gekozen uit de brainstorm samen met [012](012-vraagtype-wie-zei-het.md).
- 2026-10-22: voorstellen goedgekeurd. Beslissingen:
  - tikken in volgorde (geen slepen), 4 kaartjes, 20 seconden;
  - punten per juist paar (100 × juiste paren / 6); "Klaar" kan pas als alle kaartjes aangetikt zijn. Loopt de tijd af, dan stuurt de app de aangetikte volgorde vlak voor het einde zelf door (de rest achteraan); niets aangetikt = 0;
  - 10 vragen van 100 per dag;
  - niet in de vaste weekplanning (wel kiesbaar in het admin-paneel), wel in specials, online en de mix; niet in de derby mode;
  - alleen vaste feiten (transfers, geboortedata, afgelopen seizoenen), geen lopende cijfers.
- 2026-10-22: tijd verhoogd van 20 naar 30 seconden (20 bleek te krap).
- Gebouwd als `QuestionType.Volgorde`, bestand `Data/questions/volgorde.json` (niet `op-volgorde.json`). Geen migratie nodig: de gekozen volgorde staat in `AttemptState.Found`.
