# Browser tester

For per-device attribution, use the [Windows application](../README.md). Browser mouse events cannot identify the physical input device.

Open **../index.html** directly in a desktop browser. Everything is in that one file;
there are no packages, remote assets, or installation steps.

1. Enter your surface, actual DPI, connection, and optionally the polling rate.
   These are manual labels; the app does not read or change mouse settings.
2. Start with **Visible cursor**, 60 seconds of recording, and 5 seconds of settling.
3. Click **Start test** (or press **S** outside an input). Put the cursor somewhere
   inside the page and take your hand off during the countdown.
4. Leave the mouse and desk untouched. The recording ends automatically. Press
   **Esc** to stop early without reaching for a button. Focus loss, leaving the
   page in visible mode, hiding the tab, or resizing the window saves a partial run.
5. Repeat each condition 2–3 times. Change one variable at a time: surface first,
   then DPI, then wired versus wireless if useful.
6. Select completed runs in the comparison table to inspect them. Use **Export
   all · JSON** to share every sample and its test conditions. CSV exports provide
   a summary of all tests or individual movement events for the selected test.

The browser stores complete runs locally with IndexedDB when available. Storage
failure is shown in the page; exports remain available. Moving the file, changing
browsers, private browsing, or clearing site data can make old recordings
unavailable. Keep an exported JSON copy. Data is not uploaded anywhere.

## Capture modes

- **Visible cursor:** captures `clientX/clientY` differences in CSS pixels, including
  Windows pointer speed and acceleration effects. Keep the cursor away from page
  and screen edges. It cannot measure further travel once a cursor hits an edge.
- **Locked · unadjusted requested:** hides and locks the cursor, requests
  `unadjustedMovement: true`, and captures `movementX/movementY`. This asks the
  browser to bypass OS acceleration. Support varies; the app labels it as a
  request because it cannot independently verify the browser's implementation.
- **Locked · standard:** locks the cursor with ordinary OS-adjusted movement.

Pointer lock needs a direct user interaction and may be blocked in embedded
previews. Open the file in its own desktop browser tab. If the browser rejects
unadjusted movement, choose standard lock or visible cursor and start a new run;
the app never silently changes capture modes.

Locked deltas use **browser units**, not guaranteed physical pixels, CSS pixels,
millimetres, or hardware counts. Compare runs in the same mode and browser with
the same window scaling. DPI labels are not used to convert distances into
physical measurements.

## Interpreting a run

- **Movement events:** nonzero mousemove events delivered to the page.
- **Total travel:** sum of the length of every recorded delta. Out-and-back jitter
  adds travel even when it ends at its starting point.
- **Net displacement:** straight-line distance from the starting point to the end.
- **Farthest from start:** maximum distance reached from the start at any recorded step.
- **Activity over time:** distance accumulated in each one-second bucket.
- **Step directions:** count of events in eight direction sectors; the compass
  separately shows the direction of the final displacement.

No threshold or smoothing is applied to nonzero samples. Only the drawn trace is
subsampled after 6,000 events; statistics and exports retain all recorded events.
At 100,000 samples a recording stops and is marked partial, without dropping
already recorded samples. Idle time remains in the duration and rate denominator.

Browsers may merge or omit mouse reports, and time stamps have limited precision.
This is **not a USB polling-rate tester or a hardware sensor diagnosis**. Small
motions can be lost before reaching the page. Any pointing device or pointer
automation can contribute events. Higher DPI can make the same physical motion
larger in a recording. Repeat tests and compare timing, not just distances.

Exported JSON includes units, settings, browser environment, completion/stop
reason, summary metrics, and samples in the order
`[time_ms, dx, dy, x_from_start, y_from_start]`. Coordinates are relative to the
recording origin; right is positive X and down is positive Y. Visible mode also
includes the initial position inside the page when available.

API references: [Pointer Lock](https://developer.mozilla.org/en-US/docs/Web/API/Pointer_Lock_API)
and [movementX units](https://developer.mozilla.org/en-US/docs/Web/API/MouseEvent/movementX).

## Checks

Run From the repository root, run `node --test --test-isolation=none tests/*.test.cjs` for deterministic
capture-arithmetic, CSV-export, and controller checks. Controller tests use a
small DOM double and simulated clocks; they are not browser or physical-mouse
validation.
