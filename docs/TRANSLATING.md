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
