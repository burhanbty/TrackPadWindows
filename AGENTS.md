# TrackpadProMax Windows

- Keep Windows implementation in `Windows/`; preserve original macOS code in `upstream/`.
- .NET 10 WPF, Windows x64. Use workspace SDK through `Windows/build.ps1`.
- Build/test: `./Windows/build.ps1 -Test`. Tests include disposable Windows fixtures.
- Publish: `./Windows/build.ps1 -Test -Publish`; preserve a recoverable prior publish first.
- Configuration is `%LOCALAPPDATA%/TrackpadProMax/settings.json`; preserve backward compatibility and backups.
- Keep native, global-shortcut and simulated evidence separate. API presence/registration is not physical delivery.
- Do not invent contact coordinates, pressure, haptics or physical acceptance results.
- No driver/security/Windows gesture settings changes, no remote pushes for Phase 2.
