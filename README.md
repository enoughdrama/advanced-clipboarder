# Advanced Clipboarder

A fast, keyboard-first clipboard manager for Windows. everything local.

<p align="center">
  <img src="preview.svg" alt="Advanced Clipboarder animated preview" width="100%">
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET 8">
  <img src="https://img.shields.io/badge/Windows-10%20%2F%2011-0078D6?style=for-the-badge&logo=windows&logoColor=white" alt="Windows 10/11">
  <img src="https://img.shields.io/badge/license-MIT-green?style=for-the-badge" alt="MIT license">
</p>

---

## What it does

Every time you copy something, Advanced Clipboarder snapshots it — text, code, links, images, colors, files — then lets you search, pin, transform, and paste anything back with a global hotkey. Nothing leaves your machine.

## Features

- **Universal capture** — text, rich text, images (PNG/JPG), file drops, colors (HEX / RGB / HSL / OKLCH), URLs, code snippets
- **Smart typing** — auto-detects kind of content (email, link, 2FA code, code snippet per language, CSS Color 4 formats)
- **Paste transforms** — case conversions, Base64 / URL / HTML encode-decode, JSON prettify/minify, Unix ↔ date, smart-quotes → ASCII, sort / reverse / dedup lines — filtered per clip type so you never see JSON on a URL
- **Color format swap** — paste a `#FF00AA` clip as `rgb(...)` / `hsl(...)` / `oklch(...)` — OKLab math from the Björn Ottosson reference
- **Pinned templates with variables** — pin a text clip containing `{date}`, `{time}`, `{uuid}`, `{clipboard}`, or `{input:Label}`; placeholders are substituted on paste
- **Global hotkey** — `Ctrl+Shift+V` pops the window over any app
- **Instant search** — press `/` to focus, queries match type, source, and content
- **Categories** — All · Pinned · Text · Code · Links · Images · Colors · Files
- **Pin what matters** — pinned items stay on top forever across sessions
- **Paste-at-caret** — `Enter` pastes directly into the app you came from
- **Right-click transforms** — any card's context menu is the categorised transform picker
- **Settings UI** — retention, blocked processes, and content-pattern regexes all editable in-app with a live match tester
- **Grouped timeline** — Last hour / Today / Yesterday / Earlier
- **Live refresh** — new clips land with a subtle green flash
- **Encrypted history** — on-disk store is sealed with Windows DPAPI (per-user); other local users can't read it
- **Password-manager aware** — clips from KeePass, 1Password, Bitwarden, LastPass, Dashlane, Enpass, RoboForm, NordPass, Keeper, ProtonPass, Psono are skipped at the source
- **Content filters** — credit-card-shaped strings are blocked by regex before they touch history (user-extendable, developer tokens left alone on purpose)
- **Auto-expiring secrets** — 2FA codes wipe themselves after 60 s by default; configurable TTLs and a hard cap on non-pinned history
- **Auto-update** — checks GitHub for a newer release on startup (no throttle) and on every window open (30-min throttle); offers a silent, one-click install
- **Persistent history** — debounced writes, survives restarts
- **Single-instance + tray** — second launch just re-opens the window
- **Keyboard everything** — `Esc` to hide, `Ctrl+P` to pin, `Ctrl+T` to transform, `Ctrl+,` for settings

## Keyboard shortcuts

| Shortcut         | Action                                         |
| ---------------- | ---------------------------------------------- |
| `Ctrl+Shift+V`   | Show / hide the window (global)                |
| `/`              | Focus search                                   |
| `Esc`            | Hide window / clear search                     |
| `Enter`          | Paste selected item (renders template if any)  |
| `Ctrl+P`         | Pin / unpin the top card                       |
| `Ctrl+T`         | Open transform menu for the top card           |
| `Ctrl+F`         | Focus search                                   |
| `Ctrl+,`         | Open Settings                                  |
| `Right-click`    | Open the transform menu for any card           |

## Paste transforms

Hit `Ctrl+T` on the top card, click the ⇆ button on any card, or right-click it. A categorised menu opens with only the conversions that make sense for that clip type:

