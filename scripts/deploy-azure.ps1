# Publiceert de app naar Azure App Service. De omgeving moet al bestaan (scripts/setup-azure.ps1).
# Bij het opstarten voert de app zelf de databasemigraties uit en laadt ze de spelers en vragen.
#
# Eerst gaat de app dicht: het script zet /home/data/maintenance.json op de server (buiten de app-map, dus de deploy
# wist het niet), wacht $WarnMinutes minuten zodat een vraag die nog loopt kan afronden, publiceert en haalt het bestand
# weg zodra de nieuwe versie antwoordt. Zolang het er staat, zien spelers enkel een onderhoudsmelding.

param(
    [string]$Subscription = "11e4a1ec-68c0-4790-a7dc-34fc2144ad23",  # Visual Studio Enterprise Subscription
    [string]$ResourceGroup = "rg-truitje-trek",
    [string]$AppName = "truitjetrek",
    [int]$WarnMinutes = 1,  # 0 voor een dringende hotfix
    # JSON met title, text en optioneel link: komt na de deploy bij elke speler binnen als "Wat is er nieuw".
    [string]$ReleaseNote = ""
)

$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true

$root = Split-Path $PSScriptRoot -Parent
$work = Join-Path ([IO.Path]::GetTempPath()) "voetbalquiz-deploy"
$out = Join-Path $work "publish"
$zip = Join-Path $work "app.zip"
Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue

# Een eigen versie per publicatie: zo ziet het script verderop dat de nieuwe versie draait en niet nog de vorige.
$deployId = Get-Date -Format "yyyyMMdd.HHmmss"
dotnet publish (Join-Path $root "src/VoetbalQuiz.Server") -c Release -o $out -p:InformationalVersion=$deployId

# De app verstuurt de releasenota bij het opstarten, één keer per versie (zie Program.cs).
if ($ReleaseNote) {
    $note = Get-Content $ReleaseNote -Raw | ConvertFrom-Json
    if (-not $note.title -or -not $note.text) { throw "De releasenota mist title of text." }
    @{ version = $deployId; title = $note.title; text = $note.text; link = $note.link } | ConvertTo-Json |
        Set-Content (Join-Path $out "release-note.json") -Encoding utf8
    Write-Host "Releasenota mee: $($note.title)"
}

# ZipFile gebruikt voorwaartse slashes, zodat de paden kloppen op de Linux-server.
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($out, $zip)

az account set --subscription $Subscription

# Het onderhoudsbestand via de Kudu-API, aangemeld met de token van az login.
$vfs = "https://$AppName.scm.azurewebsites.net/api/vfs/data/"
$kudu = @{ Authorization = "Bearer $(az account get-access-token --query accessToken -o tsv)"; "If-Match" = "*" }
$url = "https://$AppName.azurewebsites.net"

$updateAt = (Get-Date).ToUniversalTime().AddMinutes($WarnMinutes)
Invoke-RestMethod $vfs -Method Put -Headers $kudu -SkipHttpErrorCheck | Out-Null  # map aanmaken (bestaat ze al, dan geeft dat niets)
Invoke-RestMethod "${vfs}maintenance.json" -Method Put -Headers $kudu -ContentType "application/json" `
    -Body (@{ updateAt = $updateAt.ToString("o") } | ConvertTo-Json) | Out-Null
Write-Host "Spelers zien nu dat er om $($updateAt.ToLocalTime().ToString('HH:mm')) een update komt."

$deployed = $false
try {
    if ($WarnMinutes -gt 0) {
        Write-Host "$WarnMinutes minuut/minuten wachten zodat een lopende vraag kan afronden..."
        Start-Sleep ($WarnMinutes * 60)
    }

    # De opstartcontrole van de CLI is onbetrouwbaar (valse fouten of minutenlang wachten), dus we wachten zelf.
    # Tot de nieuwe versie klaar is, blijft de vorige versie antwoorden: daarom kijken we naar de versie.
    az webapp deploy -g $ResourceGroup -n $AppName --src-path $zip --type zip --track-status false

    Write-Host "Wachten tot de nieuwe versie draait (migraties en seed kunnen een minuut duren)..."
    for ($i = 0; $i -lt 30 -and -not $deployed; $i++) {
        Start-Sleep 10
        try { $status = Invoke-RestMethod "$url/api/status" -TimeoutSec 30 } catch { continue }
        $deployed = "$($status.version)" -like "$deployId*"
    }
}
finally {
    # Ook bij een mislukte deploy: de vorige versie draait dan gewoon verder.
    Invoke-RestMethod "${vfs}maintenance.json" -Method Delete -Headers $kudu -SkipHttpErrorCheck | Out-Null
    Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
}

if ($deployed) {
    # De volgende releasenota beschrijft de commits vanaf hier (git log deploy/<vorige>..HEAD).
    git -C $root tag "deploy/$deployId"
    Write-Host "Gedeployed: $url (versie $deployId, tag deploy/$deployId)"
}
else { Write-Error "De nieuwe versie reageert niet na 5 minuten. Bekijk de logs: az webapp log tail -g $ResourceGroup -n $AppName" }
