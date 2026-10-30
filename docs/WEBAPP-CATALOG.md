# OmniDeck — Web App Catalog

The catalog is the single source of truth for which web apps OmniDeck can run and how each is sandboxed. It ships inside the app at `catalog/webapps.json` and is refreshed (at most daily) from this repository's raw URL — so **adding a web app is a pull request, not a release**. The flip side: installed apps read the file from `main`, so every merge to `main` is a production change for all users. That is why `main` only accepts reviewed, signed pull requests and why CI validates the catalog on every one of them.

OmniDeck deliberately does **not** allow arbitrary user-added URLs in v1: the curated model is what makes per-app navigation allowlists, profile isolation, and SSO popup handling tractable and reviewable.

## 1. Entry schema

```json
{
  "id": "gmail",
  "name": "Gmail",
  "startUrl": "https://mail.google.com/",
  "profile": "google",
  "navAllow": ["google.com", "googleusercontent.com", "gstatic.com"],
  "popupAllow": ["accounts.google.com"],
  "notifications": true,
  "audioPin": false
}
```

| Field | Type | Meaning |
|---|---|---|
| `id` | string, unique, `[a-z0-9-]` | stable identifier; also the settings key |
| `name` | string | display name |
| `startUrl` | https URL | initial navigation target |
| `profile` | string | cookie/profile group; entries sharing a value share sign-in. Reserved group: `google`. Anything else should be unique per app unless sharing is intentional |
| `navAllow` | string[] | domains the main frame may navigate to; anything else opens in the system default browser |
| `popupAllow` | string[] | domains whose `window.open` popups open **in-app on the same profile** (SSO flows); all other popups go to the default browser |
| `notifications` | bool | whether web notifications are bridged to Windows toasts (user can still disable per app) |
| `audioPin` | bool | if true, the governor treats the app as pinned while it is playing audio |

**Domain matching.** `navAllow` and `popupAllow` entries are lowercase domains or subdomains — no scheme, port, path, or wildcard. A host matches an entry when it equals it or ends with `.` + the entry, on whole DNS labels: `google.com` matches `mail.google.com`, never `evilgoogle.com`. `navAllow` governs **top-level (main-frame) navigations only**; subresource requests (scripts, images, media, API calls to CDNs) are never filtered by it, so a CDN belongs in `navAllow` only when the app really navigates the main frame there (a file download or a media viewer, for example). The `startUrl` host and every `popupAllow` entry must be covered by `navAllow`.

Top-level file fields: `$schema` (optional editor hint), `schemaVersion` (int), `updated` (ISO date of the last content change), `apps` (array). The JSON Schema is [`catalog/webapps.schema.json`](../catalog/webapps.schema.json); `scripts/validate-catalog.ps1` checks it plus the rules a schema can't express (unique `id` and `name`, the coverage rules above, `updated` not in the future), and CI runs it on every pull request. The app rejects any fetched catalog whose `schemaVersion` it does not understand or that fails validation, and keeps the bundled copy.

## 2. Review rules for new entries (PR checklist)

1. HTTPS `startUrl`; service must work in a desktop Chromium/Edge browser without extensions.
2. `navAllow` is the *minimal* set of domains the app's main frame genuinely navigates to — test sign-in redirects, core flows, and downloads; do not add ad/tracking domains just to silence the default-browser handoff, and do not add CDN/asset domains that are only loaded as subresources.
3. `popupAllow` contains only identity/SSO domains actually used by the sign-in flow.
4. Isolated `profile` unless there is a concrete single-sign-on reason to share.
5. `audioPin: true` only for apps whose primary function is audio/video playback.
6. No services that primarily distribute illegal content, and nothing requiring DRM schemes known not to work in WebView2 (see ROADMAP risk register).
7. State in the PR description which flows you manually tested inside OmniDeck (or a WebView2 sample) — sign-in, notifications, a core action.
8. `pwsh scripts/validate-catalog.ps1` passes locally (CI runs it too).

## 3. Initial catalog (v1)

| id | profile | audioPin | Notes |
|---|---|---|---|
| spotify | spotify | yes | Web Player; DRM works in the runtime ([spike #1](spikes/01-drm-widevine.md)); the third-party sign-in buttons (Google/Facebook/Apple) are still untested ([spike #2](spikes/02-google-sign-in.md)) |
| discord | discord | no | no global PTT on web; host hotkey partial mitigation |
| youtube | google | yes | sign-in hops to Google's country-code domains, which need `navAllowSets` (P2, spike #2) |
| gmail | google | no | |
| gcalendar | google | no | |
| gdrive | google | no | |
| gmaps | google | no | replaces a native map mini-app entirely |
| facebook | meta | no | also Meta's web messaging (`facebook.com/messages`) since messenger.com closed in April 2026; the Messenger entry was removed |

## 4. Known-good candidates for post-v1 PRs

WhatsApp Web, Telegram Web, X, Reddit, Notion — all verified-working-in-Edge, low-controversy additions the community can contribute following §2.
