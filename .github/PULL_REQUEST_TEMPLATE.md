## What & why

<!-- One concern per PR. Link the issue if one exists. -->

## Type
- [ ] Catalog entry (checklist from docs/WEBAPP-CATALOG.md §2 completed)
- [ ] Bug fix
- [ ] Feature
- [ ] Docs / CI / build

## Checklist
- [ ] `dotnet build` and `dotnet test` pass locally
- [ ] `dotnet format --verify-no-changes` clean; XAML styled (`dotnet xstyler -c Settings.XamlStyler -d src -r -p`)
- [ ] No new analyzer warnings (warnings are errors)
- [ ] Catalog changes pass `pwsh scripts/validate-catalog.ps1`
- [ ] User-facing strings added to `.resx` (EN; VI/JA if you can)
- [ ] docs/ and THIRD-PARTY-NOTICES updated if dependencies or behavior changed
- [ ] Screenshots/GIF attached for UI changes
