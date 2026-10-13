# Reading a mouse-motion test

Use repeated observations to narrow down a source. A trace is evidence of what
Windows delivered, not a sensor-health verdict.

For a short walkthrough, open the app's **Interpretation guide**: **Identify →
Record → Read → Compare**. The Read step has switchable quiet, back-and-forth and
drift illustrations; these are examples, not your recording. Use **Help with a
result** for a specific symptom. This page is the fuller reference.

## Identify before recording

Move one device at a time. A keyboard, receiver, touchpad or virtual driver can
expose a mouse interface. Identical names can be different interfaces of one
mouse. `D1`, `D2`, etc. identify attachments, and can change on reconnection.
VID:PID identifies a product family, not an individual unit. Save a verified label
after observing which row changes.

The graph automatically chooses an interface with movement. Clicking a row keeps
that selection, including a quiet row. Switching tests resets automatic selection.
An empty trace points to a moving row when one exists. Live identification shows
counters; plotting starts with a timed test.

## Plan a fair comparison

1. Make a separate deliberate-movement test to verify capture and device selection.
2. Record 60 seconds hands-off, after 5 seconds of settling.
3. Repeat 2–3 times. Avoid desk vibrations and cable tension.
4. Change only one condition: surface, DPI, connection or device setting.
5. Compare timing, repeatability and travel. Note unusual events such as lifting
   the mouse; keep deliberate-motion runs separate from hands-off trials.

DPI and polling rate are labels you enter, not measurements. Higher DPI can
increase the recorded magnitude of the same physical motion. These labels do
not establish a conversion to millimetres.

## Graphs and numbers

| View or metric | Meaning | Interpretation |
| --- | --- | --- |
| Movement path | Accumulated X/Y from origin; X right, Y down | Equal axis scale preserves shape. Cyan circle is start, green dot is end. |
| Displacement over time | Accumulated X (green), Y (cyan), including idle time | Steps and reversals show when movement occurred. Positive Y means down, even though positive values rise on this time chart. |
| Activity over time | Sum of step lengths per labeled time bucket | Reveals bursts and out-and-back motion. Distance per bucket, not polling rate. |
| Movement reports | Nonzero observations | Not USB polls, screen pixels or distinct physical motions. |
| Total travel | Sum of every step length | +1 then −1 has travel 2 and net 0. |
| Net X / Y | Final displacement from origin | Can be zero despite substantial motion. |
| Peak excursion | Farthest recorded point from origin | Not the same as total travel. |

Path controls: wheel zoom, drag pan, double-click/Home to fit. Hover crosshairs
show chart coordinates, not an assertion that a report occurred under the cursor.
PNG exports use the fitted view. Large graphs reduce drawing points while retaining
chronological endpoints and X/Y extremes in each bucket. Statistics and JSON retain
all observations up to the recording limit. Time charts connect retained samples;
do not infer precise timing between reduced points.

## Keep the evidence streams separate

**Relative Raw Input** identifies the Windows interface supplying deltas, in raw
relative units unaffected by Windows pointer-speed scaling. **Absolute input**
uses normalized coordinates and establishes a baseline first. Relative,
primary-display absolute and virtual-desktop absolute modes have separate traces.
Do not combine their distances or compare them as identical units.

**Injected input** means Windows set the low-level injected flag. It does not
identify the process. Zero flagged events does not rule out software, cursor-warp
APIs, virtual drivers or an unavailable/removed hook.

**Cursor changes** are screen-position samples about every 50 ms. Brief out-and-back
movement can be missed. A cursor change with no raw report remains unresolved;
the app does not guess the source from nearby timestamps. A zero device handle
is explicitly unattributed, including some precision-touchpad input.

These streams can observe the same motion. Never sum them as unique movements.

## What to try next

| Observation | Next step |
| --- | --- |
| One verified interface moves hands-off | Repeat on another surface, then change DPI separately. |
| High travel, small net movement | Inspect reversals and repeat to distinguish a pattern from one disturbance. |
| Motion only after touching the desk | Repeat undisturbed and document the difference. |
| Injected reports appear | Check pointer automation you deliberately enabled; this tool cannot name its process. |
| Cursor changes without a raw trace | Read injected input and warnings; the source is unresolved. |
| Empty graph | Check settling, selected device/mode, then make a deliberate-motion test. |
| Drops, errors or limit stop | Treat the run as incomplete; repeat under lighter load or for a shorter duration. |

## Evidence quality and sharing

The limit is 250,000 observations per run, including movement, injected input,
cursor samples and device changes. Partial runs retain their stop reason.
Warnings prevent a run being marked complete. Observation times come from app
processing, not the sensor or USB bus; scheduling and batching affect intervals.
This is not a polling-rate benchmark.

JSON retains settings, observations, identities and warnings. CSV is a summary.
Reopening JSON recomputes statistics and rejects inconsistent totals or unsupported
formats. Review identifiers, screen coordinates, button flags and notes before sharing.

## Primary API references

- [Raw Input and device distinction](https://learn.microsoft.com/en-us/windows/win32/inputdev/about-raw-input)
- [RAWMOUSE coordinate modes](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-rawmouse)
- [RAWINPUTHEADER and device handles](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-rawinputheader)
- [Injected-event flags](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-msllhookstruct)
- [Low-level hook lifetime and limits](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelmouseproc)
