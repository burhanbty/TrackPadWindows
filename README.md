# TrackpadProMax for Windows

The Windows port is in [Windows](Windows/README.md). The original Apache-2.0
macOS implementation is preserved, unchanged, in `upstream/` at commit
`73a322138b8cfaa09c1d3fe63c44774458917844`.

Clone with `git clone --recurse-submodules https://github.com/burhanbty/TrackPadWindows.git`
to also fetch the pinned original macOS source. For an existing clone, run
`git submodule update --init --recursive`.

Build with a .NET 10 SDK using `Windows\build.ps1 -Test -Publish`, then start
`artifacts\TrackpadProMax\TrackpadProMax.exe`. SDK downloads, generated binaries
and local hardware reports are excluded from the source repository.
Read the Windows documentation for supported input paths and hardware limits.
