# Contributing to OmniDeck

Thanks for helping! The lowest-friction contribution is a **web app catalog entry** — see [docs/WEBAPP-CATALOG.md](docs/WEBAPP-CATALOG.md) §2 for the review checklist. For code, read on. Everyone taking part follows the [Code of Conduct](.github/CODE_OF_CONDUCT.md).

## Dev setup

- Windows 11 x64
- **.NET 10 SDK** — the version is pinned in `global.json` (10.0.403, rolling forward to newer feature bands); `dotnet --version` to confirm
- Visual Studio 2026 (".NET desktop development" workload), JetBrains Rider, or VS Code + C# Dev Kit
- PowerShell 7.4+ (`pwsh`) for the scripts in `scripts/`
- The Microsoft Edge WebView2 Runtime ships with Windows 11

```powershell
git clone https://github.com/poli0981/OmniDeck.git
cd OmniDeck
dotnet tool restore        # vpk, dotnet-sonarscanner, XamlStyler (pinned in .config/dotnet-tools.json)
dotnet build OmniDeck.slnx
dotnet test OmniDeck.slnx
dotnet run --project src/OmniDeck.App
```

## Code style & quality gates (enforced in CI)

- `.editorconfig` is authoritative. `dotnet format OmniDeck.slnx --verify-no-changes` must pass.
- Analyzers: built-in .NET analyzers at `AnalysisLevel=latest`, plus **Roslynator**, **Meziantou.Analyzer**, **SonarAnalyzer.CSharp**, and **ReferenceTrimmer** (unused references), all configured in `Directory.Build.props`. `TreatWarningsAsErrors=true` — including unused-code diagnostics (IDE0051/IDE0052/IDE0060/IDE0005).
- XAML is formatted with **XamlStyler** (`Settings.XamlStyler`): `dotnet xstyler -c Settings.XamlStyler -d src -r -p` must pass (drop `-p` to fix). Module views must consume design tokens — a literal `FontSize=` in a module view fails CI (see [UI-MODES](docs/UI-MODES.md)).
- Catalog: `pwsh scripts/validate-catalog.ps1` (JSON Schema + semantic rules) runs on every pull request.
- Dependencies: Dependabot owns version bumps. NuGet Audit fails the restore on moderate-or-worse advisories (transitive included), and Dependency Review blocks risky pull requests. Check with `dotnet package list --outdated` before proposing a manual bump. Package versions live in each `.csproj` (no Central Package Management: GitHub's dependency graph ignores `Directory.Packages.props`).
- Static analysis: CodeQL and SonarCloud run on every pull request; fix new issues rather than suppressing them.
- Tests: xUnit v3 in `tests/OmniDeck.Tests`. New governor/provider/catalog logic needs tests; UI code does not (keep view-models testable instead).
- Packaging: `pwsh scripts/pack.ps1 -Version 0.0.1-local` publishes and packs with Velopack exactly like a release; CI does the same on every pull request.

## Conventions

- Naming: namespace `OmniDeck.*`, public type prefix `Od*`, interfaces `IOd*`; the application class is `OdApp`.
- Language: C# 14, nullable enabled, file-scoped namespaces; methods returning an awaitable end in `Async`.
- Strings: user-facing text goes in `.resx` (EN neutral + VI + JA). Never concatenate localized fragments; use format placeholders.
- App data goes under `%LocalAppData%\OmniDeck.Data`, never into Velopack's install folder `%LocalAppData%\OmniDeck` ([ARCHITECTURE](docs/ARCHITECTURE.md) §5).
- Commits: imperative subject ≤ 72 chars; scope prefix encouraged (`governor:`, `web:`, `stocks:`, `docs:`, `ci:`).
- Signatures: `main` only accepts verified (signed) commits. Signed commits are appreciated; pull requests with unsigned commits are squash-merged, so GitHub signs the result.
- Branches: `feat/…`, `fix/…`, `docs/…`, `chore/…`, `ci/…` off `main`; PRs to `main` with the template filled in. Required checks must pass before merge.
- One PR = one concern. Screenshots/GIFs for UI changes, please.

## Reporting bugs

Prefer the in-app **About → Report bug** flow (from P2) — it captures the console errors and logs the template asks for. For web app issues, include the console errors it captured; "it doesn't work" without details is triaged last.

## Contact

Questions: [GitHub Discussions](https://github.com/poli0981/OmniDeck/discussions) or [Discord](https://discord.gg/u8tn3YypyX). Contribution or Code of Conduct matters: code@poli0981.dev. Security issues: see [SECURITY](SECURITY.md).

## Legal

By contributing you agree your contributions are licensed under **GPL-3.0** (see LICENSE). Don't submit code you don't have the right to license; note the origin and license of any adapted third-party code in the PR so THIRD-PARTY-NOTICES can be updated.
