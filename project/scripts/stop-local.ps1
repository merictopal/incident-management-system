# Stops everything started by start-local.ps1 / test-all.ps1.
. "$PSScriptRoot/_common.ps1"
foreach ($name in @((Read-Pids).Keys)) { Stop-Tracked $name; Write-Host "stopped $name" }

# Also stop project processes that were started from another copy of the project
# (e.g. an older download) and still hold the project ports. Only dotnet/node are touched.
$ports = @($Backend.Values | ForEach-Object { $_.Port }) + 5173
foreach ($port in $ports) {
    foreach ($conn in @(Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue)) {
        $proc = Get-Process -Id $conn.OwningProcess -ErrorAction SilentlyContinue
        if ($proc -and @('dotnet', 'node') -contains $proc.ProcessName) {
            & taskkill /PID $proc.Id /T /F 2>&1 | Out-Null
            Write-Host "stopped $($proc.ProcessName) (pid $($proc.Id)) on port $port"
        }
    }
}
Write-Host 'All project ports are free.'
