#requires -Version 7.0
param([string]$Version = '1.1.2', [string]$Runtime = 'win-x64')
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?$') { throw 'Use a semantic version.' }
if ($Runtime -ne 'win-x64') { throw 'Only the tested Windows x64 package is supported.' }
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    & "$PSScriptRoot/verify-source.ps1"
    $package = "ctrnf-save-editor-$Version-$Runtime"
    # A fresh unique stage avoids deleting or merging older release directories.
    $stage = Join-Path $root "artifacts/staging/$package-$([Guid]::NewGuid().ToString('N'))"
    $destination = Join-Path $root 'artifacts/releases'
    New-Item -ItemType Directory -Path $stage, $destination -Force | Out-Null
    foreach ($project in @('src/CtrNfSave.Gui', 'src/CtrNfSave.Cli')) {
        dotnet publish $project -c Release -r $Runtime --self-contained true --disable-build-servers -m:1 -o $stage "-p:Version=$Version" -p:DebugType=None -p:DebugSymbols=false
        if ($LASTEXITCODE -ne 0) { throw "Publish failed: $project" }
    }
    # Runtime licenses are not automatically copied by self-contained publish.
    # Resolve the exact packs used by this build, including an overridden NuGet cache.
    $assets = Get-Content src/CtrNfSave.Gui/obj/project.assets.json -Raw | ConvertFrom-Json -AsHashtable
    $deps = Get-Content (Join-Path $stage 'ctrnf-save-gui.deps.json') -Raw | ConvertFrom-Json -AsHashtable
    foreach ($library in @($deps.libraries.Keys | Where-Object { $_.StartsWith('runtimepack.') })) {
        $relative = $library.Substring('runtimepack.'.Length).ToLowerInvariant()
        $pack = @($assets.packageFolders.Keys | ForEach-Object { Join-Path $_ $relative } | Where-Object { Test-Path -LiteralPath $_ }) | Select-Object -First 1
        if (-not $pack) { throw "Runtime pack licenses not found: $library" }
        $notices = @(Get-ChildItem -LiteralPath $pack -File | Where-Object { $_.Name -match '^(LICENSE|THIRD-PARTY-NOTICES)(\.|$)' })
        if (-not $notices.Count) { throw "Missing runtime license: $library" }
        $noticeDir = Join-Path $stage ('licenses/' + $relative.Replace('/', '-'))
        New-Item -ItemType Directory -Path $noticeDir -Force | Out-Null
        $notices | Copy-Item -Destination $noticeDir
    }
    Copy-Item LICENSE.md, THIRD_PARTY_NOTICES.md, CREDITS.md, CHANGELOG.md -Destination $stage
    Copy-Item src/CtrNfSave.Gui/Assets/FONT-LICENSE.txt -Destination $stage
    Copy-Item docs/release-readme.txt -Destination (Join-Path $stage 'README.txt')
    $forbidden = Get-ChildItem $stage -Recurse -File | Where-Object { $_.Extension -in '.pdb', '.sav', '.bak', '.pak', '.elf', '.igz' }
    if ($forbidden) { throw 'Unexpected personal/debug/game data in package.' }
    $zip = Join-Path $destination "$package.zip"
    if (Test-Path -LiteralPath $zip) { throw "Package already exists: $zip" }
    Compress-Archive -Path "$stage/*" -DestinationPath $zip -CompressionLevel Optimal
    $hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $package.zip" | Set-Content -LiteralPath "$zip.sha256" -Encoding ascii
    Write-Host "Created $zip"
} finally { Pop-Location }
