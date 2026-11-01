# Windows API evaluation and capability matrix

Evidence collection date: 2026-10-30. API documentation can describe prerelease
features, so export/metadata discovery and successful event delivery are separate.

| Mechanism | Evaluation / implementation | Scope / constraints |
|---|---|---|
| Windows Precision Touchpad | Raw Input inventory checks digitizer page 0x0D, usage 0x05 | Inventory does not grant access to exclusive HID data |
| WM_POINTER | Experimental provider checks PT_TOUCHPAD (5) | Foreground/destination manipulation input only |
| RegisterTouchpadCapableWindow | Dynamically resolved, explicit opt-in, return value checked | Not assumed from OS build or documentation alone |
| GetPointerTouchpadInfo | Export probed | Single-pointer variant evaluated; frame variant used for acquisition |
| GetPointerFrameTouchpadInfo | Dynamically resolved; bounded 16-contact buffer | Device-relative HIMETRIC contacts; cancellation and frame deduplication |
| TouchpadGesturesController | Phase 2 adapter; IsSupported/create/enable verified | Foreground three/four-finger taps and manipulation; physical delivery pending |
| PhysicalGestureRecognizer | Runtime metadata probe | Native gesture recognizer evaluated; portable C# recognizer used instead |
| HID / Raw Input | Device inventory via GetRawInputDeviceList/GetRawInputDeviceInfo | No exclusive PTP collection opened; no unrestricted raw stream claimed |
| RegisterHotKey | Primary global fallback, MOD_NOREPEAT, registration error display | Requires manual Windows gesture-to-chord assignment; chord conflicts possible |
| Low-level mouse hooks | Evaluated, deliberately unused | Mouse hook cannot recover true contact count, pad coordinates or force |
| Windows gesture settings | Open Settings URI and explain manual configuration | Never silently modified |
| Windows.Devices.Haptics | Metadata probe only | Controller type presence does not establish a touchpad actuator |
| Windows media sessions | Now playing + play/pause/skip | App/player must publish a compatible media session; virtual key fallback |
| SendInput | Explicit configured keyboard/media actions | UIPI blocks elevated targets; OS/player may ignore accepted input |
| Monitor/window APIs | MonitorFromWindow/GetMonitorInfo/SetWindowPos/ShowWindow | Native work-area coordinates, PerMonitorV2; app minimum size constraints remain |
| WMI brightness | Explicit-action WmiMonitorBrightnessMethods call | Internal/OEM displays only where provided; external monitors often unsupported |

## Physical capabilities

| Capability | Current evidence | Fallback |
|---|---|---|
| PTP identity | HID usage collection detected locally | Manual Windows gesture shortcuts |
| Pressure / Force Touch | Unverified; no report-descriptor force access or calibration | Long-press engine on sufficient input, or global shortcut |
| Haptics | Unverified; no verified touchpad SimpleHapticsController | Animated sector highlight |
| Contact count / raw pad coordinates | Native API exports found and registration tested; physical samples untested | Shortcut input does not claim contact information |
| Contact size / angle | No verified sample | Null values; no angle/size actions |
| Surface corners and edges | Classifier tested on synthetic normalized contacts | Shortcut bindings; no mouse-cursor substitution |
| Unrestricted background raw PTP | Unsupported through implemented standard user-mode mechanisms | OS gestures -> RegisterHotKey |
| Driver integration | Interface contract only | No kernel component installed |

## Primary references

* [Precision Touchpad input](https://learn.microsoft.com/en-us/windows/win32/input-precisiontouchpad/precision-touchpad-portal)
* [RegisterTouchpadCapableWindow/Thread](https://learn.microsoft.com/en-us/windows/win32/input-precisiontouchpad/registertouchpadcapable)
* [GetPointerTouchpadInfo family](https://learn.microsoft.com/en-us/windows/win32/input-precisiontouchpad/getpointertouchpadinfo)
* [GetPointerDeviceRects](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getpointerdevicerects)
* [TouchpadGesturesController](https://learn.microsoft.com/en-us/windows/win32/input-precisiontouchpad/touchpadgesturescontroller)
* [Exclusive system HID collections](https://github.com/MicrosoftDocs/windows-driver-docs/blob/staging/windows-driver-docs-pr/hid/top-level-collections-opened-by-windows-for-system-use.md)
* [RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey)
* [SimpleHapticsController](https://learn.microsoft.com/en-us/uwp/api/windows.devices.haptics.simplehapticscontroller)

Microsoft documents that the touchpad pointer stream is produced after gesture
disambiguation and cannot be relied upon to recognize taps/holds. The app
therefore routes only swipes from its native provider; synthetic engine support
for taps/holds is not a claim about real native acquisition. Three/four-finger
global gestures in this MVP use Windows-assigned shortcuts.
