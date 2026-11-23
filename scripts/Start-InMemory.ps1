<#
.SYNOPSIS
Runs the API in Docker with in-memory storage, next to the Aspire dashboard.
Stop it with: docker compose down --remove-orphans
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$root = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($PSScriptRoot, '..'))

# --remove-orphans also stops SQL Server and the collector, if Start-SqlServer.ps1 started them.
& docker compose -f "$root/docker-compose.yml" up --build --detach --remove-orphans
if ($LASTEXITCODE -ne 0) {
    throw 'docker compose up failed.'
}

Write-Host 'Waiting for the API...'
$deadline = (Get-Date).AddMinutes(2)
while ($true) {
    try {
        Invoke-WebRequest 'http://localhost:5020/health' -UseBasicParsing -TimeoutSec 2 | Out-Null
        break
    }
    catch {
        if ((Get-Date) -gt $deadline) {
            throw "The API didn't start. See: docker compose logs api"
        }
        Start-Sleep -Seconds 1
    }
}

Write-Host @'

Running in memory.
  API:        http://localhost:5020
  Dashboard:  http://localhost:18888
  Stop:       docker compose down --remove-orphans
'@
