---
name: inhoud-voorraad
description: Toont hoeveel spelers en vragen Truitje Trek heeft per type, status (approved/review/rejected), moeilijkheid en tag, en waar er tekorten zijn. Gebruik dit wanneer de gebruiker vraagt "hoeveel vragen hebben we nog?", wil weten welk type aangevuld moet worden, of wil checken of een tag genoeg inhoud heeft voor een special.
---

# Inhoud-voorraad

Tel de JSON-seed met het script naast deze skill:

```bash
pwsh -NoProfile -File .claude/skills/inhoud-voorraad/voorraad.ps1
```

Voor één tag (bv. om een special te checken):

```bash
pwsh -NoProfile -File .claude/skills/inhoud-voorraad/voorraad.ps1 -Tag rode-duivels
```

## Lezen van het resultaat

- Enkel **approved** items komen in de quiz. Veel **review** betekent: laat `inhoud-controleur` eerst nakijken voor je nieuwe inhoud laat schrijven.
- Vergelijk met wat nodig is: `GameRules.QuestionsPerDay` en `QuestionTypes.Mix` in `src/VoetbalQuiz.Shared/QuestionTypes.cs` bepalen hoeveel vragen per type een daily of special vraagt. Een type waarvan minder dan ~4 weken voorraad goedgekeurd is, is een tekort.
- Let op de spreiding van moeilijkheid: een type met bijna enkel moeilijkheid 3 is te zwaar voor de daily.
- Het script telt de JSON-bestanden, niet de database. Items die in het admin-paneel aangepast of afgekeurd werden, kunnen afwijken; de echte stand per type staat in Admin → Planning.

## Rapport

Een korte tabel per type met approved/review, de grootste tekorten bovenaan, en een voorstel: welk type of welke tag eerst aanvullen (met de skills `vraag-toevoegen` of `speler-toevoegen`, of de agent `speler-redacteur`).
