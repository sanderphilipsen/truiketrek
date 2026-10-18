---
name: vraag-toevoegen
description: Voegt vragen toe aan de vragenbank van Truitje Trek (snelvuur, carrièrepad, wie hoort er niet bij, raad het jaar, emoji, opstelling, wissel, rugnummer, mannetje) met het juiste JSON-formaat per type. Gebruik dit wanneer de gebruiker vragen wil toevoegen, de voorraad van een type wil aanvullen, of vragen wil verbeteren.
---

# Vraag toevoegen

De vragenbank staat in `src/VoetbalQuiz.Server/Data/questions/<bestand>.json`, één bestand per type. "Wie ben ik?", Kluts en Counter gebruiken de spelers; gebruik daarvoor de skill `speler-toevoegen`.

Voor veel vragen of veel opzoekwerk: laat de agent `speler-redacteur` schrijven en daarna `inhoud-controleur` nakijken.

## Stappen

1. Lees de eerste items van het doelbestand en volg exact die stijl.
2. Controleer met Grep dat de vraag nog niet bestaat. De **sleutel** van een vraag is de vraagtekst of het antwoord (zie onder). Verander je die tekst, dan wordt het een nieuwe vraag en verdwijnt de oude.
3. Zoek de feiten op en noteer de bronnen.
4. Voeg het item toe met `"status": "review"`. Pas `"approved"` + `"checkedAt"` als alles gecontroleerd is.
5. Valideer de JSON:
   ```bash
   pwsh -NoProfile -c "Get-Content src/VoetbalQuiz.Server/Data/questions/<bestand>.json -Raw | ConvertFrom-Json | Out-Null"
   ```

Elk item heeft naast de inhoud ook: `difficulty` (1–3), `status`, `sources` (URL's), `tags` (zie `speler-toevoegen` voor de lijst) en optioneel `checkedAt`.

## Formaat per type

De velden komen overeen met de records in `src/VoetbalQuiz.Shared/QuestionTypes.cs`.

| Bestand | Velden | Sleutel | Let op |
|---|---|---|---|
| `snelvuur.json` | `question`, `options` (4), `correct` (index 0–3) | question | Kort genoeg om in 5 s te lezen. Eén ondubbelzinnig juist antwoord. Wissel de positie van het juiste antwoord af. |
| `carriere.json` | `answer`, `aliases`, `clubs` (`"Club (jaar–jaar)"`, chronologisch, huidige club `(jaar–)`) | answer | Huurbeurten tellen mee als ze in de bronnen staan. |
| `niet-bij.json` | `prompt`, `options` (4), `odd` (index), `explanation` | prompt | De 3 anderen moeten allemaal aantoonbaar wél passen; leg dat uit in `explanation`. |
| `jaar.json` | `event`, `year` | event | Jaar tussen `QuestionRules.MinYear` en `MaxYear`. Het jaartal mag niet in de tekst staan. |
| `emoji.json` | `emojis`, `answer`, `aliases`, `kind` (`speler`/`club`), `explanation` | emojis + answer | 3–5 emoji's; leg elke emoji uit in `explanation`. |
| `opstelling.json` | `title`, `team`, `players` (11× `{name, aliases, position}`, position `K`/`V`/`M`/`A`) | title | Exacte basiself uit een wedstrijdverslag; titel met wedstrijd, toernooi en uitslag. |
| `wissel.json` | `answer`, `aliases`, `when`, `fee`, `from`, `to`, `extra` | inhoud | `extra` is de laatste tip en mag de naam niet bevatten. |
| `rugnummer.json` | `answer`, `aliases`, `number`, `position`, `country`, `club` (met jaren), `extra` | inhoud | Het nummer moet dat van die club in die periode zijn. |
| `mannetje.json` | `answer`, `aliases`, `avatar` (id uit `Avatars.cs`), `clues` (2) | answer | De avatar-id moet bestaan in de lijst bekende voetballers in `src/VoetbalQuiz.Shared/Avatars.cs`. |

## Na afloop

Herstart de server. Een vraag die al in het admin-paneel aangepast werd, wordt niet meer door de JSON overschreven. De voorraad per type zie je in Admin → Planning.
