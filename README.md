<p align="center">
  <img src="docs/assets/logo.png" width="96" alt="GestureSign V2 Logo">
</p>

<h1 align="center">GestureSign V2 — English</h1>

<p align="center">
  A touchpad / mouse gesture tool re-polished for Windows 11.
</p>

<p align="center">
  <img alt="Windows 11" src="https://img.shields.io/badge/Windows-11-0078D4?style=flat-square">
  <img alt="WinUI 3" src="https://img.shields.io/badge/UI-WinUI%203-0078D4?style=flat-square">
  <img alt="Platform" src="https://img.shields.io/badge/Platform-x64-555?style=flat-square">
  <img alt="Fork" src="https://img.shields.io/badge/fork-English--only-2ea44f?style=flat-square">
</p>

![GestureSign V2 main interface](docs/assets/screenshot-main-2026-07-02.png)

> **English-only fork** of [Tomclanc/GestureSignv2](https://github.com/Tomclanc/GestureSignv2).
> The interface is forced to English regardless of system locale, and the non-English
> language resources have been removed. It is built from source (see
> [Getting the English build](#getting-the-english-build)) rather than the upstream
> winget/MSI packages, which ship the multi-language version.

## Overview

GestureSign V2 is a Windows 11 focused rebuild of the classic open-source project
[TransposonY/GestureSign](https://github.com/TransposonY/GestureSign).

The original GestureSign has not been actively maintained for a long time. On newer
Windows systems, users may run into sticky modifier keys, dated UI behavior, DPI issues,
and inconsistent gesture capture. GestureSign V2 keeps the original gesture workflow while
improving the Windows 11 experience and moving the configuration interface to a modern
WinUI 3 design. This fork additionally ships an English-only interface.

## Features

- Rebuilt WinUI 3 interface with Windows 11 rounded corners, Mica styling, and light / dark theme support.
- Touchpad, touchscreen, and mouse gestures with gesture trails and thumbnail previews.
- Quick Actions page with Kando radial menus and a dedicated hotkey trigger (see notes below).
- Edge Interaction page for touchpad and touchscreen edge taps and edge swipes.
- Edge gestures can also be added to regular app groups; app-specific actions take priority and fall back to global actions when no executable app action is found.
- Per-app actions with matching by executable, window class, title, and groups.
- Common commands such as hotkeys, browser actions, window actions, media controls, system operations, file launching, volume, brightness, and command execution. New actions can include their initial command directly from the add-action dialog.
- Ignore list support for excluding specific apps, windows, or matching rules.
- Options to prefer Windows touchpad gestures or built-in browser gestures, with fullscreen exclusions.
- Optional OneDrive sync stores configuration under `OneDrive\Apps\GestureSign V2` and lets OneDrive handle cross-device synchronization.
- Tray icon, tray menu, single-instance startup, readable gesture logs, and one-click pause/resume from the tray.
- **English-only user interface.**
- Improved UI and input behavior for high-DPI and high-refresh-rate displays.

## Getting the English build

This fork does not ship its own installer. There are two ways to get the English-only build:

- **GitHub Actions artifacts** — every push to the `english-only` branch builds both apps.
  Open the [Actions](../../actions) tab, pick the latest successful run, and download either:
  - `GestureSign-classic-en-Release` — the gesture **engine** (`GestureSign.exe`) plus the classic
    Control Panel (`GestureSign.ControlPanel.exe`) and plugins. This is the functional app.
  - `GestureSign-WinUI-en-win-x64` — the modern **WinUI 3** front-end (self-contained), an
    optional alternate configuration UI.
- **Build from source** — the CI steps live in [`.github/workflows/build-english.yml`](.github/workflows/build-english.yml).
  Locally you need Visual Studio 2022 (MSBuild), the .NET Framework 4.8.1 developer pack, and the
  .NET 8 SDK:
  - Classic: `nuget restore GestureSign.sln` then `msbuild GestureSign.sln /p:Configuration=Release`.
  - WinUI: `dotnet publish GestureSign.WinUI/GestureSign.WinUI.csproj -c Release -r win-x64 --self-contained true`.

The **engine** (`GestureSign.exe`) performs gesture recognition and runs in the tray; the Control
Panel or the WinUI app is only the configuration UI. Extract a build to a permanent folder, run
`GestureSign.exe` to start the engine, and use the Control Panel or WinUI app to configure.

> For the official multi-language release with an installer and winget package, use the
> [upstream project](https://github.com/Tomclanc/GestureSignv2).

## Notes for this fork

- The interface is English only; the language selector no longer changes the language.
- The **Kando radial menu is not bundled** in this fork's builds (it is an external component that
  is not part of the repository), so the Quick Actions radial menu will not launch. Everything else
  works normally.
- Configuration files: `%AppData%\GestureSign V2` (or `OneDrive\Apps\GestureSign V2` with OneDrive sync).
- Log files: `%LocalAppData%\GestureSign V2`.

## Quick start

1. Run `GestureSign.exe` to start the engine (a tray icon appears).
2. Open the configuration UI (WinUI app or classic Control Panel) and make sure gesture recognition is enabled.
3. Select Global Actions or an app group.
4. Click New Action and record or draw a gesture pattern.
5. Set a command and bind the gesture to a hotkey, browser action, window action, or system command.
6. Return to the desktop or target app and trigger the gesture.

If an app already has system-level or built-in gestures — such as Windows 11 touchpad gestures or
Microsoft Edge mouse gestures — you can enable the related preference options on the Options page.

## Pages

- **Actions** — Manage global actions, app actions, groups, gestures, and commands.
- **Ignore** — Exclude apps, windows, or matching rules from gesture recognition.
- **Gestures** — View, import, export, retrain, and organize the gesture library.
- **Quick Actions** — Select Kando menus, sync hotkeys, open Kando settings, or test the radial menu (requires Kando, not bundled here).
- **Edge Interaction** — Configure touchpad and touchscreen edge taps and edge swipes.
- **Options** — Adjust trail color, width, opacity, input devices, fullscreen exclusions, and startup behavior.
- **About** — View the version, project links, logs, and maintenance information.

## Compatibility

- Recommended OS: Windows 11 x64.
- Windows 10 may run some features, but Windows 11 is the primary target.

## Feedback

When reporting gesture, recording, saving, or UI issues, please include:

- Windows version and display scaling.
- Whether you are using mouse gestures or touchpad gestures.
- Target app name and whether it is fullscreen.
- Logs from the About page.
- Screenshots or reproduction steps.

## Credits

Thanks to [TransposonY/GestureSign](https://github.com/TransposonY/GestureSign), HighSign,
MahApps.Metro, WGestures, and the projects this work builds on, and to
[Tomclanc/GestureSignv2](https://github.com/Tomclanc/GestureSignv2) for the Windows 11 rebuild this
fork is based on.

The Quick Actions feature integrates the radial menu experience from
[Kando](https://github.com/kando-menu/kando), an independent open-source project under the MIT
License. Kando is not redistributed in this fork's builds.
