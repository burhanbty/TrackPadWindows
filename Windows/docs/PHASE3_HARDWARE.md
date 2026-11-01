# Phase 3 — contact acquisition and driver feasibility

Investigation date: 2026-11-01 (Europe/Istanbul). No drivers, Windows gesture
preferences or security settings were changed. No remote push was made.

## Actual local evidence

Fresh `--diagnostics` inventory: MSI Katana 17 B13VGK, Windows build 26300 x64.
The HID-compatible touchpad exposes a Digitizers / Touch Pad collection
(usage page 0x0D, usage 0x05). The local identifier is retained only in the
ignored diagnostic JSON under `artifacts/diagnostics-phase3.json`.

The new read-only HID probe performed two distinct opens:

* Metadata-only, desired access zero, shared read/write: succeeded.
  `HidD_GetPreparsedData` and `HidP_GetCaps` succeeded and reported a 30-byte
  input report on the PTP collection. This is descriptor metadata, not a contact.
* `GENERIC_READ`, shared read/write: failed with Win32 **32**
  (`ERROR_SHARING_VIOLATION`). No report was read, no feature/output report was
  written, and both handles/preparsed data are released immediately.

The metadata/report length cannot establish usable coordinates, calibrated
pressure, firmware behaviour or contact delivery. Microsoft's HID architecture
documents exclusive RIM ownership of the PTP collection and permits metadata
queries without read/write access. This matches the observed access failure.
[HID architecture](https://learn.microsoft.com/en-us/windows-hardware/drivers/hid/hid-architecture)

## Implemented foreground routes

`NativeTouchpadProvider` registers the main HWND with
`RegisterTouchpadCapableWindow`, filters WM_POINTER by `PT_TOUCHPAD`, reads
`GetPointerFrameTouchpadInfo` synchronously, and normalizes **device-relative
HIMETRIC** positions with `GetPointerDeviceRects`. Windows' pixel coordinates
are the initial cursor location and are never used for surface recognition.
Frames shared with the controller are deduplicated by frame ID. Invalid bounds,
nonfinite positions, cancellation and read failures cancel recognition.
[Frame API semantics](https://learn.microsoft.com/en-us/windows/win32/input-precisiontouchpad/getpointertouchpadinfo)

`ForegroundGestureController` checks metadata and `IsSupported`, creates a
process controller, and subscribes to three/four-finger manipulations and taps.
Pointer callbacks use the same synchronous frame reader. Global tap actions have
no contact coordinates and cannot activate a region. Background controllers are
ignored by Windows. Both adapters are disabled when focus is lost; a fresh
explicit activation is required. The app records WM_POINTER callbacks,
controller callbacks, successfully read frames, position-bearing frames and read
failures separately. Successful registration is not successful delivery.
[Controller routing](https://learn.microsoft.com/en-us/windows/win32/input-precisiontouchpad/touchpadgesturescontroller)

Windows only routes manipulation contacts after disambiguation and can synthesize
an initial down frame with a spatial/time gap. Two-finger manipulations are the
default candidate for edges; three/four fingers can be selected for the
controller route. The implemented API paths do not expose ordinary one-finger
tracking or spatial corner taps/holds. A large initial jump is cancelled; initial
travel never produces an accumulated burst of actions.

## Other user-mode approaches

User-supervised physical tests subsequently delivered actual two-finger
WM_POINTER frames, three-finger controller frames and ThreeFingerTap actions.
See [Phase 3 report](PHASE3_REPORT.md) for counts and individual action status.
This establishes usable foreground acquisition; it does not establish background
access, single-finger tracking, every region or successful action effects.

* Direct HID read was **actually attempted and denied**, as above. A successful
  Raw Input registration would not bypass the exclusive collection. Enumeration
  is useful for identification/metadata; no raw PTP capture or background success
  is claimed. The app does not register a second competing global raw reader.
* `PhysicalGestureRecognizer` consumes pointer data supplied by its caller. It
  can improve interpretation but cannot create missing contacts; Microsoft also
  documents that touchpad taps/holds are not recognized through this route.
  It is not a solution to missing one-finger/background acquisition.
  [PhysicalGestureRecognizer](https://learn.microsoft.com/en-us/windows/win32/input-precisiontouchpad/physicalgesturerecognizer)
* Pointer history could recover coalesced samples **after delivery**. This
  release reads the latest full frame; it does not promise every hardware sample.
* Mouse hooks, wheel/WM_GESTURE deltas and global shortcuts cannot establish
  where a finger touched the pad. They are not fed into the region tracker.
* An OEM-supported SDK or a documented shared vendor-specific HID collection
  could be a user-mode route on suitable hardware. No such supported contact API
  has been established for this installation. Generic vendor collections are
  not assumed to carry touch reports; no proprietary feature reports are sent.

## Future HID filter: feasible research, not a ready workaround

Inference from the exclusive-read result and Microsoft's driver guidance: a
vendor-supported transport/collection interface or an appropriately placed
signed filter may be needed for unrestricted contacts. Placement must be reviewed
against this machine's actual HID/I2C stack; a mouse-class delta filter cannot
reconstruct contact coordinates. Microsoft recommends vendor collections where
possible and warns against filters between HIDCLASS and transport minidrivers.
A generic lower-filter recipe should therefore not be presented as supported.
[HID driver guidance](https://learn.microsoft.com/en-us/windows-hardware/drivers/hid/keyboard-and-mouse-hid-client-drivers)

A separate feasibility project would need:

1. WDK/KMDF design, hardware-specific stack/descriptor inspection, contact ID,
   tip/confidence/count, logical X/Y range, scan-time and partial-report assembly.
2. Passive copies of valid reports while preserving original input IRPs,
   cancellation, ordering, sleep/resume, surprise removal and normal Windows use.
3. A bounded control-device/broker channel with narrow ACLs and explicit session
   consent; secure desktop and other users must not leak into the application.
4. Driver signing/distribution requirements, compatibility/HLK testing where
   applicable, HVCI compatibility, recovery/rollback, latency and crash testing
   on disposable hardware before any installation on this laptop.
   [Driver signing](https://learn.microsoft.com/en-us/windows-hardware/drivers/dashboard/code-signing-reqs)

The existing `IAdvancedTouchProvider` remains the future integration seam.
No driver package, test-signing change, security bypass or installation was
created in Phase 3. Force and haptic output remain unsupported/unverified.
