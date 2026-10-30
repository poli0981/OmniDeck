# OmniDeck P0 spike harnesses

This is the `spike/p0` branch: throwaway proof-of-concept code for the P0 spikes. It is pushed but
**never merged**. Notes and decisions live on `main` in `docs/spikes/`, and each note names the commit
of this branch that it measured.

- Build: `dotnet build spikes/OmniDeck.Spikes.slnx`. The solution is separate from `OmniDeck.slnx`,
  and `spikes/Directory.Build.props` replaces the repository's analyzer set.
- Data: `%LocalAppData%\OmniDeck.Spikes\` (WebView2 profiles, CSV samples, raw responses, logs). Never
  in the working tree.
- Secrets: environment variables only.

| Harness | Spikes | Run |
|---|---|---|
| `ProviderProbe` | #7 providers | `dotnet run --project spikes/ProviderProbe -- stooq` · `-- twelvedata` (needs `OD_TWELVEDATA_KEY`) · `-- verify-models` (frozen models vs. saved responses) |
| `ThemeLab` | #5 preset swap | `dotnet run --project spikes/ThemeLab` (interactive) · `-- --auto` (measurements → `theme\*.log`) |
| `WebLab` | #1 DRM · #2 sign-in · #3 memory · #4 CDP | `dotnet run --project spikes/WebLab` (interactive) · `-- --auto` (EME, CDP and a short ladder) · `-- --wv2-args=...` (browser arguments) |

WebLab keeps WebView2 profiles in `%LocalAppData%\OmniDeck.Spikes\WebView2` (one profile per catalog
`profile` group, so sign-ins persist across runs), memory samples in `memory\*.csv` (one row per process
per 2 s sample, with the current phase) and its log in `weblab\*.log`. Sign-ins are done by hand in its
window; the lab never touches the user agent.
