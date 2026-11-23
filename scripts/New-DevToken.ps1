<#
.SYNOPSIS
Stores a read/write development token in your user secrets, where the .http file reads it.
.PARAMETER ValidFor
For example 365d. Default: about 3 months.
#>
[CmdletBinding()]
param(
    [string] $ValidFor
)

$ErrorActionPreference = 'Stop'

$project = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($PSScriptRoot, '..', 'src', 'ElectricityTimeSeries.Api'))

$jwtArgs = @('user-jwts', 'create', '--project', $project,
    '--scope', 'TimeSeries.Read', '--scope', 'TimeSeries.Write', '--claim', 'client_id=local-dev', '--output', 'token')
if ($ValidFor) {
    $jwtArgs += @('--valid-for', $ValidFor)
}

# The empty input stops dotnet user-jwts waiting on the console in non-interactive shells.
$output = '' | & dotnet @jwtArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet user-jwts failed: $output"
}
$token = "$($output | Select-Object -Last 1)".Trim()

& dotnet user-secrets set 'HttpFile:Token' $token --project $project | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw 'dotnet user-secrets set failed.'
}

# The token's middle part is Base64Url-encoded JSON holding the expiry.
$payload = $token.Split('.')[1].Replace('-', '+').Replace('_', '/')
$payload += '=' * ((4 - $payload.Length % 4) % 4)
$claims = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($payload)) | ConvertFrom-Json
$expires = [System.DateTimeOffset]::FromUnixTimeSeconds($claims.exp).ToLocalTime()

Write-Host "Stored a new token as HttpFile:Token. It expires on $($expires.ToString('yyyy-MM-dd HH:mm'))."
