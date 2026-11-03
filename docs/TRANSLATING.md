# Translating OmniDeck

OmniDeck ships in English, Vietnamese and Japanese ([SPEC](SPEC.md) §5). This page is for translators and for anyone who adds a string.

## 1. Where the strings are

Each project that shows text has its own string table, with English as the neutral language:

| Table | English | Translations |
|---|---|---|
| The shell (`OmniDeck.App`, and the few strings `OmniDeck.Core` uses) | `src/OmniDeck.App/Resources/Strings.resx` | `Strings.vi.resx`, `Strings.ja.resx` beside it |
| The built-in modules | `src/OmniDeck.Modules/Resources/Strings.resx` | likewise |
| The web runner | `src/OmniDeck.Web/Resources/Strings.resx` | likewise |

A translation holds only the keys it translates: a key it lacks shows in English. Edit the `.resx` files in an editor that keeps them as they are (Visual Studio or Rider's resource editor, or any text editor). Read a key's `<comment>` before translating it: it says what the placeholders hold and where the text shows.

## 2. Rules

- **Placeholders.** `{0}`, `{1}`… are filled in at run time. A translation keeps every placeholder of the English text (a test checks), in whatever order the sentence needs.
- **Whole sentences.** Each key is a whole sentence or label: OmniDeck never builds a sentence out of pieces, so word order is yours. When a screen needs a sentence around a control (a repeat's interval, for one), the control's items are whole phrases instead, such as "every 2 weeks".
- **Formats.** A few keys are .NET date and time formats rather than words: `Clock_Time12` (`h:mm:ss tt`) and `Weather_DayFormat` (`ddd d`). Reorder their parts as your language writes them; the day and month names, and AM and PM, come from the user's regional format.
- **Numbers and dates.** They follow the user's regional format (Windows → Time & language → Region), not the UI language. A Vietnamese UI on an English (United States) region shows US dates: that is by design.
- **Names.** Language names are written in their own language (English, Tiếng Việt, 日本語) whatever the UI language, and are not in the string tables. Brand names are never translated: OmniDeck, Spotify, Discord, Twelve Data, Open-Meteo.

## 3. What stays in English

- The legal documents (`docs/legal`), as the app shows them: they exist in English only.
- The text of a bug report's GitHub issue, which is for the maintainer: its headings are English and its modules go by id. The bug report dialog itself is translated.
- The logs.

## 4. How the app switches language

`OdLocalization` holds the UI language. XAML binds its strings with `{res:OdLoc Key}`, and a row's buttons with `{res:OdLocRow Key, Path=…}`, so those follow a change of language at once. Text built in code follows through `OdLocalization.FollowCulture(handler)`: each view model that builds text reads it again on a change. Lists the user picks from keep their entries and change only their text (`OdChoice<T>`, `OdLocalizedOption`, `OdPresetOption`), so the selection bound to them stays. WebView2 takes the UI language when it starts: web apps follow on OmniDeck's next start.

## 5. Checks

- `OdResxKeyTests`: every key the sources use exists in English, and every English key is used.
- `OdTranslationTests`: a translation has only English keys, the English placeholders, and no empty text.
- The tests run in US English, so they pass on Vietnamese and Japanese Windows.

## 6. Vietnamese

**Voice.** Short and plain, as Windows says it in Vietnamese: labels and buttons are bare phrases ("Lưu", "Xóa nơi này"), messages are whole sentences. Address the reader as "bạn" only where English says "you", and keep "vui lòng" for the rare request. Tones go on the modern position, as Windows writes them: "hóa", "thủy", "khóa".

**Terms.** Windows' own Vietnamese terms where Windows has one.

| English | Tiếng Việt |
|---|---|
| module | mô-đun |
| web app | ứng dụng web |
| sidebar | thanh bên |
| module library | thư viện mô-đun |
| pin / unpin | ghim / bỏ ghim |
| pop out / return to main window | mở trong cửa sổ riêng / về cửa sổ chính |
| command palette | bảng lệnh |
| Settings / Appearance | Cài đặt / Giao diện |
| layout (preset): Dynamic, Standard, Comfort | bố cục: Năng động, Tiêu chuẩn, Dễ nhìn |
| theme: Light, Dark, Follow Windows | chủ đề: Sáng, Tối, Theo Windows |
| accent color | màu nhấn |
| text size | cỡ chữ |
| animations | hiệu ứng chuyển động |
| High Contrast | độ tương phản cao |
| notification area | khu vực thông báo |
| memory: suspend, hibernate, keep running | bộ nhớ: tạm dừng, ngủ đông, luôn chạy |
| sign in / sign-in data | đăng nhập / dữ liệu đăng nhập |
| permission | quyền |
| pre-release (alpha, beta) | bản thử nghiệm |
| report a bug | báo lỗi |
| check for updates | kiểm tra bản cập nhật |
| data folder | thư mục dữ liệu |
| timer, countdown, stopwatch, lap, alarm | hẹn giờ, đếm ngược, bấm giờ, vòng, báo thức |
| event, reminder, repeat | sự kiện, lời nhắc, lặp lại |
| forecast, chance of rain, feels like | dự báo, khả năng mưa, cảm giác như |
| watchlist, listing, API key, plan, credits | danh sách theo dõi, mã, khóa API, gói, lượt gọi |
| queue, playlist, subtitles | hàng chờ, danh sách phát, phụ đề |
| local time, time zone | giờ địa phương, múi giờ |

**Formats.** `Clock_Time12` stays `h:mm:ss tt` and `Weather_DayFormat` stays `ddd d`: the region gives "SA"/"CH" and "T2"…"CN".

## 7. Japanese

**Voice.** As Windows 11's Japanese UI words things: labels and buttons are short noun phrases or plain verb forms (保存, キャンセル, この場所を削除), messages are sentences in です・ます form. Prefer the kanji terms Windows uses to needless katakana. A half-width space goes between Japanese and Latin letters or numbers, as Windows writes them (Web アプリ, 5 分, API キー), but not next to Japanese punctuation. Japanese has no plurals: "every {0} weeks" is "{0} 週間ごと".

**Terms.** Windows' own Japanese terms where Windows has one.

| English | 日本語 |
|---|---|
| module | モジュール |
| web app | Web アプリ |
| sidebar | サイドバー |
| module library | モジュール ライブラリ |
| pin / unpin | ピン留めする / ピン留めを外す |
| pop out / return to main window | 別ウィンドウで開く / メイン ウィンドウに戻す |
| command palette | コマンド パレット |
| Settings / Appearance | 設定 / 外観 |
| layout (preset): Dynamic, Standard, Comfort | レイアウト: ダイナミック, 標準, ゆったり |
| theme: Light, Dark, Follow Windows | テーマ: ライト, ダーク, Windows の設定に合わせる |
| accent color | アクセント カラー |
| text size | テキストのサイズ |
| animations | アニメーション効果 |
| High Contrast | ハイ コントラスト |
| notification area | 通知領域 |
| memory: suspend, hibernate, keep running | メモリ: 一時停止, 休止, 常に実行 |
| sign in / sign-in data | サインイン / サインイン データ |
| permission | アクセス許可 |
| pre-release (alpha, beta) | プレリリース版 |
| report a bug | バグを報告 |
| check for updates | 更新プログラムを確認 |
| data folder | データ フォルダー |
| timer, countdown, stopwatch, lap, alarm | タイマー, カウントダウン, ストップウォッチ, ラップ, アラーム |
| event, reminder, repeat | 予定, 通知, 繰り返し |
| forecast, chance of rain, feels like | 予報, 降水確率, 体感温度 |
| watchlist, listing, API key, plan, credits | ウォッチリスト, 銘柄, API キー, プラン, クレジット |
| queue, playlist, subtitles | キュー, プレイリスト, 字幕 |
| local time, time zone | 現地時刻, タイム ゾーン |

**Formats.** `Clock_Time12` is `tt h:mm:ss` and `Weather_DayFormat` is `d日(ddd)`; the region gives 午前/午後 and the day names. A row's button names join with an ideographic comma: `A11y_RowAction` is `{0}、{1}`.

**Fonts.** Japanese text falls back to Yu Gothic UI, Windows' Japanese UI font (WPF's fallback for `xml:lang="ja"`); OmniDeck tags its windows with the UI language, so that kanji take their Japanese shapes rather than Chinese ones.
