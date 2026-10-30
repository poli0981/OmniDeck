# OmniDeck P0 spike harnesses

This is the `spike/p0` branch: throwaway proof-of-concept code for the P0 spikes. It is pushed but
**never merged**. Notes and decisions live on `main` in `docs/spikes/`, and each note names the commit
of this branch that it measured.

- Build: `dotnet build spikes/OmniDeck.Spikes.slnx`. The solution is separate from `OmniDeck.slnx`,
  and `spikes/Directory.Build.props` replaces the repository's analyzer set.
- Data: `%LocalAppData%\OmniDeck.Spikes\` (WebView2 profiles, CSV samples, raw responses). Never in
  the working tree.
- Secrets: environment variables only.

| Harness | Spikes | Run |
|---|---|---|
| `ProviderProbe` | #7 providers | `dotnet run --project spikes/ProviderProbe -- stooq` · `-- twelvedata` (needs `OD_TWELVEDATA_KEY`) |
