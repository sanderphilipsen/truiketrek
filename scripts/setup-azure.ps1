# Maakt de Azure-omgeving aan: een PostgreSQL Flexible Server (B1ms) en een web-app op het bestaande App Service-plan.
# Je kan het script opnieuw uitvoeren: wat al bestaat wordt overgeslagen, de instellingen worden opnieuw gezet.
# Het databasewachtwoord wordt alleen aangemaakt bij een nieuwe server en enkel in de app-instellingen bewaard.
# Daarna deployen: pwsh scripts/deploy-azure.ps1

param(
    [string]$Subscription = "39873856-b08e-4af0-91a3-8ffb5a44fcd6",  # Azure subscription 1
    [string]$ResourceGroup = "tbooking-rg",
    [string]$Plan = "tbooking-plan",
    [string]$Location = "westeurope",
    [string]$AppName = "truitjetrek",
    [string]$DbServer = "truitjetrek-db",
    [string]$DbName = "voetbalquiz",
    [string]$DbAdmin = "quizadmin",
    [string[]]$Admins = @("sander", "sangolan")
)

$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true
$tags = "app=truitjetrek"

az account set --subscription $Subscription
az provider register --namespace Microsoft.DBforPostgreSQL --wait

# ---------- Database ----------
$serverExists = (az postgres flexible-server list -g $ResourceGroup --query "[?name=='$DbServer'] | length(@)" -o tsv) -ne "0"
$chars = [char[]]"abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789"
$password = -join (1..28 | ForEach-Object { $chars | Get-Random })
$connectionString = "Host=$DbServer.postgres.database.azure.com;Database=$DbName;Username=$DbAdmin;Password=$password;Ssl Mode=Require"
$passwordSet = $false

if (-not $serverExists) {
    Write-Host "PostgreSQL-server '$DbServer' aanmaken (duurt enkele minuten)..."
    # --public-access 0.0.0.0: alleen Azure-diensten mogen verbinden.
    az postgres flexible-server create -g $ResourceGroup -n $DbServer -l $Location `
        --tier Burstable --sku-name Standard_B1ms --storage-size 32 --version 17 `
        --admin-user $DbAdmin --admin-password $password `
        --public-access 0.0.0.0 --zonal-resiliency Disabled --backup-retention 7 `
        --tags $tags --yes -o none
    $passwordSet = $true
}
else {
    Write-Host "PostgreSQL-server '$DbServer' bestaat al."
}

$dbExists = (az postgres flexible-server db list -g $ResourceGroup -s $DbServer --query "[?name=='$DbName'] | length(@)" -o tsv) -ne "0"
if (-not $dbExists) {
    az postgres flexible-server db create -g $ResourceGroup -s $DbServer -n $DbName -o none
}

# ---------- Web-app ----------
$appExists = (az webapp list -g $ResourceGroup --query "[?name=='$AppName'] | length(@)" -o tsv) -ne "0"
if (-not $appExists) {
    Write-Host "Web-app '$AppName' aanmaken op plan '$Plan'..."
    az webapp create -g $ResourceGroup -p $Plan -n $AppName --runtime "DOTNETCORE:10.0" --tags $tags -o none
}

# Always On: anders slaapt de app in en lopen de online wedstrijden en de dagafsluiting niet.
# WebSockets: voor SignalR. HTTPS: nodig om de PWA te installeren.
az webapp config set -g $ResourceGroup -n $AppName `
    --always-on true --web-sockets-enabled true --ftps-state Disabled --min-tls-version 1.2 `
    --startup-file "dotnet VoetbalQuiz.Server.dll" -o none
az webapp update -g $ResourceGroup -n $AppName --https-only true -o none

$settings = @()
for ($i = 0; $i -lt $Admins.Count; $i++) { $settings += "Admins__$i=$($Admins[$i])" }
# Heeft de app nog geen connection string (bv. na een afgebroken eerste run), dan krijgt de server een nieuw wachtwoord.
$hasConnection = (az webapp config appsettings list -g $ResourceGroup -n $AppName --query "[?name=='ConnectionStrings__Quiz'] | length(@)" -o tsv) -ne "0"
if (-not $passwordSet -and -not $hasConnection) {
    Write-Host "Nieuw databasewachtwoord instellen..."
    az postgres flexible-server update -g $ResourceGroup -n $DbServer --admin-password $password -o none
    $passwordSet = $true
}
if ($passwordSet) { $settings += "ConnectionStrings__Quiz=$connectionString" }
az webapp config appsettings set -g $ResourceGroup -n $AppName --settings @settings -o none

Write-Host "Klaar: https://$AppName.azurewebsites.net (nog leeg tot je deployt met scripts/deploy-azure.ps1)"
