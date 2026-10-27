# Development

Install the .NET 10 SDK and PowerShell 7 on Windows. `global.json` accepts the installed .NET 10 feature band. NuGet is used for SDK runtime/reference packs when publishing; the application has no third-party NuGet package dependencies.

```powershell
./scripts/build.ps1
./scripts/test.ps1
./scripts/verify-source.ps1
./scripts/publish.ps1 -Version 1.1.2
```

Outputs go to ignored `artifacts/`. Publishing creates a self-contained Windows x64 ZIP containing GUI and CLI plus license and notices, and a SHA-256 sidecar. It downloads the Microsoft runtime packs when needed. It never starts the GUI.

## Tests

The default suite constructs synthetic binary graphs with sentinel values and unknown bits. It checks byte-identical round trips, malformed-input rejection, ownership growth/shrinkage, skin thresholds, reward isolation, bulk-action preservation, time records, Adventure cascades, difficulty and protected file writes. No game installation is needed.

```powershell
dotnet run --project tests/CtrNfSave.Tests -c Release
```

`CtrNfSave.Regression` is an optional research runner for the original private fixture layout (`samples/` plus `target/eggs`, `target/mine`, `target/fresh`). Its expected filenames are visible in `SelfTest.cs`; the files are deliberately absent from this repository. It writes only to the supplied root's `artifacts/` and compares input hashes. It is not a prerequisite for a new contributor or CI.

```powershell
dotnet run --project tests/CtrNfSave.Regression -c Release -- <private-fixture-root>
```

The optional `target/garage/` captures add a Firehawk-to-wheels-preset preservation
check when present. To keep all generated outputs in a different writable
`artifacts/` directory, append `--artifacts <absolute-artifacts-directory>` to the
regression command; private inputs are still read directly from the fixture root.

Never point a test at the live game's save directory. Public CI uses only generated inputs. A passing test proves the supported edit and preservation assertions, not every in-game unlock condition.

## Release process

CI builds the solution, runs synthetic tests, audits source and packages a Windows artifact. Pushing a `v*` tag runs the same checks and publishes the ZIP and checksum as a GitHub release. A version containing `-` is marked as a prerelease. The release job has content-write permission; normal CI has read permission only. Actions are pinned to full revisions and updated by Dependabot.

Before tagging, update `Directory.Build.props`, CLI version text and `CHANGELOG.md`; confirm that the tagged version matches the package version. Review the ZIP to ensure it contains no PDBs, saves, local settings or game assets.