| Category    | Entries                                                           | Applies to                    |
| ----------- | ----------------------------------------------------------------- | ----------------------------- |
| **Case**    | UPPER / lower / Title / camelCase / PascalCase / snake_case / kebab-case | Text, Code (identifier-style for code only) |
| **Encode**  | Base64, URL encode                                                | Text, Code (URL also for Email, Link) |
| **Decode**  | Base64, URL, HTML                                                 | HTML decode only on `html` / `xml` code |
| **Format**  | JSON prettify / minify                                            | Code clips detected as JSON only |
| **Time**    | Unix ↔ date                                                       | Text, Code                    |
| **Clean**   | Trim, normalize whitespace, smart quotes → ASCII                  | Text (Trim universal)         |
| **Convert** | Decimal ↔ hex                                                     | Code                          |
| **Lines**   | Sort, reverse, dedup                                              | Text, Code                    |

The original clip in history is never mutated — only the pasted payload is transformed.

## Pinned templates

Any **pinned** text / code / email clip whose content contains `{token}` placeholders renders through `TemplateEngine` before paste. Template clips show a `TMPL` badge.

| Token                | Replaced with                                         |
| -------------------- | ----------------------------------------------------- |
| `{date}`             | today's local date (`yyyy-MM-dd`)                     |
| `{date:<fmt>}`       | `DateTime.Now.ToString(fmt)` — e.g. `{date:MMM d yyyy}` |
| `{time}`             | current local time (`HH:mm`)                          |
| `{datetime}`         | local now (`yyyy-MM-dd HH:mm:ss`)                     |
| `{utc}`              | UTC now                                               |
| `{iso}`              | ISO 8601                                              |
| `{uuid}` / `{guid}`  | fresh `Guid.NewGuid()`                                |
| `{clipboard}`        | current system clipboard text                         |
| `{input:Label}`      | opens a prompt dialog asking for "Label" at paste time |

Unknown tokens are left verbatim — so typos show up as literal `{foo}` rather than silently disappearing.

## Color format swap

Right-click (or ⇆) a color clip to paste the same color in a different format: `HEX`, `RGB`, `HSL`, or `OKLCH`. Parsing accepts every format the detector already reads (including 3- and 4-digit hex, percentages, legacy and CSS Color 4 comma-less syntax). OKLCH round-trips through linear sRGB + OKLab — see [Services/ColorFormatSwap.cs](Services/ColorFormatSwap.cs) for the matrices.

## Preview

The animated SVG above loops through the same demo as [`preview.html`](preview.html): new clip arrives → hover → copy → toast. Open the HTML file locally for a higher-fidelity version that uses the bundled Outfit font.

## Build & run

Requirements: **.NET 8 SDK**, **Windows 10 (1809+) or Windows 11**.

```bash
git clone https://github.com/enoughdrama/advanced-clipboarder.git
cd advanced-clipboarder
dotnet build -c Release
dotnet run -c Release
```

Or open `Clipboarder.sln` in Visual Studio 2022 / Rider and press F5.

### Publish a single-file exe

```bash
dotnet publish -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Output lands in `bin/Release/net8.0-windows/win-x64/publish/`.

## Architecture

```
Models/          — ClipItem, ClipCategory, SeedData (typed clipboard records)
Services/        — ClipboardMonitor, HotkeyService, HistoryStore, PasteService,
                   TrayService, CodeDetector, ColorDetector, ColorFormatSwap,
                   PasteTransforms, TemplateEngine, CaptureRules, SettingsStore,
                   UpdateService, AutoStartService, SingleInstance
