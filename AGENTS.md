# Contributor and coding-agent guide

Read README.md, CONTRIBUTING.md and docs/architecture.md first. English is the primary documentation language; keep docs/fr/README.md aligned for user-facing changes.

- Core owns parsing, in-memory edits and file-write protections. It must not depend on CLI or GUI projects.
- CLI and WinForms are separate entry points. Keep GUI bootstrap in Program.cs and editing behavior in EditorForm/views.
- No third-party NuGet dependencies are needed. Keep Release builds warning-free.
- Run `dotnet run --project tests/CtrNfSave.Tests -c Release` for public synthetic tests. Private regression tests are optional; never commit user saves.
- Never run tests against a user's live game files. Use generated inputs or explicitly provided read-only copies; outputs belong in artifacts/.
- Preserve backup and external-change protections. Bulk edits must remain reviewable and cancellable before application; disk saving is separate.
- Unknown save fields/conditions must remain preserved or read-only. Distinguish native/static findings from in-game confirmation.
- Do not launch GUI windows automatically for testing. Let a user review the real app when explicitly requested.
- Source is offered under PolyForm Noncommercial 1.0.0; preserve the font's separate OFL license. Do not label the project OSI open source.
- Never publish local paths, credentials, personal profile data, game artwork or private fixture history.
