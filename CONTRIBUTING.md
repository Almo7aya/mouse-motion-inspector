# Contributing

Thanks for helping improve Mouse Motion Inspector. Code, documentation,
reproducible bug reports and usability feedback are welcome.

This guide is for working on the source code. To use the app, follow the
[release download instructions](README.md#download-and-run); no build is needed.

## Development requirements

- Windows x64 and the [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0).
- Git to clone the repository, or an extracted source archive.
- Node.js 24 or newer to run the browser companion's checks.

`global.json` allows installed .NET 10 feature bands. No third-party NuGet
packages are used. The normal build uses installed SDK reference packs without
external package sources.

## Build and test

From PowerShell, clone the repository and enter its directory. If you already
have a checkout or extracted source archive, open PowerShell at its root instead.

```powershell
git clone https://github.com/Almo7aya/mouse-motion-inspector.git
cd mouse-motion-inspector
```

Build the Windows app:

```powershell
.\build-native.ps1
.\artifacts\MouseMotionInspector\MouseMotionInspector.exe
```

`build-native.ps1` builds and runs the native data-handling checks, then publishes
the Windows executable and its companion files to `artifacts/MouseMotionInspector/`.
It is a developer build helper, not part of installing a release.

Run the browser companion's checks separately:

```powershell
node --test --test-isolation=none tests/*.test.cjs
```

Automated checks cover data handling and rendering. Physical-device behavior,
clean-machine installation and display scaling require the
[manual release checks](docs/release-checklist.md).

## Making changes

Keep capture work off the UI thread and keep low-level hook callbacks short.
Raw device input, absolute coordinates, cursor samples and injected events are
different evidence streams;
do not merge them into an asserted device/process attribution.

Add regression checks for changes to capture arithmetic, time windows, coordinate
modes, import validation or selection. Test graph changes with the synthetic
renderer and the manual scenarios in [the release checklist](docs/release-checklist.md).
Use synthetic fixtures in commits, never a personal recording or inventory dump.

Open a focused pull request describing the trigger, resulting behavior, checks
run and remaining limitations. Follow `.editorconfig`. Avoid unrelated formatting
or generated build output. Contributions are provided under the MIT license.

Architecture: [developer guide](docs/architecture.md). Measurement semantics:
[interpretation guide](docs/interpretation.md).

## Packaging and CI

```powershell
.\scripts\package.ps1                 # Requires .NET on the target machine
.\scripts\package.ps1 -SelfContained  # Includes .NET; may download runtime packs
.\scripts\package-source.ps1          # Source ZIP; excludes local captures
```

Packages and SHA-256 files are written to `artifacts/releases/`.

CI is manual only. Maintainers can run `gh workflow run ci.yml --ref main` or
use the workflow's **Run workflow** button. A push or pull request does not run it.
It runs the native and browser checks, renders chart, guide and About previews,
and packages both Windows flavors plus source.

The separate **Manual release** workflow publishes artifacts from a successful
build of the tagged commit. See [Publishing](docs/publishing.md) for the complete
procedure. Release builds are currently unsigned.
