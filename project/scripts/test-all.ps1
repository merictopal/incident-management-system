<#
  Full verification on a developer machine (no Docker needed):
    1. dotnet build            (all services, gateway, tests)
    2. dotnet test             (xUnit integration tests)
    3. start all services      (ports 5000-5004)
    4. end-to-end tests        (REST + SignalR through the gateway)
    5. monitoring scenario     (stop user-service -> Unhealthy + Critical incident -> restart -> auto-resolved)
  Results: test-logs\summary.txt
#>
param([switch]$KeepRunning)
. "$PSScriptRoot/_common.ps1"
Set-Location $Root
$Summary = Join-Path $LogDir 'summary.txt'
"Test run $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" | Set-Content $Summary -Encoding UTF8
function Add-Result($text) { Write-Host $text; Add-Content -Path $Summary -Value $text -Encoding UTF8 }
$failed = $false

& "$PSScriptRoot/stop-local.ps1" | Out-Null

Write-Step 'dotnet --version'
Add-Result "dotnet SDK: $(dotnet --version)"
Add-Result "node: $(node --version)"

Write-Step 'dotnet build'
dotnet build IncidentManagement.sln -c Debug -nologo -v minimal *> (Join-Path $LogDir 'build.log')
if ($LASTEXITCODE -ne 0) {
    Add-Result 'BUILD: FAILED (see test-logs\build.log)'
    Get-Content (Join-Path $LogDir 'build.log') | Select-String -Pattern 'error' | Select-Object -First 30 | ForEach-Object { Add-Result $_.Line }
    exit 1
}
Add-Result 'BUILD: OK'
Get-Content (Join-Path $LogDir 'build.log') | Select-String -Pattern 'warning' | Select-Object -Unique -First 20 | ForEach-Object { Add-Result "  $($_.Line.Trim())" }

Write-Step 'dotnet test'
dotnet test IncidentManagement.sln --no-build -nologo --logger 'console;verbosity=normal' *> (Join-Path $LogDir 'unit-tests.log')
$unitOk = $LASTEXITCODE -eq 0
Get-Content (Join-Path $LogDir 'unit-tests.log') | Select-String -Pattern '^\s*(Passed|Failed|Total tests|Total time)' | ForEach-Object { Add-Result "  $($_.Line.Trim())" }
Get-Content (Join-Path $LogDir 'unit-tests.log') | Select-String -Pattern '^\s*(Passed!|Failed!)' | ForEach-Object { Add-Result "UNIT: $($_.Line.Trim())" }
if (-not $unitOk) { $failed = $true; Add-Result 'UNIT TESTS: FAILED' } else { Add-Result 'UNIT TESTS: OK' }

Write-Step 'start services'
& "$PSScriptRoot/start-local.ps1" -NoBuild -NoClient *> (Join-Path $LogDir 'start.log')
if ($LASTEXITCODE -ne 0) { Add-Result 'START: FAILED (see test-logs\start.log)'; & "$PSScriptRoot/stop-local.ps1"; exit 1 }
Add-Result 'START: all services healthy'

Write-Step 'e2e tests'
Push-Location (Join-Path $Root 'tests/e2e')
if (-not (Test-Path 'node_modules')) { npm install --no-audit --no-fund *> (Join-Path $LogDir 'e2e-npm.log') }
node e2e.mjs main *> (Join-Path $LogDir 'e2e-main.log'); $e2eOk = $LASTEXITCODE -eq 0
Get-Content (Join-Path $LogDir 'e2e-main.log') | ForEach-Object { Add-Result "  $_" }
if (-not $e2eOk) { $failed = $true }

Write-Step 'monitoring scenario: stop user-service'
Stop-Tracked 'user-service'
node e2e.mjs down user-service *> (Join-Path $LogDir 'e2e-down.log'); if ($LASTEXITCODE -ne 0) { $failed = $true }
Get-Content (Join-Path $LogDir 'e2e-down.log') | ForEach-Object { Add-Result "  $_" }

Write-Step 'monitoring scenario: restart user-service'
Start-Backend 'user-service' | Out-Null
node e2e.mjs recover user-service *> (Join-Path $LogDir 'e2e-recover.log'); if ($LASTEXITCODE -ne 0) { $failed = $true }
Get-Content (Join-Path $LogDir 'e2e-recover.log') | ForEach-Object { Add-Result "  $_" }
Pop-Location

if (-not $KeepRunning) { & "$PSScriptRoot/stop-local.ps1" | Out-Null }

if ($failed) { Add-Result 'RESULT: SOME CHECKS FAILED'; exit 1 }
Add-Result 'RESULT: ALL CHECKS PASSED'
