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

## Publiceren

Draai op de achtergrond (duurt enkele minuten):
```bash
pwsh scripts/deploy-azure.ps1
```
Het script publiceert, zet de zip op de web-app en wacht tot de app antwoordt.

## Nadien

- Open https://truitjetrek.azurewebsites.net in de browser en controleer dat de homepagina laadt en er geen consolefouten zijn.
- Lukt het niet: bekijk de logs met `az webapp log tail -g tbooking-rg -n truitjetrek` (stop na een minuut) en meld de fout.
- Meld welke commit live staat.
