# Known limitations and remaining work

Phase 2 is runnable, not a complete macOS feature-equivalent port.
See [PHASE2_REPORT](PHASE2_REPORT.md) for current evidence and acceptance steps.

* No global raw Precision Touchpad capture. Windows owns exclusive PTP HID
  collections. Mouse hooks are not a workaround for raw multi-touch.
* Native input is experimental, foreground-only and opt-in. WM_POINTER is manipulation-only.
  No physical sample stream was tested. Windows may deliver coalesced or
  synthesized initial frames; this provider processes current frames rather
  than full history. Single tap/hold native acquisition is not promised.
* TouchpadGesturesController is integrated for foreground three/four-finger taps
  and manipulations; activation succeeded, physical delivery is pending.
  PhysicalGestureRecognizer is still a probe. Background use needs Windows shortcuts.
* The reusable engine supports sequences and delayed tap dispatch, but the
  settings schema does not yet bind custom multi-step sequences. The simulation
  laboratory never performs actions. Native routing currently accepts swipes.
* No Force Touch, pressure tiers, calibrated force graph, haptic actuator output,
  raw edge control surfaces, rotary knob or seek surface. Controls are disabled
  with a reason. Null optional sensor fields are intentional.
* Menus have a visual tree editor and up/down reorder. Advanced sensitivity uses
  JSON; drag reorder and dedicated sensitivity sliders remain future work.
* Profile identity uses executable names rather than authenticated full paths.
  All enabled profile hotkeys reserve their chords globally. Pausing stops
  dispatch but retains chord registration. Avoid common app shortcuts.
* Window tiling automatically selects eligible visible, non-minimized windows
  on the target monitor, in enumeration/Z order. It has no picker or undo yet.
  Minimized targets can be restored; minimized secondary candidates are skipped.
  Apps with minimum sizes, custom frames or restrictions can resist placement.
  Multi-window layouts and mixed-DPI/multi-monitor behavior were not physically
  tested. Monitor transfer uses Win+Shift+Right rather than raw window migration.
* Elevated target windows may reject activation/SendInput through UIPI. The app
  does not elevate or disable security protections to bypass this.
* Keyboard shortcuts use virtual keys; international layouts/AltGr need more
  testing. Modifier release waiting prevents common activation-chord leakage,
  but concurrent physical input cannot be made fully atomic with SendInput.
* Media sessions depend on player support; key fallback and volume/mute were
  not exercised against live playback. Now-playing may be unavailable.
* Brightness uses OEM WMI. No external-monitor DDC/CI control. Lock, brightness,
  screenshot and desktop effects were not triggered during development tests.
* Launcher allows EXE path/name without argument strings and HTTP(S) URLs.
  It does not index installed apps, import pinned apps or execute shell scripts.
* No installer, signing, automatic updater, startup registration, telemetry,
  network service or driver. Distribution is a whole self-contained folder.
* Diagnostics inventory can identify generic device labels without a vendor;
  HID report descriptors/pressure usages and actual haptic devices are not read.
* Accessibility/DPI/keyboard behavior has only limited local inspection. No
  multi-hour soak, second-machine test, performance benchmark or commercial
  usability acceptance was completed.
