---
name: speler-toevoegen
description: Voegt een voetballer toe aan de spelersdatabase van Truitje Trek of verbetert diens tips (gebruikt door "Wie ben ik?", Kluts en Counter). Gebruik dit wanneer de gebruiker een speler wil toevoegen, tips wil herschrijven, of vraagt om "meer spelers" voor een land, club of thema.
---

# Speler toevoegen

Spelers staan in `src/VoetbalQuiz.Server/Data/players/*.json`. Kies het bestand dat past bij het land of de groep (`belgie-1.json`, `belgie-2.json`, `duitsland.json`, `engeland.json`, `italie-frankrijk.json`, `spanje.json`, `klassiekers.json`, `specials.json`). Bij twijfel: kijk waar vergelijkbare spelers staan.

Voor meerdere spelers of veel opzoekwerk: laat de agent `speler-redacteur` schrijven en daarna `inhoud-controleur` nakijken.

## Stappen

1. **Bestaat hij al?** Zoek met Grep op achternaam, ook zonder accenten (bv. `Mbapp`). Spelers worden op `name` bijgewerkt, dus een tweede item met dezelfde naam overschrijft het eerste.
2. **Zoek de feiten op** (Wikipedia en/nl, Transfermarkt) en noteer de URL's.
3. **Schrijf het item** volgens het formaat hieronder.
4. **Valideer de JSON:**
   ```bash
   pwsh -NoProfile -c "Get-Content src/VoetbalQuiz.Server/Data/players/<bestand>.json -Raw | ConvertFrom-Json | Out-Null"
   ```
5. Herstart de server om de seed te laden. Let op: een speler die al in het admin-paneel aangepast werd, wordt niet meer door de JSON overschreven.

## Formaat

```json
{
  "name": "Kevin De Bruyne",
  "answers": ["De Bruyne", "KDB"],
  "country": "België",
  "competition": "Premier League",
  "era": "modern",
  "difficulty": 1,
  "hints": [
    "Ik werd in 1991 geboren in Drongen, vlak bij Gent.",
    "Ik ben een middenvelder die bekendstaat om zijn messcherpe passes en assists.",
    "Ik werd kampioen met Genk in 2011 en kreeg weinig kansen bij Chelsea.",
    "Bij Wolfsburg brak ik helemaal door en werd ik Speler van het Jaar in Duitsland.",
    "Met Manchester City won ik in 2023 de treble, met de Champions League als kroon."
  ],
  "status": "review",
  "sources": ["https://en.wikipedia.org/wiki/Kevin_De_Bruyne"],
  "tags": ["champions-league", "rode-duivels"]
}
```

## Regels

- **Precies 5 tips**, in de **ik-vorm**, één zin per tip, van **vaag naar duidelijk**. Tip 1 is iets dat veel spelers kan zijn (geboortejaar/-streek, speelstijl); tip 5 maakt het voor een fan zeker (grootste prestatie, record).
- **Nooit de naam verklappen:** geen voornaam, achternaam of iets uit `answers` in een tip, ook niet in een clubnaam of familielid ("mijn broer Thorgan" verklapt Hazard). Het admin-paneel weigert zulke tips.
- `answers`: enkel extra aanvaarde antwoorden naast de volledige naam (achternaam, bekende bijnaam). Tikfouten, accenten en fonetiek regelt `AnswerMatcher` zelf. Deel twee spelers een achternaam (Eden/Thorgan Hazard), zet dan de voornaam erbij als antwoord.
- `era`: `modern` of `classic` (max. 2 klassiekers per dag).
- `difficulty`: 1 = iedereen kent het, 2 = makkelijk, 3 = voetbalfan, 4 = moeilijk, 5 = echte kenner. De daily en specials tonen het gemiddelde als 0–5 sterren op het beginscherm.
- `tags`, enkel bestaande: `rode-duivels`, `champions-league`, `legende` (alle klassiekers), `gouden-schoen`, `anderlecht`, `club-brugge`, `standard`, `oranje`, `wk`, `familie`, `curiosa`, en de feestdagen `kerst`, `nieuwjaar`, `halloween`, `sinterklaas`. Specials en affiches selecteren op deze tags.
- `status`: nieuw = `"review"`. Zet pas `"approved"` + `"checkedAt": "JJJJ-MM-DD"` als elk feit gecontroleerd is en er minstens één bron is. Enkel goedgekeurde spelers komen in de quiz.
- Schrijf in vlot Vlaams-Nederlands. Feiten die kunnen verouderen (huidige club) liever in het verleden formuleren.
