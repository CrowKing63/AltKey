<div align="center">

# AltKey

**Customizable On-Screen Keyboard for Windows**

A lightweight, portable virtual keyboard for Windows, inspired by the macOS Accessibility Keyboard and reimagined for the Windows ecosystem.

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

- **QWERTY Korean / English** included by default (with Korean Hangul dual labels)
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
- **Word auto-complete** — frequency-based word suggestions (optional)
- **Auto-update notifications** — GitHub Releases integration with a banner for new versions
- **Run on Windows startup** — register or remove from startup via the registry
- **Restart as administrator** — for use cases requiring elevated privileges

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
