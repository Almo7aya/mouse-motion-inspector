# Release validation

Run on the exact release candidate. Record tested Windows versions, display
scaling, devices and package flavor in release notes. Automated success alone
does not establish UI quality or physical-device accuracy.

## Automated gates

- Native checks/build pass without warnings or errors.
- Browser checks pass on Node.js 24+.
- `--render-demo` PNGs have readable axes, units, markers, idle periods and bursts
  at both sizes.
- `--render-guide` previews show all four steps and three illustrative patterns
  with readable instructions and navigation at both sizes.
- Packaging produces a ZIP and matching SHA-256 file; CI builds the self-contained flavor.
- Archive contains no recordings, identifiers, temporary files or source/build intermediates.

## Manual Windows checks

- Launch the extracted package on a clean x64 machine. Test self-contained builds
  without a separately installed .NET runtime.
- Check 100%, 150%, 200% scaling, cross-monitor movement, minimum window size,
  inputs, tabs and graph readability.
- Test preset/custom entries, invalid inputs and keyboard navigation.
- Check guide navigation, example switching, Open graphs and every help topic.
- Identify two physical devices; verify same-named interfaces remain separate.
- Record deliberate movement and quiet tests. Confirm automatic selection,
  explicit quiet selection, all three graphs, zoom/pan/fit and PNG export.
- Check settling, deadline, manual stop, global hotkey, hotkey conflict, cancellation,
  unplug/replug and closing during capture/save.
- Save, restart, reopen single/combined JSON and compare counts/travel/net.
  Try corrupt files, future formats and duplicate import; existing tests survive.
- Export to a non-writable location; verify clear errors without losing data.
- Use a high-report-rate device for sustained capture; inspect responsiveness,
  CPU and dropped reports. Limit/overflow runs must be partial.
- Compare CSV with UI numbers and check UTF-8 labels and CSV quoting.
- Verify interpretation and privacy docs match the final behavior.

## Publication

- Review MIT license, name, README links and version.
- Enable GitHub private vulnerability reporting.
- Review staged files; `.gitignore` does not protect already-tracked files.
- Publish only tested archives, identify signing status and limitations.
- Never describe unperformed checks as passed.