ViewModels/      — MainViewModel (INotifyPropertyChanged, ICollectionView)
Styles/          — Theme.xaml (palette), Controls.xaml, Icons.xaml
Templates/       — CardTemplates.xaml + CardTemplateSelector (per-type cards)
Converters/      — HexToBrush, BoolToVisibility, ImageCaption, …
MainWindow.xaml  — layout, drag-drop, hotkeys, toast host
SettingsWindow/  — in-app editor for AppSettings with live regex tester
PromptDialog     — input collection for {input:…} tokens in templates
```

Data flow:

1. `ClipboardMonitor` listens to Windows clipboard change events (`AddClipboardFormatListener`).
2. `CaptureRules` checks the clipboard's exclusion formats (`ExcludeClipboardContentFromMonitorProcessing`, `CanIncludeInClipboardHistory`, `CanUploadToCloudClipboard`, `Clipboard Viewer Ignore`) and the foreground process name against the blocklist — password-manager clips never enter memory.
3. A `ClipItem` is built — type is inferred, language is sniffed with `CodeDetector`, color format with `ColorDetector`.
4. `MainViewModel.AddIncoming` dedupes against the last item and prepends to an `ObservableCollection`.
5. `ICollectionView` filters by category + search and groups by time bucket.
6. `HistoryStore` serialises the list to UTF-8 JSON, seals it with DPAPI (`CurrentUser` scope + domain-separation salt), and writes `%APPDATA%/Clipboarder/history.dat` atomically.

On paste (`Enter` / card click / tray → activate target app):
- **Plain clip** → clipboard set, `Ctrl+V` injected into the previous foreground window.
- **Template clip** (pinned + contains known tokens) → `TemplateEngine.Render`; `{input:…}` opens `PromptDialog` first.
- **Transform** (⇆ button / right-click / `Ctrl+T`) → `PasteTransforms.Apply` or `ColorFormatSwap.Convert` runs first; the original clip content is never mutated.

## Settings

Open with the gear icon in the top bar, the tray "Settings…" item, or `Ctrl+,`. Everything below is also editable directly in `%APPDATA%\Clipboarder\settings.json` — the UI and the JSON are the same source of truth.

| Section    | Fields                                                     |
| ---------- | ---------------------------------------------------------- |
| General    | Start with Windows                                         |
| Retention  | `TwoFactorTtlSeconds`, `UnpinnedTtlDays`, `MaxUnpinnedItems` |
| Privacy    | `BlockedProcesses`, `BlockedPatterns` + a live regex match tester |

Saves propagate to the running monitor via `SettingsWindow.SettingsSaved`, so rule changes apply immediately — no restart.

## Privacy

- **Encryption at rest.** `history.dat` is a DPAPI blob tied to your Windows account; another user on the same machine cannot read it. On first launch after upgrading from a pre-0.1.1 build, the legacy `history.json` is migrated and deleted.
- **Capture rules.** The following process name prefixes are blocked by default (case-insensitive, matched with `StartsWith` on `Process.ProcessName`):

  `KeePass`, `1Password`, `Bitwarden`, `LastPass`, `Dashlane`, `RoboForm`, `Enpass`, `NordPass`, `Keeper`, `Protonpass`, `Psono`

  Editable in Settings or directly in `%APPDATA%\Clipboarder\settings.json`:

  ```json
  {
    "BlockedProcesses": ["KeePass", "1Password", "MyCorpSecretsApp"]
  }
  ```

  Omit the key (or set it to `null`) to use the defaults; set it to `[]` to disable the blocklist entirely (exclusion-format checks still apply).

- **Content-pattern block list.** After the source-process check, the clipboard text itself is matched (trimmed, full-match) against a regex list. Defaults cover **credit-card-shaped strings only** — things like GitHub PATs, AWS keys, or SSNs are *not* in the defaults because blocking them would break normal dev workflows with silent "where did my paste go?" results. Opt in explicitly if you want them:

  ```json
  {
    "BlockedPatterns": [
      "^(?:\\d[ -]?){13,19}$",
      "^ghp_[A-Za-z0-9]{36}$",
      "^sk_live_[A-Za-z0-9]{24,}$"
    ]
  }
  ```

  Bad regexes are silently skipped — one typo won't disable the whole filter. The Settings UI has a live tester: type a string, see which patterns match before saving.

- **Retention — auto-delete.** Non-pinned items can be evicted on a schedule:

  ```json
  {
    "TwoFactorTtlSeconds": 60,
    "UnpinnedTtlDays":     0,
    "MaxUnpinnedItems":    0
  }
  ```

  * `TwoFactorTtlSeconds` — auto-detected OTP codes (`Tag="2FA"`) get wiped this fast. Default **60s**, enough to paste and forget. Set to `0` to keep forever.
  * `UnpinnedTtlDays` — hard TTL on every non-pinned item. Default `0` = keep forever.
  * `MaxUnpinnedItems` — count cap; oldest non-pinned are evicted first. Default `0` = no cap.

  Pruning runs on the same 30 s refresh tick that updates relative timestamps, so the worst case a 2FA code lingers is ~`TtlSeconds + 30 s`.

## Roadmap

- [ ] Clipboard sync across machines (optional, encrypted)
- [ ] Global search palette (Spotlight-style)
- [ ] Import/export history
- [ ] Syntax highlighting in code card previews

## License

MIT — see [LICENSE](LICENSE).
