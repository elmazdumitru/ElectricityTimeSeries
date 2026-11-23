<#
.SYNOPSIS
Runs the API in Docker on SQL Server, next to the Aspire dashboard, which also shows SQL Server's metrics.
The data is kept between runs. Stop it with: docker compose down --remove-orphans
.PARAMETER Fresh
Deletes the stored data first, so SQL Server starts empty.
#>
[CmdletBinding()]
param(
    [switch] $Fresh
)

$ErrorActionPreference = 'Stop'

$root = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($PSScriptRoot, '..'))
$compose = @('compose', '-f', "$root/docker-compose.yml", '-f', "$root/docker-compose.sqlserver.yml")

if ($Fresh) {
    & docker @compose down --volumes --remove-orphans
    if ($LASTEXITCODE -ne 0) {
        throw 'docker compose down failed.'
    }
}

# Starts SQL Server, applies the migrations, then starts the API.
& docker @compose up --build --detach
if ($LASTEXITCODE -ne 0) {
    throw 'docker compose up failed. See: docker compose logs migrations'
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

Running on SQL Server.
  API:         http://localhost:5020
  Dashboard:   http://localhost:18888
  SQL Server:  localhost,14330 (user sa, password Local-dev-1)
  Stop:        docker compose down --remove-orphans
'@
