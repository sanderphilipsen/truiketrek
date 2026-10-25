---
name: deploy
description: Publiceert Truitje Trek naar Azure (https://truitjetrek.azurewebsites.net) na een build en de tests, en controleert daarna of de app draait.
disable-model-invocation: true
---

# Deploy naar Azure

Enkel uitvoeren wanneer de gebruiker er zelf om vraagt (`/deploy`). Dit verandert de live app waar vrienden op spelen.

## Vooraf

1. `git status`: meld niet-gecommitte wijzigingen en vraag of die mee moeten. Er wordt gepubliceerd wat er lokaal staat.
2. Tests draaien; stop bij een fout:
   ```bash
   dotnet test
   ```
3. Zijn er nieuwe migraties sinds de vorige deploy (`git log` op `src/VoetbalQuiz.Server/Data/Migrations`)? Lees ze na op `DropColumn`/`DropTable` en vraag bevestiging bij dataverlies. De app migreert zelf bij het opstarten.
4. Controleer `az account show`. Is de gebruiker niet aangemeld, vraag dan om zelf `az login` te doen.

## Releasenota

Schrijf de nota zelf en vraag er niet naar: de admin wil dit niet bij elke deploy doen.

1. **Wat is er veranderd?** Neem de jongste tag `deploy/*` (`git tag -l "deploy/*" --sort=-creatordate | head -1`) en lees `git log <tag>..HEAD`. Is er nog geen tag, gebruik dan de live versie: `version` uit https://truitjetrek.azurewebsites.net/api/status is de deploytijd (`yyyyMMdd.HHmmss`, UTC), dus `git log --since="<die tijd>Z"`. Kijk ook naar niet-gecommitte wijzigingen die mee gaan.
2. **Merkt een speler het?** Tel enkel mee wat een speler ziet of voelt: nieuwe vraagtypes, specials, badges, schermen, spelregels, zichtbare bugfixes, meer vragen of spelers. Laat weg: refactors, tests, scripts, deploy, pakketupgrades, beveiliging achter de schermen, admin-only wijzigingen en het herlabelen of herschrijven van bestaande vragen. Blijft er niets over, deploy dan zonder nota.
3. **Schrijf** `release-note.json` in de scratchpad (niet in de repo):
   ```json
   { "title": "…", "text": "…", "link": "academie" }
   ```
   - `title`: 3 tot 80 tekens, de grootste nieuwigheid. Bijvoorbeeld "Nieuw: raad het rugnummer".
   - `text`: 5 tot 1000 tekens, in spelerstaal en in de jij-vorm. Het belangrijkste eerst, kort, eventueel als "• "-regels. Geen commitjargon, klassenamen of interne termen.
   - `link`: optioneel, een pagina in de app waar het nieuwe te zien is (zonder `/`). Laat het weg als er geen duidelijke pagina is.

## Publiceren

Draai op de achtergrond (duurt enkele minuten), met `-ReleaseNote` alleen als er een nota is:
```bash
pwsh scripts/deploy-azure.ps1 -ReleaseNote <pad naar release-note.json>
```
De app verstuurt de nota bij het opstarten één keer naar alle spelers ("Wat is er nieuw", het belletje). Na een geslaagde deploy zet het script de tag `deploy/<versie>`. Daar begint de volgende nota.
Het script publiceert, zet de app dicht (spelers zien enkel "Er worden momenteel updates uitgevoerd"; een vraag die al loopt, mag nog afgewerkt worden), wacht 1 minuut zodat een lopende vraag kan afronden, zet de zip op de web-app, wacht tot de nieuwe versie antwoordt en haalt de aankondiging weer weg. Reken op een minuut of vijf. Bij een dringende hotfix: `-WarnMinutes 0`.

## Nadien

- Open https://truitjetrek.azurewebsites.net in de browser en controleer dat de homepagina laadt en er geen consolefouten zijn.
- Lukt het niet: bekijk de logs met `az webapp log tail -g rg-truitje-trek -n truitjetrek --subscription "Visual Studio Enterprise Subscription"` (stop na een minuut) en meld de fout.
- Meld welke commit live staat en welke releasenota verstuurd werd, of dat er geen was en waarom. Is ze niet goed, dan kan de admin ze wissen onder Bestuurskamer → Meldingen.
