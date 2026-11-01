# Phase 2 — implementation and verification

Date: 2026-11-01, Europe/Istanbul. Existing .NET/WPF app extended in place.
`upstream/` unchanged. No driver/security change or automatic Windows
gesture-setting change.

## Delivered

- Shared Liquid Glass resources and controls; persistent dark/light/system theme,
  accent, transparency, animation and backdrop preferences. Version-1 settings
  retain backward compatibility, validation and atomic-save backups.
- Real DWM Desktop Acrylic requests on non-layered main/radial windows, opaque
  fallback, circular wheel region, reflections, Fluent glyphs, selection feedback
  and restrained transitions. No desktop screenshot capture/blur. Mouse, arrows,
  Tab/Shift+Tab, numbers, Enter/Space, Backspace and Escape routes retained.
- Runtime-gated foreground `TouchpadGesturesController`: three/four-finger taps
  and manipulations. Explicit session opt-in, suspended on main-window focus loss.
  Pointer callbacks share the bounded frame reader and frame-ID deduplication
  with WM_POINTER. No unrestricted background raw-input claim.
- Binding routes Automatic / Shortcut / Native. Automatic uses the configured
  shortcut if present, otherwise native input; a binding cannot dispatch through
  both routes. Native-only bindings do not reserve a dormant shortcut.
- Touchpad Laboratory: hardware/provider/API status, bounded 200-event log, actual
  device-relative contacts when delivered, contact view with stale-data cutoff,
  local JSON export and six guided tests. Cancellation is labeled system input;
  synthetic input cannot complete physical tests. Laboratory/setup observation
  suppresses mapped actions.
- Turkish setup wizard: registration/conflict verification, active mode/profile
  resolution, chord listener, separate user confirmation of physical performance.
  WM_HOTKEY alone cannot prove fingers, coordinates or physical gesture delivery.
- Binding creation form with action/value choices, EXE picker, profile/mode scope;
  visual nested-menu tree editor with add/update/remove/up/down and validated save.
  Advanced JSON remains available. Grid validation errors are reported.
- Staged self-contained publishing: smoke-test before replacement, retain entire
  prior folder in `artifacts/backups/`, promote only successful staging. No backup
  deletion. Failed smoke preserves the existing release.

## Verification

- Baseline: 48 core + 4 Windows tests passed.
- Phase 2: **59 core + 6 Windows = 65 passed**, 0 failed/skipped; Release build
  0 warnings/errors. New coverage: legacy settings, appearance save/reload/backup,
  invalid appearance, exclusive routes, dormant native chords, bounded events,
  physical-evidence provenance, real RegisterHotKey conflicts and safe disposal.
  Window actions still use disposable fixtures only. TRX files remain under tests.
- Self-contained win-x64 smoke passed nine pages, nested wheel navigation,
  shortcut registration, native registration and controller activation. A native
  GetWindowRgn/PtInRegion check confirms the wheel includes its center and excludes
  its rectangular corner; `CircularWindowRegion` is true. Capture-helper images
  include rectangular window bounds, so desktop blending still needs user visual
  acceptance under the actual Windows power/transparency policy.
  `PhysicalDeliveryVerified` is false. Evidence:
  `artifacts/TrackpadProMax/smoke/ui-smoke.json`.
- Computer Use inspected dashboard, wheel, laboratory and settings. Ctrl+Alt+F8
  produced WM_HOTKEY and displayed the wheel. Both native providers were enabled
  through the laboratory, then stopped. Zero physical frames were received;
  physical tests remained pending. An unreadable native ComboBox and truncated
  navigation label were corrected after visual inspection.
- Appearance was also saved through the UI as Light, the app exited and restarted,
  and the light dashboard was visually confirmed. Dark was restored and saved.
  There was no prior user settings file; default bindings/profiles/menus remain
  2/3/6 and the previous save is retained as `.bak`.
