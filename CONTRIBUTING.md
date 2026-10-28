# Contributing to OmniDeck

Thanks for helping! The lowest-friction contribution is a **web app catalog entry** — see [docs/WEBAPP-CATALOG.md](docs/WEBAPP-CATALOG.md) §2 for the review checklist. For code, read on.

## Dev setup

- Windows 10/11 x64
- **.NET 10 SDK** (10.0.10 or newer) — `dotnet --version` to confirm
- Visual Studio 2026 (any edition) with the ".NET desktop development" workload, or VS Code + C# Dev Kit
- Microsoft Edge **WebView2 Runtime** (preinstalled on Win11; the app links the installer if missing)

```powershell
git clone https://github.com/poli0981/omnideck.git
cd omnideck
dotnet build OmniDeck.slnx
dotnet test
dotnet run --project src/OmniDeck.App
```

## Code style & quality gates (enforced in CI)

- `.editorconfig` is authoritative. `dotnet format --verify-no-changes` must pass.
- Analyzers: built-in .NET analyzers at `AnalysisLevel=latest`, plus **Roslynator** and **Meziantou.Analyzer**. `TreatWarningsAsErrors=true` — including unused-code diagnostics (IDE0051/IDE0052/IDE0060).
- XAML is formatted with **XamlStyler** (settings in repo). Module views must consume design tokens — literal `FontSize=` in a module view fails the lint step (see docs/UI-MODES.md).
- Dead code & references: `roslynator analyze` and **ReferenceTrimmer** run in CI; keep them green.
- Dependencies: Dependabot owns version bumps. `dotnet list package --vulnerable --include-transitive` is a hard CI gate. Run `dotnet outdated` before proposing manual bumps.
- Tests: xUnit in `tests/OmniDeck.Tests`. New governor/provider/catalog logic needs tests; UI code does not (keep view-models testable instead).

## Conventions

- Naming: namespace `OmniDeck.*`, public type prefix `Od*`.
- Language: C# 14, nullable enabled, file-scoped namespaces.
- Strings: user-facing text goes in `.resx` (EN neutral + VI + JA). Never concatenate localized fragments; use format placeholders.
- Commits: imperative subject ≤ 72 chars; scope prefix encouraged (`governor:`, `web:`, `stocks:`). GPG-signed commits welcome but not required.
- Branches: `feat/…`, `fix/…`, `docs/…` off `main`; PRs to `main` with the template filled in.
- One PR = one concern. Screenshots/GIFs for UI changes, please.

## Reporting bugs

Prefer the in-app **About → Report bug** flow — it captures the console errors and logs the template asks for. Manual reports must fill the console-errors field of the bug template; "it doesn't work" without console output is triaged last.

## Legal

By contributing you agree your contributions are licensed under **GPL-3.0** (see LICENSE). Don't submit code you don't have the right to license; note the origin and license of any adapted third-party code in the PR so THIRD-PARTY-NOTICES can be updated.
