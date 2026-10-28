# CLAUDE.md

> Ngữ cảnh + luật bắt buộc cho AI coding agent (Claude Code) khi làm việc trong repo này. Đọc trước khi sinh/sửa code. Tài liệu chi tiết nằm trong `docs/` (xem mục 8).

## 1. Dự án là gì

**OmniDeck** — ứng dụng desktop **Windows 11** (24H2+, x64) gom các mini-app native (đồng hồ, hẹn giờ, máy tính, thời tiết, lịch, chứng khoán, media player) và các web app tuyển chọn (Spotify, Discord, YouTube, Google…, chạy trên WebView2) vào một cửa sổ, với **RAM governor** tự suspend/hibernate module.

- Open-source **GPL-3.0**; cài & cập nhật bằng **Velopack** (per-user, không admin).
- Trạng thái: **pre-alpha** — kickoff 28/10/2026, P0 từ 02/11/2026, v1.0 ~23/05/2027 (`docs/ROADMAP.md`).

## 2. Luật vàng (KHÔNG được vi phạm)

1. **Không telemetry.** Mạng chỉ dùng cho các tính năng liệt kê trong `docs/legal/PRIVACY.md`.
2. **Chỉ catalog được duyệt**, không URL tùy ý. `catalog/webapps.json` trên `main` là **cấu hình live** của mọi bản cài (app tải raw URL mỗi ngày) → mọi merge là production; luôn chạy `pwsh scripts/validate-catalog.ps1`.
3. **Không bao giờ đòi quyền admin** (`app.manifest` = `asInvoker`).
4. **Secret duy nhất** (Twelve Data key) mã hoá DPAPI; không log, không plaintext.
5. **Dữ liệu app chỉ ở `%LocalAppData%\OmniDeck.Data`.** Tuyệt đối không ghi vào `%LocalAppData%\OmniDeck` — thư mục cài của Velopack, bị xoá sạch khi gỡ hoặc cài lại.
6. **UI dùng token `Od.*`**: không hardcode FontSize/padding/radius/thời lượng animation trong view module (CI chặn `FontSize=` literal).
7. **Chỉ Windows 11.** Không thêm nhánh tương thích Windows 10.
8. **CI tự chứa trong repo:** không gọi `uses: poli0981/.github/...`; mọi action **pin SHA**; không `paths:` filter cho workflow có required check; đổi tên job required → sửa ruleset `main` trước.
9. **Hỏi người dùng trước khi sửa** `LICENSE`, `docs/legal/**`, `catalog/webapps.json`, `.claude/settings.json`. `.claude/settings.json` ép bằng rule `ask`, nhưng lệnh Bash/PowerShell ghi file **không** bị chặn → phải tự giác; lớp cứng là ruleset GitHub.
10. **Chưa ký Authenticode** — đừng thêm bước ký vào build/CI. Attestation, SBOM, checksums và kiểm tra tag ký trong `release.yml` phải giữ. Không obfuscate.

## 3. Tech stack

- **.NET 10 (LTS), C# 14**, SDK ghim trong `global.json` · **WPF** + **WPF-UI 4.3** (Fluent; quy ước ở `docs/WPFUI-CONVENTIONS.md`)
- **Velopack** 1.2 — `vpk` là local tool trong `.config/dotnet-tools.json`, luôn cùng version với thư viện
- Dự kiến từ P1: CommunityToolkit.Mvvm, M.E.DependencyInjection, Serilog, SQLite + Dapper, WebView2 SDK, LibVLCSharp, ScottPlot, Ical.Net, H.NotifyIcon (bảng version ở `docs/SPEC.md` §7)
- Test: **xUnit v3** (`xunit.v3.mtp-off`, chạy qua VSTest)
- Analyzer (warnings = errors): .NET analyzers latest, **SonarAnalyzer.CSharp**, **Roslynator**, **Meziantou**, **ReferenceTrimmer** — khai báo trong `Directory.Build.props`
- i18n: `.resx` EN (neutral) / VI / JA

## 4. Bố cục

```
OmniDeck.slnx · Directory.Build.props · global.json · .config/dotnet-tools.json · Settings.XamlStyler
src/OmniDeck.App/      WPF shell — exe OmniDeck.exe; Program.Main (Velopack) → OdApp (OdApp.xaml)
src/OmniDeck.Core/     contracts, governor, storage, services (hiện có OdVersionInfo)
src/OmniDeck.Modules/  mini-app (tạo ở P1) · src/OmniDeck.Web/ web runner (tạo ở P2)
tests/OmniDeck.Tests/  xUnit v3
catalog/               webapps.json + webapps.schema.json
scripts/               pack.ps1 (publish + vpk pack), validate-catalog.ps1
```