- Fresh diagnostic CLI exited 0 with no collector errors: MSI **Katana 17 B13VGK**,
  Windows build **26300**, HID-compatible touchpad. Local report:
  `artifacts/diagnostics-phase2.json` (ignored, contains device identifiers).
- Original working release copied before replacement:
  `artifacts/backups/TrackpadProMax-phase1-20261101-215516/TrackpadProMax/`.
  Every later publish also retains its immediate predecessor.

## Physical capability status

| Capability | Evidence |
|---|---|
| Ctrl+Alt+F8 and wheel | Keyboard-injected chord received; wheel visually inspected |
| Native pointer API registration | Succeeded locally; physical frames pending |
| Three/four-finger controller | IsSupported/create/subscribe/enable succeeded; physical events pending |
| Real count and normalized pad coordinates | Reader/visualizer implemented; no physical sample in this session |
| Physical Windows gesture → shortcut | Wizard implemented; physical acceptance pending |
| Native one-finger tap, double-tap/hold | Not supplied by implemented paths; use OS shortcut where supported |
| Background raw contacts | Unsupported through implemented standard user-mode APIs |
| Pressure, contact size/angle, haptic actuator | Hardware support unknown; unmeasured and unavailable in release |

Two-finger guided testing establishes contact delivery, not Windows scroll output.
No calibrated millimetres, default pressure values, cursor positions or synthetic
contacts are represented as hardware measurements. Native global tap events carry
a documented finger count but no coordinates.

## Kullanıcı kabul adımları

1. `artifacts/TrackpadProMax/TrackpadProMax.exe` açın; klasörü bütün olarak tutun.
2. Ctrl+Alt+F8 → ok/Tab → Enter ile alt menüye girin; Backspace geri; Esc kapatır.
3. Ayarlar → görünümü değiştirip kaydedin; “Uygulamadan çık” ile kapatıp yeniden
   açarak kalıcılığı kontrol edin. X varsayılan olarak sistem tepsisine alır.
4. Hareketler → Kolay eşleme oluştur: kapsam, giriş yolu, eylem/değer seçin.
   Kısayol Kurulumu sayfasından Windows'taki atamayı elle yapıp doğrulayın.
5. Laboratuvar → Yerel girişi aç → fiziksel testi başlatın. Pencere ön planda,
   imleç temas alanındayken hareketi yapın. Olay gelmezse test bekler; başarılı
   API kaydı olay teslimi kanıtı değildir. Sonuçları yerel dışa aktarın.
6. Testten sonra yerel girişi kapatın. Arka plan kullanımı için Windows'ta elle
   atanan kısayol yolunu kullanın.

## Limits and next priorities

Controller is an experimental Windows contract isolated in one adapter. OS power,
transparency/accessibility policy can flatten an accepted DWM request. The design
uses Windows composition materials; it does not implement Apple's renderer.
System theme is evaluated at startup/saving; live theme-change tracking is pending.
Older-Windows fallback, full accessibility, mixed-DPI/multi-monitor, performance
and soak testing remain pending. Frame history is not replayed; current frames
are read. Native double-tap/hold and force/haptics are not implemented.

Priorities: physical MSI acceptance with exported event evidence; recognition and
history reliability based on those samples; accessibility/multi-monitor checks;
live media and multi-window acceptance; signing/distribution. Pressure, haptics
and raw background acquisition require separate hardware/platform evidence.

## Primary references

- [TouchpadGesturesController](https://learn.microsoft.com/en-us/windows/win32/input-precisiontouchpad/touchpadgesturescontroller)
- [Touchpad frames and disambiguation](https://learn.microsoft.com/en-us/windows/win32/input-precisiontouchpad/getpointertouchpadinfo)
- [DWM materials](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwm_systembackdrop_type)
- [WPF backdrop implementation](https://source.dot.net/presentationframework/system/windows/Appearance/WindowBackdropManager.cs.html)
