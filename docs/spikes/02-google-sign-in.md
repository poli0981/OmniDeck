# Spike 02 — Google sign-in and SSO popups

**Question:** can users sign in to Google inside WebView2 with the default user agent, and do the SSO flows the catalog relies on work?
**Decision:** GO with conditions. Google, Spotify, Discord and Facebook sign-ins all completed inside WebView2 with the default user agent, one Google sign-in covered every app on the shared `google` profile, and in a controlled re-test all five sign-ins survived a restart. Two conditions remain for P2:
- Google's sign-in hops through country-code Google domains, and today's `navAllow` blocks that hop.
- One session lost every cookie it had set, and the cause is still unknown.

**Measured:** 2026-10-30. Two sessions where the maintainer signed in by hand, in WebLab on `spike/p0`: the first at `0fe4b12`, the re-test at `3eae21e` with Chromium logging on. Between them, the `--measure` run (`ec79454`) and the cookie probes (`6c60b86`). Issue [#3](https://github.com/poli0981/OmniDeck/issues/3).

## Environment

- **User agent:** WebView2 Runtime 156.0.4314.50 with its default user agent, `Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/156.0.0.0 Safari/537.36 Edg/156.0.0.0`. The string is identical to Edge's, but `navigator.userAgentData.brands` also lists **Microsoft Edge WebView2 156**, so sites can see the embedding through client hints.
- **Policy:** the catalog's `navAllow` / `popupAllow` were enforced. A navigation outside them was cancelled and logged; the product would hand it to the default browser.
- **Profiles:** one environment, one profile per catalog `profile` group.
- **Location:** Vietnam.

## Results

### Sign-in flows (main frame)

| App (profile) | Flow | Result |
|---|---|---|
| YouTube (`google`) | `youtube.com` → `accounts.google.com` sign-in → passkey-enrollment prompt → `CheckCookie` → Google's prompts (a home-address prompt the first time, identity verification by selfie on the re-test) → `accounts.youtube.com/accounts/SetSID` → **`accounts.google.com.vn/accounts/SetSID`** | The credentials were accepted. The main-frame hop to `google.com.vn` falls outside `navAllow` and was cancelled. Google retried it 15 times in about 40 s the first time and 6 times on the re-test, a loop the user can only leave by navigating away. The `.google.com.vn` sign-in cookies were set anyway, through subresource requests. |
| Gmail, Drive, Calendar, Maps (`google`) | `mail.google.com` → `accounts.google.com/ServiceLogin` → `<service>/accounts/SetOSID` → app | Signed in with no prompt. One sign-in covers the group. |
| Spotify (`spotify`) | `open.spotify.com` → `accounts.spotify.com/login` → back | Signed in with Spotify credentials; playback works (spike #1) |
| Discord (`discord`) | inside `discord.com/app` | Signed in |
| Facebook (`meta`) | `facebook.com` → `/checkpoint/…` → `/auth_platform/afad/` → home | Facebook's new-device checkpoint, completed in the app, both times |

Other observations:

- Google offered passkey enrollment. It was skipped, so WebAuthn was not exercised.
- YouTube and Facebook asked for the Notifications permission. P2 bridges these requests through `PermissionRequested`.

### Sessions across restarts

- **First session (12:35–13:01, 11 windows, about 25 minutes):**
  - At the next start, Google, Spotify and Facebook were signed out. Not a single cookie set during that session was left in any of the four profiles: neither the auth cookies nor tracking cookies such as `datr`, `sp_t` and `__dcfduid`.
  - Cookies set by an earlier scripted run in the same `google` profile were still there.
  - Discord stayed signed in, because its token lives in localStorage, which survived.
- **Cookie probes:** a one-year cookie set from a lab page survived a clean exit, a window close, a host kill, and a hard kill of the whole WebView2 tree 55 s after it was set. How the host shut down does not explain the loss.
- **Controlled re-test** (Chromium logging on: sign in, wait, close with the window's X): the auth cookies were on disk right after closing: Google `SID`/`SAPISID`/`__Secure-*PSID`, Spotify `sp_dc`/`sp_key`, Facebook `c_user`/`xs`. On restart, `--check-signed-in` found **Gmail, YouTube, Spotify, Facebook and Discord all signed in**. Gmail was still signed in after a reload with CDP capture on (spike #4). The Chromium log shows no cookie-store or decryption errors.
- **One clue:** in the first session the `google` profile wrote nothing to its preferences diagnostics. The other profiles logged every 10 s. Not enough to name a cause.

### Not tested

Spotify's "Continue with Google / Facebook / Apple" buttons were not tried; the maintainer used Spotify credentials. `spotify`'s `navAllow` lists only Spotify's domains, so if those buttons move the main frame to `accounts.google.com`, `facebook.com` or `appleid.apple.com`, the product would hand them to the default browser and sign-in would fail. They must be tested in P2 before the entry's lists are final ([WEBAPP-CATALOG](../WEBAPP-CATALOG.md#2-review-rules-for-new-entries-pr-checklist) §2).

## Consequences

- **Google's account domains on every country-code TLD.** Google lists them at `https://www.google.com/supported_domains`. Enumerating about 190 domains in each `google` entry would be unreadable. The proposal is a catalog schema v2 field, `navAllowSets: ["google-account-domains"]`, which the app resolves from a list it bundles. A narrower rule in code would only allow `accounts.google.<ccTLD>/accounts/SetSID`. Either way this changes the catalog or its schema, so it is decided in P2 with the maintainer's approval.
- **Sessions must be checked, not assumed.** The P2 exit criterion "stays signed in across hibernation" makes the unexplained loss a real risk:
  - P2 dogfoods with Chromium logging available;
  - the web runner records whether each app comes back signed in after a restart. The `--check-signed-in` probes become a debug-only health check;
  - the bug report includes per-profile cookie-store counts, never cookie values.
  - If the loss comes back, it gets investigated before 0.2.0.
- **Default user agent only:** this works today, and there is no spoofing. Because client hints name WebView2, Google could block it later. The roadmap's mitigation still applies: document it as an upstream limitation.
- **New-device flows** (Facebook's checkpoint, Google's prompts including the selfie check) need full in-app navigation within the entry's domains, which works.

## Reproduce

On `spike/p0`:

```powershell
dotnet run --project spikes/WebLab -- "--wv2-args=--enable-logging --v=0"   # sign in by hand, then close
dotnet run --project spikes/WebLab -- --check-signed-in                      # SIGNED-IN lines in weblab\*.log
dotnet run --project spikes/WebLab -- --cookie-set=probe1                    # then --cookie-check
```
