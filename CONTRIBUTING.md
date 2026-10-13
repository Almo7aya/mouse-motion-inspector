# Contributing

Use Windows x64 and the .NET 10 SDK. `global.json` allows installed .NET 10 feature
bands. No third-party NuGet dependencies are required for the normal build.

```powershell
.\build-native.ps1
node --test --test-isolation=none tests/*.test.cjs
```

The browser checks require Node.js 24 or newer. Keep capture work off the UI
thread and keep low-level hook callbacks short. Raw device input, absolute
coordinates, cursor samples and injected events are different evidence streams;
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

CI is manual only. Maintainers can run `gh workflow run ci.yml --ref main` or
use the workflow's **Run workflow** button. A push or pull request does not run it.
