# Update Plan — v1.0.2

Combined plan for the next CLI update, covering task-switching behavior, layout fixes,
clock-out flexibility, day-start automation rework, and multiline task descriptions.

## 1. Switch menu keeps showing tasks already switched away from

**Root cause:** [SwitchScreen.cs:124](../src/SheduleHelper.Cli/Screens/SwitchScreen.cs) filters the
list purely by `TaskItem.Status != Done`. Switching away from a task (`TrackingService.SwitchAsync`)
only closes its `ProjectTimeLog` row — it never touches `TaskItem.Status`. So a task you left stays
`Todo`/`InProgress` forever and keeps reappearing.

**Decision:** Auto-transition status. A task becomes `InProgress` automatically the first time it's
tracked; an explicit "mark Done" action is added, and Done tasks (only) disappear from the switch
menu. The existing `Status != Done` filter then behaves correctly with no query changes needed.

**Fix:**
- `TrackingService.SwitchAsync` sets `TaskItem.Status = InProgress` when starting tracking on a task
  that isn't already `InProgress`/`Done`.
- Add a "mark Done" action reachable from `ProjectScreen` / `SwitchScreen`.

**Effort:** Small–Medium.

## 2. Task name column too narrow on Projects/Project screens

**Root cause:** Hardcoded fixed widths — `Name,-28` in
[ProjectsScreen.cs:68](../src/SheduleHelper.Cli/Screens/ProjectsScreen.cs), `Title,-30` in
[ProjectScreen.cs:80](../src/SheduleHelper.Cli/Screens/ProjectScreen.cs). Both just pad, never
truncate, so long names overflow into neighboring columns. Status column is also hardcoded at
absolute x=60 (`ProjectsScreen.cs:69`).

**Fix:** Reuse the pattern already used in
[HomeScreen.cs:313-320](../src/SheduleHelper.Cli/Screens/HomeScreen.cs) — compute
`available = frame.Width - (fixed columns width)` and call the existing
`Formatting.Truncate(name, available)` helper
([Formatting.cs:48](../src/SheduleHelper.Cli/Infrastructure/Formatting.cs)), which already ellipsizes
with `…`. Apply to both screens; shift the header literal and Status column x-position to follow the
dynamic Name width.

**Effort:** Small.

## 3. Clock-out doesn't allow a few minutes into the future

**Root cause:** Hard `> DateTime.Now` rejection in
[AttendanceService.cs:88-91](../src/SheduleHelper.Core/Services/AttendanceService.cs) (`ClockOutAsync`).
Sibling identical checks exist for clock-in (`AttendanceService.cs:51-54`) and tracking start time
(`TrackingService.cs:44-47`) — all three are independent.

**Decision:** Fixed 30–60 minute grace window (simple constant, no new setting). Clock-in and
tracking-start checks remain strict (unchanged).

**Fix:** Relax the clock-out check in `AttendanceService.cs:88-91` to allow up to N minutes ahead of
`DateTime.Now`. No CLI changes needed — `ClockOutScreen`/`TimeEntryScreen` already pass the chosen
time straight through.

**Effort:** Small.

## 4. Remove silent day-start automation → Now/Default/Custom choice

**Root cause:**
[AttendanceService.ResolveDayStartAsync](../src/SheduleHelper.Core/Services/AttendanceService.cs)
(lines 106-151) runs fully automatically and silently on first Home entry each day, gated only by the
`DayStartAutomation` enum (`Off` / `CloseForgottenDays` / `CloseAndClockIn`) — it writes
clock-out/clock-in rows *before* showing anything, then shows a dismissible after-the-fact banner
([HomeScreen.cs:406-416](../src/SheduleHelper.Cli/Screens/HomeScreen.cs)).

**Fix:** The Now/Default/Custom pattern already exists and is proven — it's exactly what
`ClockOutScreen.cs` does today (a 3-row `SelectList`: Now / Default / Custom time via
`TimeEntryScreen`, `ClockOutScreen.cs:20,53-55,88-94`). Stop `ResolveDayStartAsync` from acting
unprompted and instead surface that same picker when a forgotten-day/day-start condition is
detected — reusing `TimeEntryScreen` for the custom option, same as clock-in/out already do.

