<div align="center">

# AltKey

**한국어 사용자 전용 커스터마이징 화상 키보드**

Windows 환경에서 한국어 입력을 지원하는 경량·무설치 가상 키보드. macOS 손쉬운 사용 키보드에서 영감을 받아 Windows 생태계에 맞게 재설계되었다.

[![Release](https://img.shields.io/github/v/release/CrowKing63/AltKey?style=flat-square&color=2563EB)](https://github.com/CrowKing63/AltKey/releases/latest)
[![License](https://img.shields.io/github/license/CrowKing63/AltKey?style=flat-square)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square)](https://dotnet.microsoft.com)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%2B-0078D4?style=flat-square)](https://www.microsoft.com/windows)

[Download](#installation) · [Features](#features) · [Screenshots](#screenshots) · [Layout Customization](#layout-customization)

</div>

---

## Overview

AltKey is a virtual keyboard that lets you fully replace your physical keyboard using only a mouse or touch input.
It enables fast, flexible text entry even without a physical keyboard or in situations where certain key inputs are inconvenient.

### Core Philosophy

| | |
|---|---|
| **Lightweight** | Single executable, ~20 MB memory at idle |
| **Portable** | No installation required; settings managed via local JSON files |
| **Beautiful** | Windows 11 Acrylic blur effects, smooth animations |
| **Extensible** | JSON-based layout editor, per-app layout profiles |

---

## Features

### Input

- **SendInput API** for precise key delivery — compatible with virtually all applications
- **Sticky Keys** — tap Shift/Ctrl/Alt/Win once to activate, twice to lock, three times to release
- **Dwell Click** — hover over a key for a set duration to trigger it automatically (assists users with tremors or motor impairments)
- **Direct Unicode input** — supports all Unicode characters including emoji

### Layouts

- **QWERTY Korean** included by default (with "가/A" toggle for English submode)
- **JSON Layout Editor** — edit key placement and actions directly in the GUI
- **Per-app layout profiles** — automatically switch layouts when specific apps come into focus
- **Instant layout switching** — change layouts with a single click from the header dropdown

### UI / UX

- **Windows 11 Acrylic blur** background (Windows 10 22H2+ supported)
- **Dark / Light / System theme** auto-applied
- **Auto opacity** — dims when idle, becomes opaque on hover
- **Screen edge snap buttons** — instantly move the keyboard to any screen edge with ← ↑ ↓ → buttons in the header
- **Free resize** — resize with aspect-ratio-locked drag handle
- **Collapse / Expand** — hide the keyboard body, leaving only the header bar
- **Tray icon** — minimizes to the system tray; toggle visibility with a global hotkey

### Extended Actions

Each key can be assigned a variety of actions:

| Action Type | Description |
|---|---|
| `SendKey` | Send a single virtual key code |
| `SendCombo` | Send a key combination (e.g. Ctrl+C) |
| `ToggleSticky` | Toggle sticky state for a modifier key |
| `SwitchLayout` | Switch to another layout |
| `RunApp` | Launch an application (path + arguments) |
| `Boilerplate` | Type boilerplate text via direct Unicode input |
| `ShellCommand` | Execute a CMD / PowerShell command |
| `VolumeControl` | Raise / lower volume or mute |
| `ClipboardPaste` | Copy text to clipboard and paste it |

### Extras

- **Key click sound** — replaceable WAV file
- **Emoji panel** — quick access to frequently used emoji
- **Clipboard history panel** — view and paste recently copied items
- **Unicode-based Korean auto-complete** — frequency-based Korean word suggestions (optional)
- **Auto-update notifications** — GitHub Releases integration with a banner for new versions
- **Run on Windows startup** — register or remove from startup via the registry
- **Restart as administrator** — for use cases requiring elevated privileges
- **OS IME Hangul emergency button** — top bar button to toggle system IME Korean/English

---

## Screenshots

> Screenshots coming soon.

---

## Installation

### Installer (Recommended)

1. Download `AltKey-Setup-x.y.z.exe` from the [latest release](https://github.com/CrowKing63/AltKey/releases/latest)
2. Run the installer

### Portable

1. Download `AltKey-Portable-x.y.z.zip` from the [latest release](https://github.com/CrowKing63/AltKey/releases/latest)
2. Extract to any folder
3. Run `AltKey.exe`

### Prerequisites

- **Windows 10 22H2 or later** (Acrylic blur recommended)
- No separate .NET runtime required (self-contained deployment)

---

## Layout Customization

Layouts are defined as JSON files in the `layouts/` folder.

```jsonc
{
  "name": "My Layout",
  "language": "ko",
  "rows": [
    {
      "keys": [
        {
          "label": "📋",
          "action": { "type": "ClipboardPaste", "text": "Frequently used phrase" },
          "width": 2.0
        },
        {
          "label": "Notepad",
          "action": { "type": "RunApp", "path": "notepad.exe" },
          "width": 2.0
        }
      ]
    }
  ]
}
```

**`width`** — Key width in units (1.0 = one standard key width)  
**`label`** — Text displayed on the key (supports emoji and Unicode)  
**`shift_label`** — Label shown when Shift is active  
**`hangul_label`** / **`hangul_shift_label`** — Secondary labels for Korean input mode

The GUI editor can be opened from Settings → **Layout Editor**.

---

## Development Setup

### Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10 or later
- Visual Studio 2022 or Rider (recommended), or VS Code + C# Dev Kit

### Build & Run

```bash
# Clone the repository
git clone https://github.com/CrowKing63/AltKey.git
cd AltKey/AltKey

# Run (development build)
dotnet run

# Release build
dotnet build AltKey/AltKey.csproj -c Release

# Publish as a single self-contained executable
dotnet publish AltKey/AltKey.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

### Automated Release (GitHub Actions)

Set the `AssemblyVersion`, then push a version tag — GitHub Actions will automatically build, package, and create a release.

```bash
git tag v1.2.3
git push origin --tags
```

```powershell
# 1. 패치 버전 업데이트 (0.1.6 -> 0.1.7)
./scripts/release.ps1 -VersionType patch

# 2. 마이너 버전 업데이트 (0.1.6 -> 0.2.0)
./scripts/release.ps1 -VersionType minor

# 3. 특정 버전 지정
./scripts/release.ps1 -CustomVersion "1.0.0"
'''

---

## Project Structure

```
AltKey/
├── App.xaml / App.xaml.cs          # App entry point, DI container
├── MainWindow.xaml / .cs           # Main window (transparent, NoActivate, Acrylic)
│
├── Views/                          # XAML views
│   ├── KeyboardView.xaml           # Keyboard UI
│   ├── SettingsView.xaml           # Settings panel
│   ├── LayoutEditorWindow.xaml     # Layout editor
│   ├── ActionBuilderView.xaml      # Action builder
│   ├── EmojiPanel.xaml             # Emoji panel
│   ├── ClipboardPanel.xaml         # Clipboard history panel
│   └── SuggestionBar.xaml          # Auto-complete suggestion bar
│
├── ViewModels/                     # MVVM ViewModels (CommunityToolkit.Mvvm)
├── Models/                         # Data models (layout & settings structures)
├── Services/                       # Core services
│   ├── InputService.cs             # SendInput wrapper, Sticky Keys, action dispatcher
│   ├── LayoutService.cs            # JSON layout loading, saving, caching
│   ├── ConfigService.cs            # Settings JSON read/write
│   ├── ProfileService.cs           # WinEventHook for foreground app detection
│   ├── ThemeService.cs             # Dark / Light / System theme application
│   ├── HotkeyService.cs            # Global hotkey registration
│   ├── StartupService.cs           # Startup registry management
│   ├── SoundService.cs             # Key click sound playback
│   ├── AutoCompleteService.cs      # Word auto-complete
│   └── UpdateService.cs            # GitHub Releases update checker
├── Controls/                       # Custom controls (KeyButton, etc.)
├── Platform/                       # Win32 P/Invoke declarations
├── Themes/                         # WPF resource dictionaries (colors, styles)
└── layouts/                        # Default layout JSON files
```

---

## Tech Stack

| Layer | Technology |
|---|---|
| Language | C# 12, .NET 8 |
| UI | WPF + WPF-UI (lepoco/wpfui) |
| MVVM | CommunityToolkit.Mvvm |
| JSON | System.Text.Json |
| Win32 | P/Invoke (direct declarations) |
| Deployment | .NET 8 Single-file self-contained |

---

## Known Limitations

| Symptom | Cause | Notes |
|---|---|---|
| Key input not delivered to elevated apps | SendInput privilege level mismatch | Use "Restart as Administrator" in Settings |
| Window covered in exclusive-fullscreen games | DirectX exclusive mode | Use in windowed-mode games |
| Acrylic blur not available on older Windows | Below Windows 10 22H2 | Falls back to solid color gracefully |

---

## License

[MIT License](LICENSE) © 2025 CrowKing63
