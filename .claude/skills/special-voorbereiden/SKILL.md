---
name: special-voorbereiden
description: Zet een special van Truitje Trek klaar rond een match, speler, jubileum of thema: tag kiezen, inhoud taggen en aanvullen, voorraad controleren en de special aanmaken (eenmalig in het admin-paneel of als vast sjabloon in SpecialTemplates). Gebruik dit wanneer de gebruiker een special wil maken of een voorstel van de agent special-scout wil uitvoeren.
---

# Special voorbereiden

Een special is een `SpecialEvent` (`src/VoetbalQuiz.Server/Data/Model/Daily.cs`). De vragenpool komt uit spelers en vragen met een bepaalde **tag** (of uit handmatig gekozen spelers), gemengd over de gekozen vraagtypes volgens `QuestionTypes.Mix`.

Nog geen idee voor een special? Laat eerst de agent `special-scout` zoeken.

## Stappen

1. **Bepaal het concept**: naam, één emoji als icoon, beschrijving van één zin, start- en einddatum, en de vraagtypes. Kijk in `SpecialTemplates` (`src/VoetbalQuiz.Shared/AdminDtos.cs`) of er al iets gelijkaardigs is.
2. **Kies de tag.** Hergebruik liefst een bestaande tag. Een nieuwe tag: kort, kleine letters, met koppeltekens (bv. `euro-2000`). Voeg ze toe aan de lijst in de skill `speler-toevoegen`, zodat de redacteurs ze kennen.
3. **Tag bestaande inhoud.** Zoek met Grep spelers en vragen die erbij passen en voeg de tag toe aan hun `tags`. Verander niets aan de vraagtekst of het antwoord: dat is de sleutel, en dan wordt het een nieuwe vraag.
4. **Check de voorraad** per type:
   ```bash
   pwsh -NoProfile -File .claude/skills/inhoud-voorraad/voorraad.ps1 -Tag <tag>
   ```
   Er moeten genoeg **approved** items zijn voor elk gekozen type. Laat anders een type weg of vul aan.
5. **Vul aan** met de skills `speler-toevoegen` / `vraag-toevoegen` of de agent `speler-redacteur`, en laat `inhoud-controleur` nakijken. Rond een match passen vaak: `Opstelling` (basiself), `Jaar`, `Snelvuur`, `Wissel` (transfers rond die ploeg), `Wie ben ik?` (de helden).
6. **Valideer de JSON** van elk aangepast bestand:
   ```bash
   pwsh -NoProfile -c "Get-Content <bestand> -Raw | ConvertFrom-Json | Out-Null"
   ```
7. **Maak de special aan:**
   - **Eenmalig** (een jubileum, een bepaalde match): herstart de server en maak ze in Admin → Specials. Het paneel toont of er tekorten zijn (`SpecialShortagesAsync`).
   - **Jaarlijks of herbruikbaar** (feestdag, vaste affiche): voeg een `SpecialTemplate` toe aan `SpecialTemplates.All`, met `Month`/`Day` voor een vaste datum en eventueel een `Affiche` met clubkleuren. Er is dan geen migratie nodig.
8. **Speel de special door** op gsm-formaat (agent `mobiel-tester`) voor ze live gaat.

## Let op

- Een special volgt dezelfde regels als de daily: max. 1000 punten, server bepaalt antwoorden en tijd.
- Uitgeschakelde vraagtypes vallen weg uit de special; staan alle gekozen types uit, dan worden ze toch gebruikt.
- Feiten rond jubilea (datum, uitslag, basiself) altijd met een bron.
