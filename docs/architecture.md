# Architecture and diagnostics

Windows x64, .NET 10 WinForms, no third-party NuGet dependencies. `global.json`
allows stable .NET 10 SDK feature bands. `native/Directory.Build.props` sets the
version, deterministic compilation and warnings as errors.

`CaptureEngine` owns a message-only window on a background thread. It registers
mouse usage with `RIDEV_INPUTSINK | RIDEV_DEVNOTIFY`, reads the current WM_INPUT
using GetRawInputData, then drains subsequent events using GetRawInputBuffer.
Checks verify x64 ABI layouts. The low-level hook only enqueues injected moves;
the global shortcut uses RegisterHotKey, not a keyboard hook.

A bounded queue feeds the UI. Overflow increments a counter and stops an active
test as incomplete. Metadata, disk I/O and drawing stay outside the hook callback.
Disposal signals the listener thread and unregisters resources, including during startup.

`Recording` applies settling/deadline windows and per-device absolute baselines.
`RecordingFiles` validates bounded version-1 JSON and recomputes motion statistics;
saves write a temporary sibling before renaming. `PlotData` computes activity and
reduces display points while preserving endpoints and local X/Y extremes.
The live `PlotSeries` cache processes only new samples for the selected stream,
maintains its bounds and activity totals, and merges display summaries in buckets
while preserving chronological endpoints and X/Y extrema. JSON exports retain
every captured observation; display reduction never changes a recording.

`UiFrameClock` requests a frame every 15 ms (roughly 60 updates per second), with
at most one callback pending on the UI thread. Actual refresh depends on rendering
cost and Windows scheduling. The device table updates changed cells in place;
hidden graphs and report tables do no rendering work. Visible detailed report
lists refresh at most 10 times per second. Minimized windows keep draining input
every 50 ms without repainting. Each normal queue drain has a 3 ms soft budget;
stopping a test flushes queued observations through the recording cutoff.
Cursor-position sampling remains approximately every 50 ms or longer under load,
separate from raw device capture and graph refresh.

`TraceSelection` distinguishes automatic selection from a deliberate row choice.
`MotionChart` handles gestures; `ChartRenderer` paints both the screen and PNGs.
`InterpretationGuide` presents four short steps with native keyboard-accessible
navigation. `GuidePicture` draws illustrative device counters, a test timeline,
three selectable motion patterns and a comparison plan. These examples never
enter capture or recording data. `GuideReference` opens troubleshooting by topic.
`AboutPage` shows the version, license, credits, local data policy and project links.
Links open the user's browser only after a click.

Automatic saves: `%LOCALAPPDATA%\MouseMotionInspector\recordings`. Open history
explicitly. Import limits: 128 MiB per file, 250,000 observations per run,
100 runs/1,000,000 observations per session. The browser has a separate format;
its old IndexedDB database name remains for compatibility with existing recordings.

## Commands after building

```powershell
# Device metadata only; no visible UI.
dotnet artifacts/MouseMotionInspector/MouseMotionInspector.dll --list-devices artifacts/device-inventory.json

# Two-second listener initialization check; never moves the pointer.
dotnet artifacts/MouseMotionInspector/MouseMotionInspector.dll --probe artifacts/native-probe.json

# Exact chart painter, synthetic input, no capture or desktop interaction.
dotnet artifacts/MouseMotionInspector/MouseMotionInspector.dll --render-demo artifacts/chart-preview

# Render About and guide controls at two sizes, all examples and help topics.
dotnet artifacts/MouseMotionInspector/MouseMotionInspector.dll --render-guide artifacts/guide-preview

# Reopen and render a saved recording without capture.
dotnet artifacts/MouseMotionInspector/MouseMotionInspector.dll --render-recording path/to/recording.json artifacts/recorded-preview

# Synthetic large-recording rendering and hidden-message-loop scheduler benchmark.
# Reports elapsed times and allocations; this does not measure on-screen FPS.
dotnet artifacts/MouseMotionInspector/MouseMotionInspector.dll --benchmark-charts artifacts/chart-benchmark.json

# Deterministic checks plus saved-recording validation.
dotnet native/MouseMotionInspector.Checks/bin/Release/net10.0/MouseMotionInspector.Checks.dll path/to/recording.json
```

Do not publish inventory dumps or personal captures. Packages contain published
binaries, license and docs, not recordings or build intermediates. SHA-256 files
provide corruption checks; publisher signing is separate.