**Project/task continuation stays automatic, unchanged:** this is a *separate* switch —
`TrackingService.ResumeLastAsync` ([TrackingService.cs:77-118](../src/SheduleHelper.Core/Services/TrackingService.cs)),
driven by its own `ResumeTrackingOnClockIn` setting, independent of `DayStartAutomation`. It already
correctly skips resuming if you deliberately stopped or the project was archived. No change needed
here except decoupling its trigger from the now-manual clock-in step.

**Effort:** Medium — largest item. Touches `AttendanceService.ResolveDayStartAsync`, needs a new
screen/flow (or extends `ResolveForgottenScreen.cs`), and the Settings toggle semantics change (the
`DayStartAutomation` enum may collapse to just on/off "prompt me" rather than encoding the
auto-behavior itself). Test carefully — real timesheet data is on the line.

## 5. Choice of "now" vs "original clock-in time" when picking first task of the day

**Root cause:** `SwitchScreen.SwitchToSelectedAsync` hardcodes `DateTime.Now`
([SwitchScreen.cs:159](../src/SheduleHelper.Cli/Screens/SwitchScreen.cs)) with no alternative.
`TrackingService.SwitchAsync` already accepts an arbitrary `startTime` and only rejects future times
— this is a pure CLI/UI addition, no Core blocker.

**Fix:** When starting tracking for the first time today (no active/prior segment), show a small
Now / "at clock-in time (HH:mm)" choice before calling `SwitchAsync` — same `TimeEntryScreen`
building block again.

**Effort:** Small.

## 6. Progress bar hides overtime (worked > shift target)

**Root cause:** Not broken/crashing — `ProgressBar.Draw` clamps the fill ratio to `[0,1]`
([ProgressBar.cs:26](../src/SheduleHelper.Cli/Widgets/ProgressBar.cs)), so the bar silently plateaus
at 100% full once you pass your target, with no visual distinction between "just hit 8h" and "10h in,
2h overtime." The caption text still shows correct worked/target values — only the bar itself is
uninformative.

**Fix:** In [HomeScreen.cs:215-218](../src/SheduleHelper.Cli/Screens/HomeScreen.cs), detect
`ratio > 1` and pass an "overtime" flag/color into `ProgressBar.Draw` so the full bar renders in a
distinct color (e.g. warning/amber) instead of the normal accent color when over target. Optional
nice-to-have: a small `+2h 15m` badge next to the caption.

**Effort:** Small.

## 7. Multiline task description, shown on Home screen

**Root cause:** `TaskItem.Description` field already exists
([TaskItem.cs:40-44](../src/SheduleHelper.Core/Components/Entities/TaskItem.cs), max 1000 chars) — no
Core change needed there. But `TextField` is genuinely single-line: it stores chars in a flat list
with one cursor, never wraps, and explicitly does not consume Enter
([TextField.cs:89-91](../src/SheduleHelper.Cli/Widgets/TextField.cs)) — Enter always falls through to
`TaskEditScreen`'s save action ([TaskEditScreen.cs:82-85](../src/SheduleHelper.Cli/Screens/TaskEditScreen.cs)).
`HomeScreen` never reads `.Description` today at all.

**Fix:**
- `TextField` needs a multiline mode: row+column cursor (or `\n`-aware char list), vertical
  wrap/scroll in `Draw`, and Enter inserts a newline instead of falling through — only when that mode
  is active, so the Title field (still single-line) keeps its current Enter-saves behavior.
- `TaskEditScreen` needs to route Enter to the focused field first when it's the multiline
  Description, saving via a separate key (F10 is already wired) instead of unconditional Enter;
  layout needs multiple rows reserved for Description and everything below (Status row etc.) shifted
  down.
- `HomeScreen.RenderClockedIn` (currently lines 220-226) needs a second line under the active task
  label showing the description, wrapped/split across however many lines it needs, with the "Today"
  section start row and banner/message rows below shifted down accordingly.

**Effort:** Medium–Large — second-biggest item, mainly because of the `TextField` rewrite.

## Suggested order

1. **Quick wins** (low risk, independent): #2 (column width), #6 (progress bar color), #5
   (now/clock-in choice), #3 (clock-out grace window).
2. **Switch-menu status transition** (#1).
3. **Day-start automation → manual choice** (#4) — largest behavioral change, touches the daily
   flow, test carefully given real timesheet data.
4. **Multiline description** (#7) — self-contained `TextField` rewrite, can be done any time.
