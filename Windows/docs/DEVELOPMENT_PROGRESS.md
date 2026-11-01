# Development progress and engineering report

## Phase 2 — 2026-11-01

Delivered and validated in [PHASE2_REPORT](PHASE2_REPORT.md): shared glass UI,
foreground gesture controller, honest physical laboratory, setup wizard,
binding/menu editors, 65 passing tests and staged/backup-protected win-x64 release.
Physical event delivery remains pending. The original Phase 1 record follows.

Session date: 2026-10-30 (Europe/Istanbul).

## Scope delivered

The previously empty workspace now contains an unchanged original clone in
`upstream/` and a separate .NET 10 Windows solution. No push or commit was made.
The original repository's architecture, recognizer/dispatcher, pressure/click
logic, profiles, radial selectors, tiling, media, haptics, surfaces and tests
were reviewed. See ARCHITECTURE for the portability decisions.

Implemented projects:

* `src/TrackpadProMax.Core`: gesture engine, configuration, profiles, action
  dispatch contracts, shortcut parser, sequence matcher and delayed tap gate.
* `src/TrackpadProMax.Input`: registered hotkeys, simulation, experimental
  dynamically gated foreground native frames and future driver contract.
* `src/TrackpadProMax.Actions`: real Windows window, launch, input, media, system
  and optional brightness operations with result/error reporting.
* `src/TrackpadProMax.Diagnostics`: local hardware/API inventory and JSON report.
* `src/TrackpadProMax.App`: seven Turkish WPF pages, nested animated radial wheel,
  persistent editors, profiles, launcher, system tray and diagnostic CLI mode.
* `tests/TrackpadProMax.Tests`, `TrackpadProMax.WindowsTests` and the disposable
  `TrackpadProMax.TestWindow` fixture.

## Validation evidence

* .NET 10.0.403 SDK downloaded from Microsoft to `.tools/dotnet`, verified using
  the official SHA512. The preinstalled SDK was 8.0.422, so it was not used to
  target .NET 10. System SDK installation was not modified.
* Release solution compilation succeeded with **0 warnings and 0 errors**.
  **48 core tests + 4 Windows integration tests passed (52 total, none skipped)**.
  `build.ps1 -Test -Publish` completed successfully. Full log:
  `artifacts/validation/build-test-publish.log`; per-suite TRX files are in each
  test project's TestResults directory.
* A **self-contained win-x64 executable was generated** at
  `artifacts/TrackpadProMax/TrackpadProMax.exe`. Keep the complete publish folder.
  The final package's `--smoke` exited 0 and reported all seven pages, nested
  navigation, hotkey registration and native touchpad window registration as
  successful. Its `--diagnostics` run also exited 0. Smoke result:
  `artifacts/validation/ui-smoke.json`.
* Core tests cover 1–4-finger taps, double timing, movement cancellation, holds,
  swipe direction, all surface zones, monotonic clocks, invalid coordinates,
  profile precedence, mode/enable filtering, shortcut conflicts, serialization,
  atomic backup, sequences, delayed single taps and dispatch error isolation.
* Four Windows tests verify actual EXE launch and left/right/maximize/restore/
  minimize on a disposable test window, invalid action handling and provider
  lifecycle/probe semantics. No user windows were tiled for testing.
* UI smoke initializes all seven pages, checks wheel nested navigation, probes
  native window registration, writes results and exits without external actions.
* Interactive Computer Use inspected the dashboard and wheel. Ctrl+Alt+F8 was
  exercised and opened the wheel. The initial Ctrl+Alt+M chord conflicted with
  another program, so the default was changed and successfully registered.
* A theme application bug on the derived WPF window and a reentrant radial
  closing bug were found in runtime inspection and fixed.
* Read-only local diagnostic CLI exited 0. Firmware reports MSI Katana 17 B13VGK,
  Windows build 26300. PTP collection and the new touchpad API exports were found.
  No real finger, pressure or haptic behavior was validated.

Implemented does not mean physically verified. Playback/system actions,
multi-window tiling, monitor transfer, mixed-DPI placement and physical Windows
gesture-to-shortcut delivery still require acceptance testing.

## Next five priorities

1. Physical MSI acceptance: manual three/four-finger mappings, foreground native
   frames, cancellation/focus behavior and honest per-device capability evidence.
2. Isolated multi-window tiling and mixed-DPI/multi-monitor tests; add a window
   picker/preview and restoration instead of relying only on enumeration order.
3. Visual editors for nested menus, actions, timing/sensitivity and sequences;
   improve conflict handling and international keyboard support.
4. Verify actual media players, WMI brightness and supported device descriptors;
   implement force/haptics/surfaces only if hardware evidence supports them.
5. Accessibility and long-running stability testing, signed distribution and
   opt-in startup/installer work without silent system changes.