## 5. Lệnh

```powershell
dotnet tool restore
dotnet build OmniDeck.slnx -c Release               # 0 warning (warnings = errors)
dotnet test OmniDeck.slnx -c Release
dotnet format OmniDeck.slnx --verify-no-changes
dotnet xstyler -c Settings.XamlStyler -d src -r -p  # bỏ -p để tự định dạng
pwsh scripts/validate-catalog.ps1
pwsh scripts/pack.ps1 -Version 0.0.1-local          # publish + Velopack → Releases/ (Velopack cần ≥ 0.0.1)
```

Repo dùng CRLF (`.gitattributes`, `.editorconfig`); file do tool tạo có thể là LF → chạy `unix2dos` cho `.cs`/`.xaml` trước `dotnet format`. XamlStyler ghi XAML kèm BOM (`.editorconfig` khai báo `charset = utf-8-bom` cho `*.xaml`).

## 6. Quy ước code

- Namespace `OmniDeck.*`, type public tiền tố `Od*`, interface `IOd*`; lớp ứng dụng là `OdApp` (tên `App` vi phạm MA0049).
- Nullable bật; file-scoped namespace; method trả awaitable kết thúc bằng `Async` (MA0137/MA0138).
- Version package nằm trong **từng `.csproj`** (không Central Package Management — dependency graph của GitHub không đọc `Directory.Packages.props`); analyzer nằm trong `Directory.Build.props`.
- Chuỗi hiển thị cho người dùng: `.resx`, không nối các mảnh chuỗi đã bản địa hoá.
- `pack.ps1` chỉ truyền `-p:Version` (truyền `FileVersion` với version có hậu tố gây CS7035); SDK tự gắn `+<sha>` vào InformationalVersion và `OdVersionInfo` đọc ra cho About.
- Bản self-contained phải mang theo notices: `docs/legal/*.md` + `LICENSE` và notices của runtime (target `OdCopyRuntimeNotices`). Thêm package bundle mới → cập nhật `docs/legal/THIRD-PARTY-NOTICES.md` (cần hỏi người dùng, xem luật 9).

## 7. Quy trình làm việc

1. Lên kế hoạch → người dùng duyệt → làm **liên tục**, không dừng hỏi từng bước trong phạm vi đã duyệt.
2. Làm ấm GPG một lần (pinentry, cache 8 giờ); mọi commit/tag phải **ký** (`commit.gpgsign`/`tag.gpgsign` đã bật).
3. Nhánh `feat/ fix/ docs/ chore/ ci/` → commit → push → PR (bật Auto-fix) → check xanh → `gh pr merge --merge`.
4. Release: PR cập nhật `CHANGELOG.md` + `<Version>` → merge → `git tag -s vX.Y.Z` trên `main` → push tag → `release.yml` (chi tiết ở `RELEASING.md`). Mọi bản < 1.0.0 là **pre-release**.

Required checks trên `main`: `build`, `package`, `catalog`, `lint`, `Analyze (csharp)`, `Analyze (actions)`, `dependency-review`, kèm luật code scanning (CodeQL). SonarCloud báo Quality Gate trên PR nhưng không bắt buộc.

## 8. Chỉ mục tài liệu

| File | Nội dung |
|---|---|
| `docs/SPEC.md` | Đặc tả chức năng, version tham chiếu |
| `docs/ARCHITECTURE.md` | Bố cục solution, module contract, web runner, storage, release |
| `docs/RAM-GOVERNOR.md` | State ladder, cap, pinning, metric bộ nhớ |
| `docs/UI-MODES.md` | Token `Od.*`, 3 preset |
| `docs/WPFUI-CONVENTIONS.md` | Cách dùng WPF-UI |
| `docs/WEBAPP-CATALOG.md` | Schema + luật review catalog |
| `docs/ROADMAP.md` | Phase P0–P4, version, rủi ro |
| `docs/legal/*` | EULA, Disclaimer, Privacy, Third-party notices (ship trong app) |
| `README.md`, `CONTRIBUTING.md`, `SECURITY.md`, `RELEASING.md`, `CHANGELOG.md` | Tài liệu công khai |

**Liên hệ (@poli0981.dev):** `security@` (lỗ hổng, cùng GitHub private vulnerability reporting) · `privacy@` · `legal@` · `copyright@` (license/attribution) · `takedown@` (gỡ entry catalog/thương hiệu) · `code@` (đóng góp, Code of Conduct). Không dùng `games@`, `sponsor@`, `collab@`, `press@`, `dmca@` cho dự án này. Cộng đồng: GitHub Discussions + Discord `https://discord.gg/u8tn3YypyX`.
