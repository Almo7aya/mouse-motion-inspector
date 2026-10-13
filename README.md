# Mouse Motion Inspector

Find which Windows mouse interface is sending movement, then compare controlled
tests across surfaces, DPI settings and connections. A local, open-source utility
for investigating pointer drift and jitter, with no account or cloud service.

**Windows x64 · .NET 10 · MIT**

[Download](https://github.com/Almo7aya/mouse-motion-inspector/releases/latest) ·
[Report a bug](https://github.com/Almo7aya/mouse-motion-inspector/issues/new?template=bug_report.yml) ·
[Request a feature](https://github.com/Almo7aya/mouse-motion-inspector/issues/new?template=feature_request.yml)

![Movement path using synthetic example data](docs/images/path-340.png)

## Features

- Device attribution with Windows Raw Input, including same-named interfaces.
- Timed tests with settling time, surface labels and editable DPI/polling presets.
- Equal-scale movement path, X/Y displacement over time and activity over time.
- Path zoom/pan, chart crosshairs, and fitted PNG exports.
- Separate Windows-flagged injected input and sampled cursor changes.
- Automatic JSON saves, reopening recordings, and CSV summaries.
- A four-step visual guide with quiet, back-and-forth and drift examples.
- About tab with version, credits, privacy information and project links.

This identifies an **input interface**, not a proven hardware fault or the process
responsible for every pointer change. It is not a USB polling-rate benchmark.
[Understand the evidence and its limits](docs/interpretation.md).

## Run

Extract an entire packaged ZIP and launch `MouseMotionInspector.exe` as a normal
user. A **self-contained** package includes .NET; a **framework-dependent** package
needs the [.NET 10 Windows Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0).
Keep the files together; copying only the EXE is insufficient.

To build from source, install the .NET 10 SDK on Windows x64, then:

```powershell
.\build-native.ps1
.\artifacts\MouseMotionInspector\MouseMotionInspector.exe
```

No third-party NuGet packages are used. The normal build uses installed SDK
reference packs without external package sources. Build output stays in `artifacts/`.

## Your first test

1. Move one physical device in live identification. Watch **Live reports** and
   select its row. In **Device identity**, save a recognizable label.
2. Enter the surface, actual DPI and polling-rate labels. Choose presets or type
   custom values. The app does not read or change your mouse configuration.
3. Start a 60-second test with 5 seconds to settle. Release the mouse before
   recording begins. Avoid touching the desk during a hands-off test.
4. Inspect **Test reports**, travel, net displacement and the three graphs.
   Automatic selection follows a moving interface; clicking a row keeps that
   interface selected for the current test.
5. Repeat 2–3 times, changing one variable at a time. Use a separate intentional
   movement run to check capture before investigating subtle drift.

**Ctrl+Shift+F8** starts/stops globally; **Esc** stops while the app is focused.
Path controls: wheel to zoom, drag to pan, double-click/Home to fit, number-pad
+/− to zoom. The in-app **Interpretation guide** walks through **Identify → Record
→ Read → Compare**. Switch the illustrated movement patterns to understand travel,
net displacement and activity. **Help with a result** opens one troubleshooting
topic at a time.

## Recordings and privacy

Automatic saves go to `%LOCALAPPDATA%\MouseMotionInspector\recordings`.
**Open recordings** accepts version-1 JSON, including older app recordings and
combined exports. CSV contains summaries, not enough data to reconstruct a path.
Old recordings beside previous executables remain there and can be opened explicitly.

No automatic network requests, keystroke recording, driver installation or mouse-setting
changes. Project links open in your browser when clicked. Exports may contain device identifiers, interface paths, screen coordinates,
mouse-button flags and personal notes. Review them before sharing publicly.
See [security and privacy](SECURITY.md).

## Test and package

```powershell
.\build-native.ps1
node --test --test-isolation=none tests/*.test.cjs
.\scripts\package.ps1                  # Target needs the .NET runtime
.\scripts\package.ps1 -SelfContained   # Includes runtime; may download runtime packs
.\scripts\package-source.ps1          # Source-only ZIP; excludes local captures
```

Browser checks require Node.js 24+. Packages and SHA-256 files appear in
`artifacts/releases/`. CI runs **only when manually triggered** through
**Actions → Manual build and packages → Run workflow**, or:

```powershell
gh workflow run ci.yml --ref main
```

It builds/tests on Windows, renders chart, guide and About previews, and packages
both Windows builds plus source. Pushes and pull requests do not start CI.
CI does not automatically publish a release. The separate **Manual release**
workflow publishes packages from a successful build when explicitly started.
Packages are currently unsigned.

Automated checks cover data handling and chart rendering. Physical-device behavior,
clean-machine installation and display scaling require the [manual release checks](docs/release-checklist.md).
A successful build alone is not production validation on every Windows system.

## Browser companion

Open `index.html` for a standalone browser cursor tester. It cannot attribute
movement to devices and uses separate coordinate units and recording formats.
[Browser guide](docs/browser-guide.md).

## Project

- [Interpretation guide](docs/interpretation.md)
- [Architecture and diagnostic commands](docs/architecture.md)
- [Contributing](CONTRIBUTING.md)
- [Release checklist](docs/release-checklist.md)
- [Publishing releases](docs/publishing.md)
- [MIT license](LICENSE)

Independent project; not affiliated with Razer or any device manufacturer.
