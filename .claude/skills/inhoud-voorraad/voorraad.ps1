# Telt de inhoud van Truitje Trek per bestand: status, moeilijkheid en tags.
# Gebruik: pwsh -NoProfile -File .claude/skills/inhoud-voorraad/voorraad.ps1 [-Tag <tag>]
param([string]$Tag)

$root = Join-Path $PSScriptRoot '../../../src/VoetbalQuiz.Server/Data'
$files = Get-ChildItem (Join-Path $root 'players'), (Join-Path $root 'questions') -Filter *.json

$rows = foreach ($f in $files) {
    $items = Get-Content $f.FullName -Raw | ConvertFrom-Json
    if ($Tag) { $items = @($items | Where-Object { $_.tags -contains $Tag }) }
    [pscustomobject]@{
        Bestand   = "$($f.Directory.Name)/$($f.Name)"
        Totaal    = $items.Count
        Approved  = @($items | Where-Object status -eq 'approved').Count
        Review    = @($items | Where-Object status -eq 'review').Count
        Rejected  = @($items | Where-Object status -eq 'rejected').Count
        'Moeil 1' = @($items | Where-Object difficulty -eq 1).Count
        'Moeil 2' = @($items | Where-Object difficulty -eq 2).Count
        'Moeil 3' = @($items | Where-Object difficulty -eq 3).Count
        'Moeil 4' = @($items | Where-Object difficulty -eq 4).Count
        'Moeil 5' = @($items | Where-Object difficulty -eq 5).Count
    }
}
if ($Tag) { "Enkel items met tag '$Tag'" }
$rows | Format-Table -AutoSize

if (-not $Tag) {
    'Tags (goedgekeurde items):'
    $files | ForEach-Object { Get-Content $_.FullName -Raw | ConvertFrom-Json } |
        Where-Object status -eq 'approved' | ForEach-Object { $_.tags } |
        Group-Object | Sort-Object Count -Descending | Format-Table Name, Count -AutoSize
}
