<#
  Builds and starts the whole system on this machine without Docker:
  4 microservices + API Gateway (ports 5000-5004) and the SvelteKit client (5173).
  Stop everything with:  .\scripts\stop-local.ps1
#>
param([switch]$NoClient, [switch]$NoBuild)
. "$PSScriptRoot/_common.ps1"
Set-Location $Root

$busy = @($Backend.Values | Where-Object { Test-PortInUse $_.Port } | ForEach-Object { $_.Port })
if ($busy.Count -gt 0) {
    Write-Host "Ports already in use: $($busy -join ', '). Run the 'Stop all' task (or .\scripts\stop-local.ps1) or 'docker compose down' first." -ForegroundColor Red
    exit 1
}

if (-not $NoBuild) {
    Write-Step 'dotnet build'
    dotnet build IncidentManagement.sln -c Debug -nologo -v minimal
    if ($LASTEXITCODE -ne 0) { Write-Host 'Build failed.' -ForegroundColor Red; exit 1 }
}

Write-Step 'Starting backend services'
foreach ($name in $Backend.Keys) {
    $p = Start-Backend $name
    Write-Host ("  {0,-22} http://localhost:{1}  (pid {2})" -f $name, $Backend[$name].Port, $p.Id)
}
foreach ($name in $Backend.Keys) {
    if (-not (Wait-Healthy "http://localhost:$($Backend[$name].Port)/health")) {
        Write-Host "$name did not become healthy - see test-logs\$name.*.log" -ForegroundColor Red
        exit 1
    }
}
Write-Host '  all backend services are healthy' -ForegroundColor Green

if (-not $NoClient) {
    Write-Step 'Starting client (http://localhost:5173)'
    Push-Location (Join-Path $Root 'client')
    if (-not (Test-Path 'node_modules')) { npm ci }
    $client = Start-Process -FilePath 'cmd.exe' -ArgumentList '/c', 'npm run dev' -PassThru -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $LogDir 'client.out.log') -RedirectStandardError (Join-Path $LogDir 'client.err.log')
    Pop-Location
    $pids = Read-Pids; $pids['client'] = $client.Id; Save-Pids $pids
    if (Wait-Healthy 'http://localhost:5173' 60) { Write-Host '  client is up: http://localhost:5173' -ForegroundColor Green }
}
