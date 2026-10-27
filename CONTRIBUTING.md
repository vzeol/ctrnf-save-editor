# Contributing

**Please participate.** The editor grows through pull requests, issues, translations, reproducible bug reports and careful in-game validation. English is preferred for shared documentation; French reports and discussions are welcome.

## Report a problem or missing item

Use an [issue template](https://github.com/vzeol/ctrnf-save-editor/issues/new/choose). Include the editor version, save type, game/build version, expected behavior and exact steps. For unlock research, describe the single action between before/after states and whether you used local or server progression. Screenshots and sanitized semantic diffs are useful.

Do not upload personal saves, account identifiers, credentials, game archives or executables. A synthetic reproduction or a small description of changed offsets is preferable. Use [private security reporting](SECURITY.md) for vulnerabilities.

## Send a pull request

1. Fork the repository and create a focused branch.
2. Read [architecture](docs/architecture.md) and [development](docs/development.md).
3. Keep save parsing and edits in Core; keep UI and CLI presentation in their own projects.
4. Add a meaningful synthetic test when changing parsing or save mutations. Unknown bytes and unrelated values must survive the edit.
5. Run the build, tests and source audit in `scripts/`. Describe what was tested and distinguish static evidence from in-game confirmation.
6. Update English documentation and the French user guide when behavior changes, then open a PR.

Small, reviewable PRs help: one catalogue correction or one format finding is valuable. For a major redesign, start with an issue so we can agree on the scope. Never test writes against a live personal profile, and never launch test windows automatically.

## Evidence standards

- **In-game confirmed:** a controlled edit/action and observed result, with the relevant scope explained.
- **Native/static mapping:** authored data or code predicates support the mapping, but runtime behavior may still need confirmation.
- **Inferred:** plausible from comparisons; keep uncertainty visible and avoid broad mutation of unknown fields.

An unlock XML can locate predicates; a bypass patch alone does not prove a persistent save-field mapping. Server ownership must not be confused with local ownership.

## Licensing contributions

By submitting a contribution, you agree that your contribution is offered under this repository's [PolyForm Noncommercial 1.0.0 license](LICENSE.md), unless a clearly identified compatible third-party license applies. Submit only material you have the right to share. Do not copy game artwork, binaries or code from differently licensed projects without an explicit compatibility review.

Participation is encouraged, never mandatory. Be kind, credit evidence and help other contributors reproduce findings.
