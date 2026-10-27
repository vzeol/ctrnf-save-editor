$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    dotnet run --project tests/CtrNfSave.Tests -c Release --disable-build-servers
    if ($LASTEXITCODE -ne 0) { throw 'Synthetic tests failed.' }
} finally { Pop-Location }
