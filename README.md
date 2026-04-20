# Advanced Clipboarder

A local, keyboard-first clipboard manager for Windows that does two things most alternatives don't:

1. **Keeps what you copy private.** DPAPI-encrypted history, password-manager and screen-capture opt-outs, content-pattern regex blocklist with a live match tester, auto-wiping 2FA codes, URL trackers stripped on paste.
2. **Transforms what you paste.** 24 text conversions filtered per clip type, color format swap between HEX / RGB / HSL / OKLCH, pinned clips that render `{date}` / `{uuid}` / `{input:Label}` templates on paste.

Nothing leaves your machine. ~40 MB self-contained exe, no Electron.

<p align="center">
  <img src="preview.svg" alt="Advanced Clipboarder animated preview" width="100%">
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET 8">
  <img src="https://img.shields.io/badge/Windows-10%20%2F%2011-0078D6?style=for-the-badge&logo=windows&logoColor=white" alt="Windows 10/11">
  <img src="https://img.shields.io/badge/license-MIT-green?style=for-the-badge" alt="MIT license">
</p>

---

## Privacy

- **Encrypted on disk.** History is sealed with Windows DPAPI scoped to your user account; another local user can't read it.
- **Password-manager aware.** Clips from KeePass, 1Password, Bitwarden, LastPass, Dashlane, Enpass, RoboForm, NordPass, Keeper, ProtonPass, Psono are skipped at the source.
- **Screen-capture opt-out.** Optional: Windows renders the app as a black rectangle in Teams / Zoom / OBS / GDI screenshots (`SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)`).
- **Content filters.** Credit-card-shaped strings blocked by default; extendable via regex with a live match tester in Settings.
- **Auto-expiring secrets.** Auto-detected 2FA codes wipe after 60 s (configurable). Global TTL + hard cap on non-pinned history.
- **URL tracker stripping.** `utm_*`, `fbclid`, `gclid`, `mc_cid`, `msclkid`, `mkt_tok`, `igshid`, `yclid` and friends removed via a `Strip trackers` transform on Link clips. Original clip in history isn't mutated — only the pasted URL is clean.

## Capture & search

- **Universal capture** — text, rich text, images, file drops, colors (HEX / RGB / HSL / OKLCH), URLs, code with language detection across 23+ languages.
- **Smart typing at capture** — email / link / 2FA / CSS-Color-4 / per-language-code auto-classified on the fly.
- **Structured search.**

  | Token           | Filters on                                          |
  | --------------- | --------------------------------------------------- |
  | `>today`        | since local midnight                                |
  | `>yesterday`    | since local midnight two days ago                   |
  | `>hour` / `>week` / `>month` | rolling window                        |
  | `>15m` / `>2h` / `>7d`       | custom rolling window                 |
  | `source:vscode` | substring match on the capture source app          |
  | `lang:py`       | exact match on detected code language               |
  | `type:url`      | exact type: text / email / code / link / image / color / file |
  | `!needle`       | excludes items containing `needle`                  |

  Combine freely: `source:chrome >yesterday github` shows everything captured from Chrome yesterday that mentions GitHub.

- **Smart hover preview.** Hovering a card pops a type-specific peek: full-size swatch + WCAG contrast ratios for colors, ~400 px image preview for screenshots, parsed host / path / query-parameter breakdown for URLs, Base64-decoded view for text that looks like Base64, a larger monospace view for code and text.

## Transform on paste

