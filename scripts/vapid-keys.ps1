# Maakt een VAPID-sleutelpaar voor pushmeldingen (P-256, base64url), zoals WebPushSender ze verwacht.
# Eén keer per omgeving: de sleutels mogen daarna NOOIT wijzigen, anders krijgt geen enkel toestel nog meldingen
# tot elke speler ze opnieuw aanzet.
#
#   pwsh scripts/vapid-keys.ps1              # toont de sleutels
#   pwsh scripts/vapid-keys.ps1 -UserSecrets # zet ze lokaal in de user-secrets van de server
#
# Op Azure: als app-instellingen Push__PublicKey en Push__PrivateKey (zie README, "Pushmeldingen").
param([switch]$UserSecrets)

$ErrorActionPreference = 'Stop'

function ConvertTo-Base64Url([byte[]]$bytes) {
    [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

$ecdsa = [System.Security.Cryptography.ECDsa]::Create([System.Security.Cryptography.ECCurve+NamedCurves]::nistP256)
$p = $ecdsa.ExportParameters($true)
# Publieke sleutel: het ongecomprimeerde punt (0x04 || X || Y); privé: de scalar D.
$public = ConvertTo-Base64Url ([byte[]](@(4) + $p.Q.X + $p.Q.Y))
$private = ConvertTo-Base64Url $p.D

if ($UserSecrets) {
    $project = Join-Path $PSScriptRoot '..' 'src' 'VoetbalQuiz.Server'
    dotnet user-secrets set 'Push:PublicKey' $public --project $project | Out-Null
    dotnet user-secrets set 'Push:PrivateKey' $private --project $project | Out-Null
    Write-Host 'VAPID-sleutels staan in de user-secrets van de server.'
}
else {
    Write-Host "Push:PublicKey  = $public"
    Write-Host "Push:PrivateKey = $private"
}
