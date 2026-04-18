# Advanced Clipboarder

A fast, keyboard-first clipboard manager for Windows. WPF + .NET 8, no background bloat, everything local.

<p align="center">
  <a href="preview.html">
    <img src="https://img.shields.io/badge/open-preview.html-9E7BF0?style=for-the-badge" alt="Open HTML preview">
  </a>
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET 8">
  <img src="https://img.shields.io/badge/Windows-10%20%2F%2011-0078D6?style=for-the-badge&logo=windows&logoColor=white" alt="Windows 10/11">
  <img src="https://img.shields.io/badge/license-MIT-green?style=for-the-badge" alt="MIT license">
</p>

---

## What it does

Every time you copy something, Advanced Clipboarder snapshots it — text, code, links, images, colors, files — then lets you search, pin, and paste anything back with a global hotkey. Nothing leaves your machine.

## Features

- **Universal capture** — text, rich text, images (PNG/JPG), file drops, colors (HEX), URLs, code snippets
- **Smart typing** — auto-detects kind of content (email, link, 2FA code, code snippet per language)
- **Global hotkey** — `Ctrl+Shift+V` pops the window over any app
- **Instant search** — press `/` to focus, queries match type, source, and content
- **Categories** — All · Pinned · Text · Code · Links · Images · Colors · Files
- **Pin what matters** — pinned items stay on top forever across sessions
- **Paste-at-caret** — `Enter` pastes directly into the app you came from
- **Grouped timeline** — Last hour / Today / Yesterday / Earlier
- **Live refresh** — new clips land with a subtle green flash
- **Persistent history** — debounced JSON writes, survives restarts
- **Single-instance + tray** — second launch just re-opens the window
- **Keyboard everything** — `Esc` to hide, `Ctrl+P` to pin, `Ctrl+F` to focus search
- **Native WPF, no Electron** — ~40 MB install, instant cold start

## Keyboard shortcuts

| Shortcut         | Action                          |
| ---------------- | ------------------------------- |
| `Ctrl+Shift+V`   | Show / hide the window (global) |
| `/`              | Focus search                    |
| `Esc`            | Hide window / clear search      |
| `Enter`          | Paste selected item             |
| `Ctrl+P`         | Pin / unpin selected            |
| `Ctrl+F`         | Focus search                    |

## Preview

Open [`preview.html`](preview.html) in a browser for an animated walkthrough of the interface (new clip arrival → hover → copy → toast).

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
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

Output lands in `bin/Release/net8.0-windows/win-x64/publish/`.

## Architecture

```
Models/          — ClipItem, ClipCategory, SeedData (typed clipboard records)
Services/        — ClipboardMonitor, HotkeyService, HistoryStore,
                   PasteService, TrayService, CodeDetector, SingleInstance
ViewModels/      — MainViewModel (INotifyPropertyChanged, ICollectionView)
Styles/          — Theme.xaml (palette), Controls.xaml, Icons.xaml
Templates/       — CardTemplates.xaml + CardTemplateSelector (per-type cards)
Converters/      — HexToBrush, BoolToVisibility, ImageCaption, …
MainWindow.xaml  — layout, drag-drop, hotkeys, toast host
```

Data flow:

1. `ClipboardMonitor` listens to Windows clipboard change events (`AddClipboardFormatListener`).
2. On each change, a `ClipItem` is built — type is inferred, language is sniffed with `CodeDetector`.
3. `MainViewModel.AddIncoming` dedupes against the last item and prepends to an `ObservableCollection`.
4. `ICollectionView` filters by category + search and groups by time bucket.
5. `HistoryStore` persists to `%LOCALAPPDATA%/Clipboarder/history.json` with a 500 ms debounce.

## Roadmap

- [ ] Clipboard sync across machines (optional, encrypted)
- [ ] Snippet library with template variables
- [ ] Global search palette (Spotlight-style)
- [ ] Import/export history
- [ ] Per-app capture rules (block passwords from KeePass, etc.)

## License

MIT — see [LICENSE](LICENSE).
