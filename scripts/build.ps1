param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    dotnet build CtrNfSave.slnx -c $Configuration --disable-build-servers -m:1
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
} finally { Pop-Location }
