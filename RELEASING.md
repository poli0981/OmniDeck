# Releasing OmniDeck

Releases are automated by [`release.yml`](.github/workflows/release.yml), triggered by pushing a GPG-signed SemVer tag on `main`. Version numbers follow the plan in the [ROADMAP](docs/ROADMAP.md) (§2).

## Steps

1. Make sure `main` is green and contains everything for the release.
2. Choose the version `X.Y.Z[-suffix]`. In a pull request, move the `[Unreleased]` notes in [CHANGELOG.md](CHANGELOG.md) under `## [X.Y.Z] — <date>`, add the compare link at the bottom, and set `<Version>` in `Directory.Build.props` (the dev-build default; the release build stamps the tag's version anyway). Merge it.
3. Tag the merge commit on `main` with a **signed, annotated** tag and push it:

   ```bash
   git switch main && git pull --ff-only
   git tag -s vX.Y.Z -m "OmniDeck X.Y.Z"
   git tag -v vX.Y.Z
   git push origin vX.Y.Z
   ```

   The workflow refuses lightweight or unsigned tags and tags whose commit is not on `main`. The `release tags` ruleset protects `v*` tags: once pushed, a release tag can't be moved or deleted (only a repository admin can bypass this).
4. The **Release** workflow runs on `windows-latest` and:
   - checks the tag (SemVer, annotated, verified signature, commit on `main`) and picks the release type: every version below 1.0.0, and any version with a SemVer suffix (`-alpha.1`, `-beta.2`, …), is a **pre-release**;
   - restores the pinned `vpk` tool and downloads the previous release so a delta package can be computed;
   - publishes and packages with Velopack (`scripts/pack.ps1`): self-contained `win-x64`, ReadyToRun, with the legal documents and the runtimes' notices;
   - creates **build-provenance attestations** for `Setup.exe`, the portable zip, and this version's `.nupkg` files;
   - uploads everything as a **draft** GitHub Release, attaches the **SPDX SBOM** (`sbom.spdx.json`) and **`checksums.txt`** (SHA-256 of every published asset), sets the release notes to this version's section of `CHANGELOG.md`, then **publishes** it — as a pre-release, or as the latest release for stable versions. Installed copies only see the update once every asset is in place;
   - announces the release on Discord (`DISCORD_RELEASE_WEBHOOK`) with `.github/scripts/discord_notify.py`.
5. Check the release page: the assets are all there, and the attestation verifies:

   ```bash
   gh release download vX.Y.Z --repo poli0981/OmniDeck --pattern "*-Setup.exe"
   gh attestation verify OmniDeck-win-Setup.exe --repo poli0981/OmniDeck
   ```

There is no manual "Run workflow" for releases: `vpk` would create the tag itself, unsigned. To re-announce a release on Discord, run **Actions → Notify → Run workflow** with the tag.

## If a release fails

- Failures on tag pushes are posted to Discord (`DISCORD_CI_WEBHOOK`) by `notify.yml`.
- A failed run can leave a draft release behind: delete the draft before re-running the workflow, or `vpk upload` refuses to create it again.
- If the fix needs a code change, merge it to `main` through a pull request and tag a **new** version — release tags are immutable.

## Repository secrets

| Secret | Used for | Required |
|---|---|---|
| `GITHUB_TOKEN` | create the release, upload assets, read the SBOM | automatic |
| `DISCORD_RELEASE_WEBHOOK` | release announcements (`release.yml`, `notify.yml`) | optional — skipped when absent |
| `DISCORD_CI_WEBHOOK` | failure alerts for CI, CodeQL, SonarCloud, and Release runs on pushes (`notify.yml`) | optional |
| `SONAR_TOKEN` | SonarCloud analysis and coverage (`sonarcloud.yml`) | for SonarCloud (runs without it are skipped) |

> `poli0981` is a user account (not an organization), so secrets are set **per repository** (Settings → Secrets and variables → Actions).

## Code signing

Not done yet: releases ship without an Authenticode signature, so users may see a SmartScreen "unknown publisher" warning. Mitigations: signed tags, build-provenance attestations, SHA-256 checksums, an SBOM, no obfuscation, and public source. When Azure Trusted Signing (or SignPath Foundation) is configured, add a signing step between `dotnet publish` and `vpk pack` in `scripts/pack.ps1`, and update [SECURITY](SECURITY.md).
