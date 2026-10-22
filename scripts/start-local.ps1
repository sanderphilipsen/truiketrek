# Zet alles klaar om Truitje Trek lokaal te draaien en meldt per stap wat de toestand is.
# Idempotent: alles wat al draait, wordt gewoon overgeslagen.
#
#   1. .NET SDK aanwezig?
#   2. Podman aanwezig en de Podman-machine gestart? (start ze zo nodig)
#   3. PostgreSQL-container voetbalquiz-db gestart en bereikbaar op localhost:5433? (scripts/start-db.ps1)
#   4. Draait de app al op poort 5174?
#
# De app zelf wordt niet gestart; dat doet de skill /start-app via de browserpreview, of jijzelf met:
#   dotnet run --project src/VoetbalQuiz.Server --launch-profile http
#
# Exitcode 0 = alles klaar voor de app; 1 = iets ontbreekt (zie de regels met [FOUT]).

$ErrorActionPreference = "Continue"
$root = Split-Path $PSScriptRoot -Parent
$failed = $false

function Ok($msg)   { Write-Host "[OK]   $msg" -ForegroundColor Green }
function Info($msg) { Write-Host "[..]   $msg" -ForegroundColor Cyan }
function Fail($msg) { Write-Host "[FOUT] $msg" -ForegroundColor Red; $script:failed = $true }

# 1. .NET SDK
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if ($dotnet) {
    Ok ".NET SDK $(dotnet --version)"
} else {
    Fail ".NET SDK niet gevonden. Installeer de .NET 10 SDK: https://dotnet.microsoft.com/download"
}

# 2. Podman + machine
$podman = Get-Command podman -ErrorAction SilentlyContinue
if (-not $podman) {
    Fail "Podman niet gevonden. Installeer Podman Desktop of 'winget install RedHat.Podman'."
} else {
    $null = podman info --format "{{.Host.Hostname}}" 2>$null
    if ($LASTEXITCODE -eq 0) {
        Ok "Podman-machine draait"
    } else {
        $machines = podman machine list --format "{{.Name}}" 2>$null
        if (-not $machines) {
            Fail "Geen Podman-machine. Maak er een met 'podman machine init' en start ze met 'podman machine start'."
        } else {
            Info "Podman-machine starten (kan een halve minuut duren)..."
            podman machine start 2>&1 | Where-Object { $_ -match "started|Error|failed to start" } | ForEach-Object { Write-Host "       $_" }
            $null = podman info --format "{{.Host.Hostname}}" 2>$null
            if ($LASTEXITCODE -eq 0) { Ok "Podman-machine gestart" } else { Fail "Podman-machine wil niet starten. Probeer 'podman machine start' zelf." }
        }
    }
}

# 3. Database
if (-not $failed) {
    $running = podman ps --filter "name=^voetbalquiz-db$" --format "{{.Names}}" 2>$null
    if ($running) {
        Ok "Container voetbalquiz-db draait al"
    } else {
        Info "Container voetbalquiz-db starten..."
        & (Join-Path $PSScriptRoot "start-db.ps1") | Out-Null
    }

    $ready = $false
    for ($i = 0; $i -lt 30 -and -not $ready; $i++) {
        $null = podman exec voetbalquiz-db pg_isready -U voetbalquiz -d voetbalquiz 2>$null
        if ($LASTEXITCODE -eq 0) { $ready = $true } else { Start-Sleep -Seconds 1 }
    }
    if ($ready) {
        Ok "PostgreSQL antwoordt op localhost:5433"
    } else {
        Fail "PostgreSQL antwoordt niet na 30 s. Bekijk 'podman logs voetbalquiz-db'."
    }
}

# 4. App al bezig?
try {
    $resp = Invoke-WebRequest -Uri "http://localhost:5174/" -UseBasicParsing -TimeoutSec 3 -ErrorAction Stop
    Ok "De app antwoordt al op http://localhost:5174 (HTTP $($resp.StatusCode)); niet opnieuw starten"
} catch {
    Info "Poort 5174 is vrij; de app moet nog gestart worden"
}

Write-Host ""
if ($failed) {
    Write-Host "Niet alles is klaar. Los de regels met [FOUT] op en draai het script opnieuw." -ForegroundColor Red
    exit 1
}
Write-Host "Klaar voor de app." -ForegroundColor Green
exit 0
