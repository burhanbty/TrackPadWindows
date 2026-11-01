# Architecture and upstream portability review

Phase 3 update: [PHASE3_REPORT](PHASE3_REPORT.md) and
[hardware investigation](PHASE3_HARDWARE.md). `SurfaceTracker` consumes only
normalized contact frames, locks an outer region contact and produces bounded
relative action requests. `SurfaceSettings` adds eight validated regions without
changing the version-1 schema. Surface sessions and the foreground providers end
on focus loss; action cancellation prevents deferred effects. Spatial taps and
background capture are not inferred from global gestures. The zone laboratory
defaults to preview; action execution is separately selected. Critical recognition
events are retained separately from the bounded high-frequency contact log.

Phase 2 update (2026-11-01): see [PHASE2_REPORT](PHASE2_REPORT.md). Shared glass
resources, laboratory evidence models and explicit input routes extend this
architecture. Runtime-gated TouchpadGesturesController callbacks feed the same
native pointer frame reader; global tap events carry a count but no coordinates.
Automatic bindings select shortcut when present, otherwise native. Native-only
bindings do not register their dormant shortcut. Laboratory/setup observation
suppresses action dispatch. Appearance is additive to the version-1 settings schema.

## Source separation and attribution

Original repository: https://github.com/SerhanEnsar/TrackpadProMax

Inspected commit: `73a322138b8cfaa09c1d3fe63c44774458917844`. The clone in
`upstream/` is unchanged. No macOS source, resources or tests were overwritten.
The Windows code resides separately in `Windows/`. LICENSE and NOTICE preserve
the Apache-2.0 attribution; adapted algorithm files also identify the source.

| Original area | Finding | Windows decision |
|---|---|---|
| TouchStream / MultitouchBridge | Private Apple MultitouchSupport callbacks and data layout | Replace acquisition; never imitate with mouse hooks |
| GestureRecognizer | Pure session state, max finger count/movement, thresholds, tap interval | Adapt session/zone logic to C#, top-left coordinates; add swipes, long press, cancellation |
| Pressure detection | Raw Apple units, click gating, tier hysteresis | Not portable without calibrated hardware evidence; no pressure actions enabled |
| GestureDispatcher | Profile/mode matching and delayed single-tap disambiguation | Configuration.Resolve, ActionDispatcher, deterministic TapDispatchGate |
| ActionPerformer | AppKit, CoreGraphics events, AppleScript and macOS helpers | Replace with Win32/SendInput, Windows media sessions and local process launching |
| Configuration | Codable bindings/modes/profiles with local storage | Typed JSON schema, defaults, validation, atomic file replacement and backup |
| ModeWheelView / RadialSelector | SwiftUI sectors; finger-relative virtual knob | WPF sector geometry and nested menus; mouse/keyboard steering instead of raw finger steering |
| TilingLayout / TilingFlow | Portable normalized 2/3/4 pane layout; macOS window selection | Same split geometry, Win32 monitor work areas and window APIs |
| LauncherApps | Dock discovery and bundle IDs | Explicit user-defined EXE/URL list; no Dock-equivalent assumptions |
| ControlSurface | Origin-locked edge bars, relative gain, session cancellation | Device-coordinate SurfaceTracker with configurable edges/corners and Windows volume/brightness/seek actions |
| NowPlaying | Private MediaRemote and AppleScript fallbacks | Windows GlobalSystemMediaTransportControlsSessionManager |
| HapticEngine | Apple private MTActuator / NSHapticFeedbackManager | Defer actuator control; use visual sector feedback |
| Pressure visualizer | Displays measured Apple pressure | No pressure graph without measured force; synthetic gesture laboratory is labeled |
| Tests | Swift Testing suites for taps, press/click intent, wheel, tiling, surfaces, config | Port applicable gesture invariants; add xUnit configuration, dispatch and Windows fixture tests |

The Windows release does not import the macOS JSON format. Its action names,
process profiles and thresholds are different. Pressure values in the original
Mac measurements must not be reused as Windows calibration.

## Projects

* **Core** (`net10.0`): immutable contact/frame/event records, pure recognizer,
  sequence matcher, delayed tap gate, configuration/profile resolution,
  shortcut parser, action interface and error-isolating dispatcher. No Win32.
* **Input**: bounded dynamic native touchpad provider, export probes,
  RegisterHotKey provider and synthetic frame provider. `IAdvancedTouchProvider`
  reserves an explicit contract for a future consented/signed driver integration.
* **Actions**: Win32 window/process operations, input synthesis, media sessions,
  OEM-dependent brightness. Returns a result instead of throwing into the UI.
* **Diagnostics**: read-only WMI, Raw Input inventory, runtime metadata and DLL
  export probes, report model with explicit evidence/status.
* **App**: WPF composition root, Turkish pages/editors, tray, optional foreground
  native input, radial overlay, serialized action execution and modifier release
  waiting. No third-party DI framework is necessary for this MVP.
* **Tests**: cross-platform xUnit core tests. **WindowsTests**: real Win32 action
  and provider tests. **TestWindow**: disposable external WPF fixture.

## Data flow

```text
Windows-configured physical gesture -> Windows shortcut -> WM_HOTKEY
    -> matching enabled profile/mode binding -> action executor -> result UI

Optional foreground touchpad WM_POINTER -> device-relative HIMETRIC contacts
    -> Surface Mode / zone test active: SurfaceTracker -> region action / preview
    -> otherwise: GestureRecognizer -> swipe binding -> action executor

Synthetic contacts -> same recognizer -> laboratory output only
```

Native calls use `ptHimetricLocationRaw` normalized by GetPointerDeviceRects;
cursor coordinates are never mapped to touch contacts. Missing optional
pressure/size/angle remain null. The provider leaves WM_POINTER unhandled so
default Windows processing remains available. Opt-in native handling can still
change foreground scrolling behavior; test it deliberately.

## Lifetime, focus and persistence

WPF owns the message hook, all hotkey registration and recognizer callbacks.
Disposal unregisters shortcuts/native capability and destroys the tray icon.
An activation chord is allowed up to three seconds to release before key/media
execution; stuck modifiers cancel the action. The last foreground external
window is captured before opening the wheel and held during selection. Keyboard
actions require successful foreground activation and recheck focus before
SendInput; elevated windows may reject it through UIPI.

Profiles match exact executable base names, case-insensitively, with a global
fallback. A matching enabled profile binding precedes general bindings.
All enabled profile shortcuts are globally reserved even when another profile
is active; choose uncommon chords. Modes filter bindings; `*` means all modes.

Saving validates before replacing the file. A same-directory temporary file
and replacement prevent partially written live JSON; a backup preserves the
previous save. This is not a crash-consistent transactional database. Failed
saves restore the last accepted in-memory configuration. No startup, registry,
driver, security or Windows gesture-setting modifications are performed.
