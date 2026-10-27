$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    # Inspect source directories, never generated binaries or git internals.
    $roots = @('src', 'tests', 'docs', 'scripts', '.github')
    $files = @($roots | ForEach-Object { if (Test-Path $_) { Get-ChildItem $_ -File -Recurse } } |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' })
    $forbidden = $files | Where-Object { $_.Extension -in '.sav', '.bak', '.elf', '.pak', '.igz', '.exe', '.dll', '.pdb', '.pfx', '.key' }
    if ($forbidden) { throw "Forbidden source artifacts: $($forbidden.Name -join ', ')" }
    $textFiles = $files | Where-Object { $_.Extension -in '.cs', '.csproj', '.json', '.md', '.yml', '.yaml', '.ps1', '.svg' }
    $findings = @($textFiles | Select-String -Pattern '[A-Za-z]:[\\/]Users[\\/]', '[A-Za-z]:[\\/]Jeux_ReDecomp', 'gh[pousr]_[A-Za-z0-9]{30,}', 'github_pat_[A-Za-z0-9_]{30,}', '-----BEGIN [A-Z ]*PRIVATE KEY-----')
    if ($findings.Count) { $findings | ForEach-Object { Write-Error "$($_.Path):$($_.LineNumber): private data pattern" }; throw 'Source audit failed.' }
    Write-Host "Source audit passed ($($files.Count) files; generated folders excluded)."
} finally { Pop-Location }
