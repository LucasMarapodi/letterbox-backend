param([switch]$Live)

$ErrorActionPreference = 'Stop'
if (-not $Live) {
    Write-Output 'Smoke desativado. Use -Live para autorizar chamadas reais limitadas a Twitch/IGDB.'
    exit 0
}

$backendRoot = Split-Path -Parent $PSScriptRoot
$previousOptIn = [Environment]::GetEnvironmentVariable('IGDB__RunLiveSmoke', 'Process')
Push-Location -LiteralPath $backendRoot
try {
    [Environment]::SetEnvironmentVariable('IGDB__RunLiveSmoke', '1', 'Process')
    dotnet test --no-restore tests/Tcc.Api.Tests -c Release -p:OutputPath=bin/IgdbPr06/ --filter 'Category=IgdbLive'
    if ($LASTEXITCODE -ne 0) { throw 'Smoke não aprovado. Consulte somente o relatório sanitizado docs/evidence/G-PR-06-live.json.' }
    Write-Output 'Smoke live aprovado. Evidência sanitizada: docs/evidence/G-PR-06-live.json.'
}
finally {
    [Environment]::SetEnvironmentVariable('IGDB__RunLiveSmoke', $previousOptIn, 'Process')
    Pop-Location
}
