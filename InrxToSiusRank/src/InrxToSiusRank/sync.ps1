[CmdletBinding()]
param(
    [string]$DatabasePath = "C:\Users\ms\Dropbox\KPS-Stevne\INRX191\storage.db3",
    [string]$StevneIds = "413-417",
    [string]$OutputDir = (Join-Path $PSScriptRoot "siusrank-import")
)

$ErrorActionPreference = "Stop"

$exePath = Join-Path $PSScriptRoot "InrxToSiusRank.exe"
if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Fant ikke InrxToSiusRank.exe ved siden av sync.ps1: $exePath"
}

if (-not (Test-Path -LiteralPath $DatabasePath)) {
    throw "Fant ikke storage.db3: $DatabasePath"
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

Write-Host "Database: $DatabasePath"
Write-Host "Stevner:  $StevneIds"
Write-Host "Output:   $OutputDir"
Write-Host ""

& $exePath --db $DatabasePath --stevne-ids $StevneIds --output-dir $OutputDir

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "Ferdig. Importfiler ligger i: $OutputDir"
