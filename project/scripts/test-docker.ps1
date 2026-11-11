<#
  Verifies the Docker Compose deployment:
  build + start all containers, wait for the gateway, run the same e2e tests
  (including stopping/starting the user-service container), then shut everything down.
  Results: test-logs\docker-summary.txt
#>
param([switch]$KeepRunning)
. "$PSScriptRoot/_common.ps1"
Set-Location $Root
$Summary = Join-Path $LogDir 'docker-summary.txt'
"Docker test run $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" | Set-Content $Summary -Encoding UTF8
function Add-Result($text) { Write-Host $text; Add-Content -Path $Summary -Value $text -Encoding UTF8 }
$failed = $false

& "$PSScriptRoot/stop-local.ps1" | Out-Null

docker version --format '{{.Server.Version}}' *> (Join-Path $LogDir 'docker-version.log')
if ($LASTEXITCODE -ne 0) { Add-Result 'DOCKER: engine not running - start Docker Desktop first.'; exit 1 }
Add-Result "Docker engine: $(Get-Content (Join-Path $LogDir 'docker-version.log'))"

Write-Step 'docker compose up --build'
docker compose up -d --build *> (Join-Path $LogDir 'docker-build.log')
if ($LASTEXITCODE -ne 0) {
    Add-Result 'COMPOSE BUILD: FAILED (see test-logs\docker-build.log)'
    Get-Content (Join-Path $LogDir 'docker-build.log') | Select-Object -Last 40 | ForEach-Object { Add-Result "  $_" }
    exit 1
}
Add-Result 'COMPOSE BUILD: OK'

if (-not (Wait-Healthy 'http://localhost:5000/health' 90)) { Add-Result 'GATEWAY: not reachable'; $failed = $true }
if (Wait-Healthy 'http://localhost:5173' 90) { Add-Result 'CLIENT: http://localhost:5173 serves the app (production build)' } else { Add-Result 'CLIENT: not reachable'; $failed = $true }

docker compose ps --format 'table {{.Service}}\t{{.State}}\t{{.Ports}}' *> (Join-Path $LogDir 'docker-ps.log')
Get-Content (Join-Path $LogDir 'docker-ps.log') | ForEach-Object { Add-Result "  $_" }

Write-Step 'microservices are not reachable from the host (only via gateway)'
$exposed = @(5001, 5002, 5003, 5004 | Where-Object { Test-PortInUse $_ })
if ($exposed.Count -eq 0) { Add-Result 'PORTS: only 5000 (gateway) and 5173 (client) are published' } else { Add-Result "PORTS: unexpectedly published: $($exposed -join ', ')"; $failed = $true }

Push-Location (Join-Path $Root 'tests/e2e')
if (-not (Test-Path 'node_modules')) { npm install --no-audit --no-fund *> (Join-Path $LogDir 'e2e-npm.log') }
node e2e.mjs main *> (Join-Path $LogDir 'docker-e2e-main.log'); if ($LASTEXITCODE -ne 0) { $failed = $true }
Get-Content (Join-Path $LogDir 'docker-e2e-main.log') | ForEach-Object { Add-Result "  $_" }

docker compose stop user-service *> $null
node e2e.mjs down user-service *> (Join-Path $LogDir 'docker-e2e-down.log'); if ($LASTEXITCODE -ne 0) { $failed = $true }
Get-Content (Join-Path $LogDir 'docker-e2e-down.log') | ForEach-Object { Add-Result "  $_" }

docker compose start user-service *> $null
node e2e.mjs recover user-service *> (Join-Path $LogDir 'docker-e2e-recover.log'); if ($LASTEXITCODE -ne 0) { $failed = $true }
Get-Content (Join-Path $LogDir 'docker-e2e-recover.log') | ForEach-Object { Add-Result "  $_" }
Pop-Location

docker compose logs --no-color *> (Join-Path $LogDir 'docker-services.log')
if (-not $KeepRunning) { docker compose down *> $null }

if ($failed) { Add-Result 'RESULT: SOME CHECKS FAILED'; exit 1 }
Add-Result 'RESULT: ALL CHECKS PASSED'
