# Lance Maquereaux.exe en mode --smoke-test et vérifie qu'il se ferme proprement (code 0).
# Usage : ./build/Invoke-SmokeTest.ps1 -ExePath publish/Maquereaux.exe
param(
    [Parameter(Mandatory = $true)][string]$ExePath,
    [int]$TimeoutSeconds = 120
)

$dataDir = Join-Path $env:RUNNER_TEMP "maquereaux-smoke-data"
if (-not $env:RUNNER_TEMP) { $dataDir = Join-Path $env:TEMP "maquereaux-smoke-data" }
New-Item -ItemType Directory -Force -Path $dataDir | Out-Null
$env:MAQUEREAUX_DATA_DIR = $dataDir
$report = Join-Path $dataDir "smoke-report.txt"
$env:MAQUEREAUX_SMOKE_REPORT = $report

Write-Host "Lancement de $ExePath --smoke-test (données : $dataDir)"
$proc = Start-Process -FilePath $ExePath -ArgumentList "--smoke-test" -PassThru
$timedOut = -not $proc.WaitForExit($TimeoutSeconds * 1000)
if ($timedOut) { $proc.Kill() }

if (Test-Path $report) {
    Write-Host "---- Rapport smoke test ----"
    Get-Content $report | ForEach-Object { Write-Host $_ }
}

$logDir = Join-Path $dataDir "logs"
if (Test-Path $logDir) {
    Write-Host "---- Logs appli ----"
    Get-ChildItem $logDir -Filter *.log | ForEach-Object { Get-Content $_.FullName | Select-Object -Last 60 | ForEach-Object { Write-Host $_ } }
}

if ($timedOut) { throw "Smoke test : l'appli ne s'est pas fermée en $TimeoutSeconds s" }
if ($proc.ExitCode -ne 0) { throw "Smoke test : code de sortie $($proc.ExitCode)" }
Write-Host "✅ Smoke test OK (code 0)"
