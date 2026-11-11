# Shared helpers for the local (non-Docker) scripts. Works on Windows PowerShell 5.1 and PowerShell 7.

$ErrorActionPreference = 'Continue'
# English dotnet CLI output, so logs are readable and the summary patterns match on any Windows language.
$env:DOTNET_CLI_UI_LANGUAGE = 'en'
$Root = Split-Path -Parent $PSScriptRoot
$LogDir = Join-Path $Root 'test-logs'
$PidFile = Join-Path $LogDir 'pids.json'
New-Item -ItemType Directory -Force -Path $LogDir | Out-Null

# name -> (project folder, assembly name, port)
$Backend = [ordered]@{
    'user-service'         = @{ Dir = 'services/user-service';         Dll = 'UserService.dll';         Port = 5001 }
    'incident-service'     = @{ Dir = 'services/incident-service';     Dll = 'IncidentService.dll';     Port = 5002 }
    'notification-service' = @{ Dir = 'services/notification-service'; Dll = 'NotificationService.dll'; Port = 5003 }
    'monitoring-service'   = @{ Dir = 'services/monitoring-service';   Dll = 'MonitoringService.dll';   Port = 5004 }
    'api-gateway'          = @{ Dir = 'api-gateway';                   Dll = 'ApiGateway.dll';          Port = 5000 }
}

function Write-Step($message) { Write-Host "`n== $message" -ForegroundColor Cyan }

function Test-PortInUse([int]$port) {
    $listener = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
    return $null -ne $listener
}

function Read-Pids {
    if (Test-Path $PidFile) {
        $obj = Get-Content $PidFile -Raw | ConvertFrom-Json
        $map = @{}
        foreach ($p in $obj.PSObject.Properties) { $map[$p.Name] = [int]$p.Value }
        return $map
    }
    return @{}
}

function Save-Pids($map) { $map | ConvertTo-Json | Set-Content -Path $PidFile -Encoding UTF8 }

# Starts the already-built service DLL directly (so stopping it really stops it).
function Start-Backend([string]$name) {
    $svc = $Backend[$name]
    $bin = Join-Path $Root "$($svc.Dir)/bin/Debug/net9.0"
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ASPNETCORE_URLS = "http://localhost:$($svc.Port)"
    $proc = Start-Process -FilePath 'dotnet' -ArgumentList $svc.Dll -WorkingDirectory $bin -PassThru -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $LogDir "$name.out.log") -RedirectStandardError (Join-Path $LogDir "$name.err.log")
    Remove-Item Env:ASPNETCORE_URLS
    $pids = Read-Pids; $pids[$name] = $proc.Id; Save-Pids $pids
    return $proc
}

function Stop-Tracked([string]$name) {
    $pids = Read-Pids
    if ($pids.ContainsKey($name)) {
        # /T also stops child processes (e.g. node started by npm).
        & taskkill /PID $pids[$name] /T /F 2>&1 | Out-Null
        $pids.Remove($name); Save-Pids $pids
    }
}

function Wait-Healthy([string]$url, [int]$timeoutSeconds = 60) {
    $deadline = (Get-Date).AddSeconds($timeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $r = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 3
            if ($r.StatusCode -eq 200) { return $true }
        } catch { Start-Sleep -Milliseconds 500 }
    }
    return $false
}
