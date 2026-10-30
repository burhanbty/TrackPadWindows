# Hardware diagnostics

The collector uses read-only WMI queries, RtlGetVersion, Raw Input inventory,
DLL export lookup and WinRT metadata. It does not open exclusive HID collections,
change registry values or send device commands. No report leaves the computer.

## Status semantics

* **ConfirmedSupported**: stated evidence was observed. For an API row this
  means its export/type is present, not that physical touchpad data arrived.
* **ConfirmedUnsupported**: a specific API is absent or an implemented mechanism
  cannot provide the requested functionality, with an explanation.
* **NotDetected**: this probe found no evidence; it is not a definitive hardware
  negative (Windows may hide exclusive collections).
* **NotAccessible**: an access/probe failure prevented determining support.
* **UnknownRequiresPhysicalTesting**: data/calibration/device testing is needed.

WMI failures are preserved in Errors and other probes continue. Pressure,
contact count, coordinates, contact geometry and touchpad haptics remain unknown
without a verified physical stream or accessible descriptor. The native provider
does not expose the pressure field's default 512 as measured force.

## Local collection evidence

Collected successfully on the user's local computer, 2026-10-30:

* Manufacturer: Micro-Star International Co., Ltd.
* Model returned by Win32_ComputerSystem: **Katana 17 B13VGK**. This differs from
  the reported B13VFK-1242XTR; firmware model is recorded without guessing SKU.
* Architecture: x64; Windows kernel version 10.0, build **26300**.
* PnP touchpad: `HID uyumlu dokunmatik yüzey`; instance begins
  `HID\MSNB0001&COL02`. I2C parent begins `ACPI\MSNB0001`.
* PnP manufacturer is the generic standard-system-device label; a physical
  touchpad vendor cannot reliably be inferred from that label.
* Raw Input inventory included the Precision Touchpad digitizer usage collection.
* New touchpad registration/frame APIs and the two WinRT gesture classes were
  present. Presence alone did not prove physical input delivery.
* Windows.Devices.Haptics.SimpleHapticsController type was present; no touchpad
  haptic actuator was confirmed.

Full local report: `artifacts/diagnostics-local.json` (ignored build artifact;
contains full device instance IDs). Read-only command exited 0. No physical
finger motion, force, haptic actuator response or background raw stream was
verified in this session.

## Physical acceptance procedure

1. Run the app and export a fresh report; compare the model and PTP rows.
2. Confirm Ctrl+Alt+F8 opens the wheel; resolve any footer shortcut conflicts.
3. Map a three/four-finger gesture manually in Windows and verify it opens the
   wheel with another application in the foreground.
4. Optionally enable experimental native input while the settings window is
   foreground. Test two-finger manipulation; look for native frame recognition.
   Test focus loss and cancellation. Disable it afterward if scrolling changes.
5. Do not infer pressure from a default field or haptic support from a type name.
   These require separate device-specific evidence before implementation.

Never publish a diagnostic report automatically. Review instance IDs before
sharing. A report can be exported from the page or the `--diagnostics` CLI mode.
