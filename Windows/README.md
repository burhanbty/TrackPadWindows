# TrackpadProMax for Windows — Phase 2

A .NET 10 WPF desktop utility inspired by Serhan Ensar's
[TrackpadProMax](https://github.com/SerhanEnsar/TrackpadProMax). Turkish interface,
local configuration, no telemetry, no kernel driver and no automatic startup.
This is an independent Windows adaptation, not a claim of full macOS parity.
Apache-2.0 license and upstream attribution are retained in LICENSE and NOTICE.

Phase 2 adds Liquid Glass styling with real DWM Acrylic requests and opaque
fallbacks, persistent appearance, Touchpad Laboratory, Turkish shortcut setup,
a runtime-gated foreground three/four-finger controller, a binding form and a
visual nested-menu editor. **65 automated tests pass.** API/controller activation
was verified locally; physical event delivery remains pending. See
[Phase 2 evidence and test steps](docs/PHASE2_REPORT.md).

## Run

From the repository root in PowerShell:

```powershell
.\artifacts\TrackpadProMax\TrackpadProMax.exe
```

The published `win-x64` folder is self-contained. Keep **the whole folder**
together; the EXE is not a single-file bundle. No system SDK installation or
administrator rights are needed to run it. The build is unsigned.

Open the radial menu with **Ctrl+Alt+F8**. Move between sectors with arrows or
Tab, enter with Enter/Space, use Backspace to go back and Escape to close.
Mouse hover/click works too. Select a category, then an action. Each category
has a "Modu seç" entry to persist its active binding mode. Losing focus closes
the wheel. Close/minimize the settings window to the tray; exit through the
tray menu or Settings → Uygulamadan çık.

## Build and test

Install a .NET 10 SDK, or use the workspace-local SDK in `.tools/dotnet` when
present. The development session used Microsoft SDK 10.0.403; its downloaded
archive was verified against the official SHA512. The SDK is not committed.

```powershell
.\Windows\build.ps1 -Test -Publish
```

Exit the app through Settings → Uygulamadan çık before publishing. The script
smoke-tests staging first, retains the previous complete folder under
`artifacts/backups/`, then promotes the new release.

On systems with .NET 10 installed, the equivalent standard commands are:

```powershell
dotnet build .\Windows\TrackpadProMax.Windows.sln -c Release -m:1
dotnet test .\Windows\TrackpadProMax.Windows.sln -c Release --no-build -m:1
dotnet publish .\Windows\src\TrackpadProMax.App -c Release -r win-x64 --self-contained true -o .\artifacts\TrackpadProMax
```

Build requires NuGet connectivity for xUnit, System.Management, Windows SDK
references and runtime packs. Build/publish do not install drivers. Close a
running development build before rebuilding its output folder. Windows tests
open and close a disposable WPF fixture; they do not rearrange user windows.
Core tests target plain `net10.0` and can run without Windows.

## Configure Windows touchpad gestures

1. Open Windows Settings → Bluetooth & devices → Touchpad → Advanced gestures.
2. Select a supported three/four-finger tap or swipe.
3. Select **Custom shortcut** and record a chord (for example Ctrl+Alt+F8).
4. Use the same chord in this app's Gestures page, enable the binding and save.
5. Check the footer for registration conflicts. If another app owns the chord,
   choose a different chord in both places.

Windows remains responsible for recognizing those physical gestures. This
does not give the app raw finger coordinates, pressure or contact count.
The app never changes Windows gesture settings automatically.

## Features

| Area | Implemented behavior | Validation scope |
|---|---|---|
| Dashboard/settings | Nine pages, glass controls, appearance, validation, local save, reset, tray | Nine-page smoke; local visual inspection |
| Radial wheel | Configurable entries, nested menus, modes, keyboard/mouse, animation | Opened using global chord; nested navigation via programmatic smoke |
| Hotkeys | RegisterHotKey, MOD_NOREPEAT, unregister/reconfigure, conflict reporting | Default chords registered locally; F8 wheel chord exercised |
| Launcher | Add local EXE or HTTP(S) URL, JSON remove/reorder, shortcut bindings | EXE launch verified on test fixture; URL opening not exercised |
| Window actions | Left/right, maximize/restore/minimize, 2/3/4 tiling | Single-window actions verified against real fixture geometry; multi-window tiling untested |
| Media | Windows media sessions + virtual media-key fallback; volume/mute; now-playing query | Compiled; playback/hardware behavior untested |
| System | Lock, Settings, Task Manager, screenshot/desktop shortcuts, WMI brightness | Implemented; disruptive/system effects not exercised |
| Profiles | Foreground process matching, profile-first fallback, per-mode mappings | Automated core tests |
| Gesture engine | 1–4-finger tap/double/hold/swipes, zones, cancellation, thresholds | Deterministic synthetic tests, not physical finger tests |
| Diagnostics | OS/model, PnP/HID/Raw Input inventory, API probes, local JSON export | Ran successfully on this MSI computer |
| Native touchpad input | Opt-in foreground frames + runtime-gated three/four-finger controller | Registration/controller activation checked; physical delivery pending |
| Pressure/haptics/surfaces | Explicitly unavailable in this release | No fabricated values or substituted cursor coordinates |

Profiles, bindings and nested menus have working editors; advanced sensitivity
settings use validated JSON. Menu JSON remains an optional advanced route.
Action kinds/values are documented inside the Actions page. `launch` accepts
EXE names/paths without arguments; `url` accepts HTTP(S) only. Configuration
is stored at `%LOCALAPPDATA%\TrackpadProMax\settings.json`, with a `.bak` of the
previous save. Invalid files are retained; loading falls back to defaults and
shows an error. Invalid edits are not applied to the active configuration.

## Diagnostics and simulation

Open Device Diagnostics and export JSON, or collect read-only data without
registering any hotkeys/tray icon:

```powershell
.\artifacts\TrackpadProMax\TrackpadProMax.exe --diagnostics "$PWD\artifacts\diagnostics-local.json"
```

`--diagnostics` must be followed by exactly one path argument. Reports contain
PnP identifiers; review them before sharing. Collection stays local.

Touchpad Laboratory separates native, shortcut and simulated input, with bounded
logs and guided tests. Its simulation feeds normalized contacts into the engine
and **does not execute actions**. Physical tests stay pending until matching native
input arrives. Shortcut setup requires separate user confirmation of physical
performance. Pressure is nullable and never synthesized. `--smoke` initializes all
nine pages, checks nested navigation and native/controller activation, then exits. It writes
`smoke/ui-smoke.json` next to the executable; it does not prove physical input.

See [ARCHITECTURE](docs/ARCHITECTURE.md), [COMPATIBILITY](docs/COMPATIBILITY.md),
[HARDWARE_DIAGNOSTICS](docs/HARDWARE_DIAGNOSTICS.md),
[KNOWN_LIMITATIONS](docs/KNOWN_LIMITATIONS.md) and
[DEVELOPMENT_PROGRESS](docs/DEVELOPMENT_PROGRESS.md).
