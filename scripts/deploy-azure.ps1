# Publiceert de app naar Azure App Service. De omgeving moet al bestaan (scripts/setup-azure.ps1).
# Bij het opstarten voert de app zelf de databasemigraties uit en laadt ze de spelers en vragen.

param(
    [string]$Subscription = "39873856-b08e-4af0-91a3-8ffb5a44fcd6",  # Azure subscription 1
    [string]$ResourceGroup = "tbooking-rg",
    [string]$AppName = "truitjetrek"
)

$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true

$root = Split-Path $PSScriptRoot -Parent
$work = Join-Path ([IO.Path]::GetTempPath()) "voetbalquiz-deploy"
$out = Join-Path $work "publish"
$zip = Join-Path $work "app.zip"
Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue

dotnet publish (Join-Path $root "src/VoetbalQuiz.Server") -c Release -o $out

# ZipFile gebruikt voorwaartse slashes, zodat de paden kloppen op de Linux-server.
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($out, $zip)

az account set --subscription $Subscription
# De opstartcontrole van de CLI is onbetrouwbaar (valse fouten of minutenlang wachten), dus we wachten zelf.
# Let op: tot de nieuwe versie klaar is, blijft de vorige versie antwoorden.
az webapp deploy -g $ResourceGroup -n $AppName --src-path $zip --type zip --track-status false
Remove-Item $work -Recurse -Force

$url = "https://$AppName.azurewebsites.net"
Write-Host "Wachten tot de app opgestart is (migraties en seed kunnen een minuut duren)..."
for ($i = 0; $i -lt 30; $i++) {
    Start-Sleep 10
    $res = Invoke-WebRequest "$url/_framework/blazor.webassembly.js" -Method Head -SkipHttpErrorCheck -TimeoutSec 30 -ErrorAction SilentlyContinue
    if ($res.StatusCode -eq 200) { Write-Host "Gedeployed: $url"; exit 0 }
}
Write-Error "De app reageert niet na 5 minuten. Bekijk de logs: az webapp log tail -g $ResourceGroup -n $AppName"