- **24 text transforms** filtered per clip type: case conversions, Base64 / URL / HTML encode–decode, JSON prettify/minify (only on JSON clips), Unix ↔ date, smart quotes → ASCII, sort / reverse / dedup lines, URL tracker stripper.
- **Color format swap** — HEX ↔ RGB ↔ HSL ↔ OKLCH. OKLab matrices from the [Björn Ottosson reference](https://bottosson.github.io/posts/oklab/).
- **Pinned templates** — `{date}`, `{time}`, `{datetime:<fmt>}`, `{utc}`, `{iso}`, `{uuid}`, `{clipboard}`, `{input:Label}` substituted on paste. `{input:…}` opens a prompt before paste.
- **Source preserved** — the clip in history is never mutated; transforms apply only to the pasted payload.

### Transforms reference

| Category    | Entries                                                                 | Applies to                              |
| ----------- | ----------------------------------------------------------------------- | --------------------------------------- |
| **Case**    | UPPER / lower / Title / camelCase / PascalCase / snake_case / kebab-case | Text, Code (identifier-style code only) |
| **Encode**  | Base64, URL encode                                                       | Text, Code (URL also for Email, Link)   |
| **Decode**  | Base64, URL, HTML                                                        | HTML decode only on `html` / `xml` code |
| **Format**  | JSON prettify / minify                                                   | Code clips detected as JSON only        |
| **Time**    | Unix ↔ date                                                              | Text, Code                              |
| **Clean**   | Trim, normalize whitespace, smart quotes → ASCII                         | Text (Trim universal)                   |
| **Convert** | Decimal ↔ hex                                                            | Code                                    |
| **Lines**   | Sort, reverse, dedup                                                     | Text, Code                              |
| **URL**     | Strip trackers                                                           | Link                                    |

### Template tokens

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

Unknown tokens are left verbatim — typos show up as literal `{foo}` rather than silently disappearing.

## Keyboard

| Shortcut                 | Action                                         |
| ------------------------ | ---------------------------------------------- |
| `Ctrl+Shift+V` (default) | Show / hide the window (global, rebindable)    |
| `/`                      | Focus search                                   |
| `Esc`                    | Hide window / clear search                     |
| `Enter`                  | Paste top item (renders template if any)       |
| `Ctrl+P`                 | Pin / unpin the top card                       |
| `Ctrl+T`                 | Open transform menu for the top card           |
| `Ctrl+F`                 | Focus search                                   |
| `Ctrl+,`                 | Open Settings                                  |
| `Right-click`            | Open the transform menu for any card           |

The global open-window hotkey is rebindable from Settings → Keyboard.

## Settings

Open with the gear icon, the tray "Settings…" item, or `Ctrl+,`. Everything below is also editable directly in `%APPDATA%\Clipboarder\settings.json` — the UI and the JSON are the same source of truth.

| Section   | Fields                                                                   |
| --------- | ------------------------------------------------------------------------ |
| General   | Start with Windows · Hide from screen capture                            |
| Keyboard  | Open-window hotkey recorder                                              |
| Retention | `TwoFactorTtlSeconds`, `UnpinnedTtlDays`, `MaxUnpinnedItems`             |
| Privacy   | `BlockedProcesses`, `BlockedPatterns` + live regex tester                |

Saves propagate to the running monitor via `SettingsWindow.SettingsSaved` — rule changes apply immediately, no restart.

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
Models/       — ClipItem, ClipCategory, SeedData
Services/     — ClipboardMonitor, HotkeyService, HistoryStore, PasteService,
                TrayService, CodeDetector, ColorDetector, ColorFormatSwap,
                PasteTransforms, TemplateEngine, CaptureRules, SettingsStore,
                UpdateService, AutoStartService, SingleInstance,
                UrlCleaner, PrivacyService, SearchParser
ViewModels/   — MainViewModel (INotifyPropertyChanged, ICollectionView)
Styles/       — Theme.xaml (palette), Controls.xaml, Icons.xaml
Templates/    — CardTemplates.xaml + CardTemplateSelector (per-type cards)
                HoverPreviewTemplates.xaml + HoverPreviewSelector
Converters/   — HexToBrush, BoolToVisibility, ImageCaption, ContrastRatio,
                UrlHost/Path/QueryParts, Base64DecodePreview, …
MainWindow    — layout, drag-drop, hotkeys, toast host
SettingsWindow — in-app editor for AppSettings with hotkey recorder + regex tester
PromptDialog  — input collection for {input:…} tokens in templates
```

Data flow:

1. `ClipboardMonitor` listens to Windows clipboard change events (`AddClipboardFormatListener`).
2. `CaptureRules` checks clipboard exclusion formats (`ExcludeClipboardContentFromMonitorProcessing`, `CanIncludeInClipboardHistory`, `CanUploadToCloudClipboard`, `Clipboard Viewer Ignore`) and the foreground process name against the blocklist — password-manager clips never enter memory.
3. A `ClipItem` is built; `CodeDetector` + `ColorDetector` classify the content.
4. `MainViewModel.AddIncoming` dedupes against the last item and prepends to an `ObservableCollection`.
5. `ICollectionView` filters by category + `SearchParser`-parsed query and groups by time bucket.
6. `HistoryStore` serialises the list to UTF-8 JSON, seals it with DPAPI (`CurrentUser` scope + domain-separation salt), and writes `%APPDATA%/Clipboarder/history.dat` atomically.

On paste (`Enter` / card click / tray → activate target app):
- **Plain clip** → clipboard set, `Ctrl+V` injected into the previous foreground window.
- **Template clip** (pinned + contains known tokens) → `TemplateEngine.Render`; `{input:…}` opens `PromptDialog` first.
- **Transform** (⇆ button / right-click / `Ctrl+T`) → `PasteTransforms.Apply` or `ColorFormatSwap.Convert` runs first; the original clip content is never mutated.

## Privacy details

`history.dat` is a DPAPI blob tied to your Windows account; another local user can't read it. On first launch after upgrading from a pre-0.1.1 build, the legacy `history.json` is migrated and deleted.

Process blocklist defaults (case-insensitive, `StartsWith` on `Process.ProcessName`):

`KeePass`, `1Password`, `Bitwarden`, `LastPass`, `Dashlane`, `RoboForm`, `Enpass`, `NordPass`, `Keeper`, `Protonpass`, `Psono`

```json
{
  "BlockedProcesses": ["KeePass", "1Password", "MyCorpSecretsApp"]
}
```

Omit the key (or set it to `null`) to use the defaults; set it to `[]` to disable the blocklist entirely (exclusion-format checks still apply).

Content-pattern defaults cover **credit-card-shaped strings only** — things like GitHub PATs, AWS keys, or SSNs are *not* in the defaults because blanket-blocking them would silently eat normal dev workflows. Opt in explicitly:

```json
{
  "BlockedPatterns": [
    "^(?:\\d[ -]?){13,19}$",
    "^ghp_[A-Za-z0-9]{36}$",
    "^sk_live_[A-Za-z0-9]{24,}$"
  ]
}
```

Bad regexes are silently skipped so one typo won't disable the whole filter. The Settings UI has a live tester — type a string, see how many patterns match before saving.

Retention — auto-delete non-pinned items:

```json
{
  "TwoFactorTtlSeconds": 60,
  "UnpinnedTtlDays":     0,
  "MaxUnpinnedItems":    0
}
```

* `TwoFactorTtlSeconds` — auto-detected OTP codes (`Tag="2FA"`) wiped this fast. Default **60 s**. `0` keeps them forever.
* `UnpinnedTtlDays` — hard TTL on every non-pinned item. `0` = keep forever.
* `MaxUnpinnedItems` — count cap; oldest non-pinned evicted first. `0` = no cap.

Pruning runs on the same 30 s refresh tick that updates relative timestamps — worst case a 2FA code lingers is `TtlSeconds + 30 s`.

## Roadmap

- [ ] Clipboard sync across machines (optional, encrypted)
- [ ] Global search palette (Spotlight-style)
- [ ] Import/export history
- [ ] Syntax highlighting in code card previews

## License

MIT — see [LICENSE](LICENSE).
