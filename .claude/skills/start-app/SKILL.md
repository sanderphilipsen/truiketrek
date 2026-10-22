---
name: start-app
description: Start Truitje Trek lokaal in één keer: controleert .NET en Podman, start de Podman-machine en de PostgreSQL-container (localhost:5433) als die niet draaien, start dan de app op http://localhost:5174 in de browserpreview en controleert dat ze laadt. Gebruik dit wanneer de gebruiker "run the app", "start de app", "zet alles op" of iets gelijkaardigs vraagt, of vóór een test in de browser.
---

# App lokaal starten

Alles wat de app nodig heeft, in de juiste volgorde. Elke stap is idempotent: wat al draait, wordt overgeslagen.

## 1. Vereisten en database

Draai het controlescript (PowerShell, blokkeert maximaal een minuut):

```bash
pwsh -NoProfile -File scripts/start-local.ps1
```

Het script meldt per regel `[OK]`, `[..]` of `[FOUT]`:
- controleert de .NET SDK en Podman;
- start de Podman-machine als ze niet draait (`podman machine start`);
- start de container `voetbalquiz-db` via `scripts/start-db.ps1` en wacht tot `pg_isready` antwoordt op localhost:5433;
- zegt of de app al antwoordt op poort 5174.

Exitcode 1: lees de `[FOUT]`-regels, los op wat kan (bv. de machine nog eens starten) en draai het script opnieuw. Ontbreekt .NET of Podman, dan stop je en meld je dat de gebruiker die moet installeren.

Een melding over `win-sshproxy.exe` bij het starten van de machine is onschuldig: ze gaat over de Docker-API, Podman zelf werkt.

## 2. App starten

Antwoordt de app al op 5174, sla deze stap over en ga naar 3.

Start anders de dev-server via de browserpreview met de launch-configuratie `voetbalquiz` (`preview_start` met `{name: "voetbalquiz"}`, nooit via Bash). Die draait `dotnet run --project src/VoetbalQuiz.Server --launch-profile http` op poort 5174. De eerste build duurt 20 tot 40 seconden; wacht tot de poort antwoordt:

```bash
for i in $(seq 1 45); do c=$(curl -s -o /dev/null -w '%{http_code}' http://localhost:5174/); [ "$c" = "200" ] && { echo "up na ${i}x2s"; exit 0; }; sleep 2; done; echo "niet opgestart"
```

Komt er niets: bekijk `preview_logs` van die server. De meest voorkomende oorzaak is `Failed to connect to 127.0.0.1:5433` (database niet bereikbaar); dan is stap 1 niet goed afgelopen.

## 3. Controleren

- Navigeer naar http://localhost:5174 en neem een screenshot. De aanmeldpagina ("Aanmelden") of de homepagina moet zichtbaar zijn; een leeg vlak betekent dat de server nog aan het bouwen is of gecrasht is.
- `preview_logs` met niveau `error` moet leeg zijn.
- In de browserconsole zijn 401's en één 404 vóór het aanmelden normaal (de app controleert of je al aangemeld bent).

Meld kort: database gestart of al bezig, app op http://localhost:5174, en wat er eventueel niet lukte.

## Andere varianten

`.claude/launch.json` heeft ook configuraties met een aparte database (`voetbalquiz-release`, `-derby`, `-training`, `-home`, `-volgorde`, `-published`). Die gebruiken dezelfde container, dus stap 1 blijft hetzelfde; enkel de naam bij `preview_start` en de poort verschillen.
