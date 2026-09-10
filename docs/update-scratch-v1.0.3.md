# Update Scratch — v1.0.3: Planning & Editing

Working notes for the next update. Two asks, one theme:

1. **Plan** days that aren't ordinary working days — vacation, public holidays, sick leave, a
   half-day at the doctor.
2. **Edit** what's already recorded — today's clock-in, last Thursday's clock-out, a whole week that
   never got entered at all — without opening SQLite by hand.

The common thread is that both need the app to address **an arbitrary date**, and everything it can
do today is anchored to *now*.

> Status: scratch/design. Nothing here is decided; §9 lists the calls that are the user's to make.

---

## 1. The platform question: CLI or WinUI?

Short answer: **build it in the CLI.** Put the domain in `SheduleHelper.Core` so a future WinUI host
inherits it for free, but ship the UX in the TUI.

### Why not WinUI

The comparison isn't "which host renders a calendar more nicely" — it's "which host already has an
app around the feature."

| | CLI (`SheduleHelper.Cli`) | WinUI (`SheduleHelper.Modern`) |
|---|---|---|
| Clock in/out | shipped | not implemented |
| Project/task tracking, switching | shipped | not implemented |
| Reports (4 zoom levels, calendar, breakdown) | shipped | not implemented |
| Projects/tasks browser + editors | shipped | not implemented |
| Settings | shipped | not implemented |
| Holds the real timesheet | yes — `%LOCALAPPDATA%\SheduleHelper\data.db` | no |

Day-editing is not a standalone tool; it's a *correction path* for data that gets created by clock-in,
clock-out and switching. Shipping it in WinUI means first porting all five rows above, or shipping a
second app that edits the first app's database — a data-integrity problem with no upside. That's a
port, not a feature update.

### Where a GUI would genuinely win, and why it doesn't decide this

Three things are honestly nicer with a mouse:

- **Drag a segment boundary** on a timeline to move 11:00 → 11:15.
- **Rubber-band a date range** across a month grid to book a week of vacation.
- **Multi-select scattered days** (three Fridays) in one gesture.

All three have keyboard equivalents that are *faster* once learned (type `11:15`; type a From/To
date pair). None involves rendering anything the TUI can't draw — the app already has
[`CalendarGrid`](../src/SheduleHelper.Cli/Widgets/CalendarGrid.cs),
[`ProgressBar`](../src/SheduleHelper.Cli/Widgets/ProgressBar.cs),
[`Sparkline`](../src/SheduleHelper.Cli/Widgets/Sparkline.cs) and an
[`Inspector`](../src/SheduleHelper.Cli/Widgets/Inspector.cs) pane. The one real cost is that a date
*picker* is ~60 lines of TUI code versus a one-line `CalendarDatePicker` in XAML. That's a rounding
error against porting five screens.

### What the CLI is missing that this needs

Genuinely nothing structural. The building blocks are all present:

- [`TimeEntryScreen`](../src/SheduleHelper.Cli/Screens/TimeEntryScreen.cs) — HH:mm entry with
  validation and a retry loop, already reused by clock-in and clock-out.
- [`ScreenStack`](../src/SheduleHelper.Cli/Infrastructure/ScreenStack.cs) — push/pop is the app's
  modal-dialog mechanism.
- [`ProjectEditScreen`](../src/SheduleHelper.Cli/Screens/ProjectEditScreen.cs) /
  [`TaskEditScreen`](../src/SheduleHelper.Cli/Screens/TaskEditScreen.cs) — the ↑↓ field / Enter edit /
  F10 save form pattern, ready to copy for an attendance form and a segment form.
- [`SwitchScreen`](../src/SheduleHelper.Cli/Screens/SwitchScreen.cs) — the project/task picker a
  segment editor needs.

The only genuinely new widget is a **date field** (or a small date-picker screen). One widget.

### The one thing to build host-agnostically

Every rule in §7 — segments inside their attendance window, no overlaps, one open log — belongs in
`SheduleHelper.Core`, not in a screen. Today those invariants are enforced *implicitly by the order
of operations* ([`StartProjectTimeLogAsync`](../src/SheduleHelper.Core/Models/LocalDbContext.cs)
closes the open segment before opening a new one, so overlaps can't happen). Free-form editing
removes that guarantee, so the rules have to become explicit and live somewhere both hosts share.

**Verdict:** CLI for the UX, Core for the rules. If WinUI is ever revived, it inherits a finished
domain and only has to draw.

---

## 2. What the data model can't express today

### 2.1 There is no such thing as a non-working day

[`AttendanceLog`](../src/SheduleHelper.Core/Components/Entities/AttendanceLog.cs) is
`(UserId, WorkDate unique, ClockIn required, ClockOut nullable)`. A vacation day is not an attendance
session — there is no clock-in to record — so the only ways to express one today are to invent a fake
08:30→17:00 session (lies about presence) or to leave the day empty (indistinguishable from "forgot
to log it").

There is also no way to record anything **in the future**: `ClockInAsync` rejects
`clockInTime > DateTime.Now`
([AttendanceService.cs:49-52](../src/SheduleHelper.Core/Services/AttendanceService.cs)), which is
correct for attendance and fatal for planning. Planning is future-dated by definition.

### 2.2 Nothing can address a past date

[`IAttendanceService`](../src/SheduleHelper.Core/Services/IAttendanceService.cs) has four methods and
all four are about *now*: `GetDaySnapshotAsync` (today), `ClockInAsync` (today), `ClockOutAsync` (the
one open log), `ResolveDayStartAsync` (today). There is no read, no write, no delete for "6 August".

### 2.3 Today's clock-in cannot be corrected at all

[`HomeScreen`](../src/SheduleHelper.Cli/Screens/HomeScreen.cs) offers `I` / `D` / `M` to *set* a
clock-in and `O` to clock out. Once written there is no key that changes it. Clock in at 09:00 by
mistake and the day is wrong until you edit SQLite.

### 2.4 Reports can look at a day but not touch it

[`ReportsScreen.HandleKey`](../src/SheduleHelper.Cli/Screens/ReportsScreen.cs) handles
`Esc`/`←`/`→`/`↑`/`↓`/`T` and nothing else. There is no day cursor and no `Enter`. The
`DayDetailScreen` designed in §5.3 of [Console TUI App Plan.md](Console%20TUI%20App%20Plan.md) was
never built — it's step 8 of that plan's §7, explicitly still open.

### 2.5 The backfill primitives exist but are orphaned

This is the useful surprise. [`LocalDbContext`](../src/SheduleHelper.Core/Models/LocalDbContext.cs)
already has:

- `LogAttendanceAsync(userId, workDate, clockIn, clockOut, ct)` — *"Intended for backfilling
  attendance that was not recorded live (e.g., previous months)."*
- `LogProjectTimeAsync(attendanceLogId, projectId, taskId, startTime, endTime, ct)` — *"Intended for
  backfilling project/task time that was not recorded live."*
- `DeleteAttendanceLogAsync(attendanceLogId, ct)` — cascades to its segments.

No service and no screen calls any of them. Half the write path for §6.3 is already written and
tested-by-construction against the schema.

What's still missing at the DbContext level:

- `UpdateAttendanceLogAsync` — there is `ClockOutAsync` (closes *the open* log) but nothing that
  changes a closed log's `ClockIn`/`ClockOut`.
- `UpdateProjectTimeLogAsync` — there is `EndProjectTimeLogAsync` (closes *the open* segment) but
  nothing that moves an existing segment's boundaries or reassigns its project/task.
- `DeleteProjectTimeLogAsync` — doesn't exist at all (only whole-day delete does).
- Anything at all for day marks.

### 2.6 A latent hazard the editor must not trip

`GetOpenAttendanceLogAsync` finds the open session as *"the most recent log with a null ClockOut"*
([LocalDbContext.cs](../src/SheduleHelper.Core/Models/LocalDbContext.cs)). Backfilling a past day and
leaving `ClockOut` null would make that day masquerade as the live session, or shadow the real one —
Home would show `ForgottenSession`, and a clock-out would land on the wrong date. **A retro-edited
day must never be left open.** This is invariant I5 in §7.

---

## 3. Proposed domain: `DayMark`

### 3.1 Why a separate table, not a column on `AttendanceLog`

Adding `DayType` to `AttendanceLog` and making `ClockIn` nullable would touch
[`TimeBudgetCalculator`](../src/SheduleHelper.Core/Models/TimeBudgetCalculator.cs),
`BuildSnapshotAsync`, `ReportingService` and every consumer of `AttendanceDaySnapshot` — a wide blast
radius on a live database, to model something that isn't an attendance session in the first place.

A separate table keeps the split honest: **`AttendanceLog` = what happened. `DayMark` = what kind of
day it was.** They're independent (a half-day of vacation has both), and only `DayMark` can be
future-dated.

```
DayMark
  Id             int, PK
  UserId         int, FK → Users, cascade
  Date           string "yyyy-MM-dd"   -- same convention as AttendanceLog.WorkDate
  Kind           DayMarkKind, stored as string name
  CreditedHours  decimal?              -- null = the user's TargetShiftHours
  Note           string?, max 200
  CreatedAt      DateTime

  unique index (UserId, Date)           -- mirrors AttendanceLog's (UserId, WorkDate)
```

```csharp
public enum DayMarkKind
{
    PublicHoliday = 0,
    Vacation      = 1,
    SickLeave     = 2,
    TimeOffInLieu = 3,   // spending banked overtime
    UnpaidLeave   = 4,
    BusinessTrip  = 5,   // present, but not clocking in/out normally
    Other         = 6,
}
```

### 3.2 How a mark affects the balance

Each kind maps to a **credit policy**, and `CreditedHours` overrides the amount:

| Policy | Target counted | Worked credited | Net effect | Default kinds |
|---|---|---|---|---|
| `Credited` | yes | = `CreditedHours` ?? target | balance 0, hours visible | Holiday, Vacation, Sick, Business trip |
| `Spent` | yes | 0 | balance −target (draws down banked overtime) | Time off in lieu |
| `Unpaid` | yes | 0 | balance −target | Unpaid leave |
| `Ignored` | no | no | invisible to the balance entirely | *(opt-in per mark)* |

`Spent` and `Unpaid` are arithmetically identical and differ only in label — worth keeping separate
because they mean different things on a timesheet, but they could collapse if that's noise.

**Half days** fall out of `CreditedHours`: a 2-hour doctor's appointment is
`Kind = Other, CreditedHours = 2`.

**Days that have both** a mark and real attendance: attendance always wins for worked time, and the
mark's credit is *added on top*. Half-day vacation (`CreditedHours = 4`) plus 4h clocked = 8h, balance
0. Documented explicitly because it's the one case where the two tables interact.

### 3.3 The one change that moves existing numbers

[`ReportingService`](../src/SheduleHelper.Core/Services/ReportingService.cs) currently computes a
bucket's target as `dailyTarget * logsInBucket.Count` — **a day with no log has no target**, so
missing days are invisible in the balance rather than negative. Marks have to join into that
calculation, which means `Worked`, `Target` and `Balance` will change for any period containing a
mark. Nothing changes for periods without marks, so existing history stays stable until the first
mark is created — but it's still a visible-numbers change and belongs in a release note.

`ReportBucket` also needs to carry the mark so the calendar can draw `V`/`H`/`S` instead of `—`, and
the "8 of 21 logged" caption can stop counting a vacation day as negligence.

### 3.4 Provenance — because these records may have to be defended

Once the app can rewrite history, "the database says 08:29" stops being evidence of anything. Two
cheap columns make edits legible instead of invisible:

```
AttendanceLog     + Origin      DayRecordOrigin  (Live | Backfilled | Edited)
                  + ModifiedAt  DateTime?
ProjectTimeLog    + Origin      DayRecordOrigin
                  + ModifiedAt  DateTime?
```

Day detail then shows a `✎` marker on rows that weren't recorded live. This is the minimum; a full
append-only audit table is the alternative, and probably overkill for a single-user local app —
see §9.

### 3.5 Migration notes

One migration adds the `DayMarks` table plus four columns. Per the hard-won lesson from the
`DayStartAutomation` migration: **`DayMarkKind` and `DayRecordOrigin` are string-converted enums, so
EF's scaffolded `defaultValue: ""` will produce rows that fail to materialize.** Hand-correct the
scaffolded `AddColumn` defaults to the entity initializers' own values (`"Live"` for `Origin`) before
running it, and rehearse against a throwaway SQLite file — not the real database.

---

## 4. Proposed domain: `ITimesheetEditService`

`IAttendanceService` stays what it is — the *live* clock-in/clock-out state machine. Retro-editing is
a different concern with different rules (arbitrary dates, no "is it in the future" check, invariants
that the live path gets for free), so it gets its own service rather than doubling the size of that
interface.

```csharp
public interface ITimesheetEditService
{
    // Read
    Task<DayDetail> GetDayAsync(int userId, DateOnly date, CancellationToken ct);
    Task<IReadOnlyList<DaySummary>> GetRangeAsync(int userId, DateOnly from, DateOnly to, CancellationToken ct);

    // Attendance
    Task<DayDetail> SetAttendanceAsync(int userId, DateOnly date, DateTime clockIn, DateTime clockOut,
                                       SegmentClampMode clamp, CancellationToken ct);
    Task<DayDetail> ClearAttendanceAsync(int userId, DateOnly date, CancellationToken ct);

    // Segments
    Task<DayDetail> AddSegmentAsync(int userId, DateOnly date, int projectId, int? taskId,
                                    DateTime start, DateTime end, CancellationToken ct);
    Task<DayDetail> UpdateSegmentAsync(int segmentId, int projectId, int? taskId,
                                       DateTime start, DateTime end, CancellationToken ct);
    Task<DayDetail> DeleteSegmentAsync(int segmentId, CancellationToken ct);

    // Day marks
    Task<int> SetDayMarksAsync(int userId, DateOnly from, DateOnly to, DayMarkKind kind,
                               decimal? creditedHours, string? note, bool skipWeekends,
                               MarkConflictMode onConflict, CancellationToken ct);
    Task<int> ClearDayMarksAsync(int userId, DateOnly from, DateOnly to, CancellationToken ct);
}
```

`TimesheetEditException` mirrors
[`AttendanceOperationException`](../src/SheduleHelper.Core/Services/IAttendanceService.cs): message is
an already-localized display string, callers show it as-is.

`DayDetail` is the read model behind the Day detail screen — the attendance log, its segments with
project/task names resolved, the day mark, the derived raw/lunch/net/target/balance figures, and the
**gap list** (untracked stretches inside the attendance window). Every mutation returns a freshly
rebuilt `DayDetail`, the same way `AttendanceService` rebuilds its snapshot after every write, so the
state is derived in exactly one place.

Every method here is *pure editing* — no "is it in the future" checks, since a corrected day is
allowed to be any date. Nothing in this service may touch the currently open live session; if the
targeted date is today and a session is open, the edit is rejected and the user is pointed at Home's
`O` instead (invariant I6).

---

## 5. The UX

### 5.1 Reports gains a day cursor and drill-down

The calendar becomes navigable and `Enter` drills in. Navigation model, consistent across zooms:

- **←/→** — previous/next bucket (day at Week/Month zoom, week at Quarter, month at Year). Rolling
  past the edge of the period moves into the neighbouring period, so period navigation comes free.
- **↑/↓** — ±1 week in the Month grid (up/down a calendar row); ±1 bucket elsewhere.
- **Enter** — drill down one level. Year → Month, Quarter → Week, Week/Month → **Day detail**.
- **Backspace** — zoom back out one level.
- **PgUp/PgDn** — previous/next period without moving the cursor.
- **T** — by task/project, unchanged. **Esc** — leave Reports, unchanged.

⚠ This **changes existing bindings**: ←→ no longer changes period and ↑↓ no longer cycles zoom. It's a
better model (drill-down is what a calendar is *for*), but it's muscle memory being rewritten and
belongs in the release note.

```
 REPORTS                                                             Esc  Back
────────────────────────────────────────────────────────────────────────────────
 ‹ Week · [ MONTH ] · Quarter · Year ›                          August 2026
────────────────────────────────────────────────────────────────────────────────
 Worked 58h 45m       Target 64h 00m       Balance −5h 15m

 8 logged · 3 vacation · 1 holiday · 4 missing · avg 7h 20m/day

     Mon    Tue    Wed    Thu    Fri    Sat    Sun
                                  1 ^    2 ·    3 ·
      4 v    5 ^    6 ^    7 v    8 -    9 ·   10 ·
     11 ^   12 ^   13 -   14 V   15 V   16 ·   17 ·
   ►[18 -] 19 -   20 -   21 -   22 H   23 ·   24 ·
     25 -   26 -   27 -   28 -   29 -   30 ·   31 ·

 By project
   MotorPal      ████████████████████████░░░░░░░░░░░░   38h 10m    65%
   Internal      ██████████░░░░░░░░░░░░░░░░░░░░░░░░░░   14h 25m    25%
────────────────────────────────────────────────────────────────────────────────
 ←→↑↓ Day  Enter Open  Bksp Zoom out  T By task  PgUp/Dn Month  Esc Back
```

Glyphs: `^` over target, `v` under target, `-` working day with nothing logged, `·` weekend,
`V` vacation, `H` holiday, `S` sick, `T` time off in lieu, `½` a partial-credit mark.

### 5.2 Day detail — the hub

Everything in this update is reachable from here. It's the screen §5.3 of the TUI plan already
specified, plus a Day type row.

```
 REPORTS › Thu 6 Aug 2026                                            Esc  Back
────────────────────────────────────────────────────────────────────────────────
 Day type    Working day
 Attendance  08:29 → 17:41      raw 9h 12m   lunch −0h 30m   net 8h 42m   ✎
                                target 8h 00m               balance +0h 42m

 08 ─── 09 ─── 10 ─── 11 ─── 12 ─── 13 ─── 14 ─── 15 ─── 16 ─── 17 ─── 18
    ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓░░░░░▒▒▒▒▒▒▒▒▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓

  #  From    To      Project / Task                            Duration
────────────────────────────────────────────────────────────────────────────────
► 1  08:29   11:00   MotorPal / API refactor                     2h 31m
  2  11:30   12:15   Internal / Standup                          0h 45m  ✎
  3  12:15   17:41   MotorPal / Code review                      5h 26m

 ⚠ 0h 30m untracked between 11:00 and 11:30

────────────────────────────────────────────────────────────────────────────────
 ↑↓ Move  E Edit  N Add  Del Delete  A Attendance  K Day type  Esc Back
```

The untracked line is the honest reconciliation between the attendance wrapper and the segments — the
`✎` markers are §3.4's provenance. On a day with no record at all:

```
 REPORTS › Wed 29 Jul 2026                                           Esc  Back
────────────────────────────────────────────────────────────────────────────────
 Day type    Working day
 Attendance  not recorded

 Nothing was logged for this day.

   A   Add attendance at defaults          08:30 → 17:00
   M   Add attendance at custom times...
   K   Mark as vacation / holiday / sick leave...

────────────────────────────────────────────────────────────────────────────────
 A Defaults  M Custom  K Day type  Esc Back
```

### 5.3 Attendance editor (`A`)

The two-field form. Same ↑↓ / Enter / F10 shape as
[`ProjectEditScreen`](../src/SheduleHelper.Cli/Screens/ProjectEditScreen.cs); each field opens
`TimeEntryScreen` on Enter, so time parsing and validation stay in one place.

```
 EDIT ATTENDANCE › Thu 6 Aug 2026                                  Esc  Cancel
────────────────────────────────────────────────────────────────────────────────
   Clock in     08:29
 ► Clock out    17:41

   raw 9h 12m    lunch −0h 30m    net 8h 42m    balance +0h 42m

 ⚠ Moving clock-in to 09:00 would leave segment 1 (08:29–11:00) partly outside
   the day. On save:
     ‹ Clamp segments to the new window ›   Delete what falls outside
                                             Cancel

────────────────────────────────────────────────────────────────────────────────
 ↑↓ Field  Enter Edit  ←→ Conflict action  F10 Save  Esc Cancel
```

The conflict block only appears when the edit actually creates one — that's `SegmentClampMode` from
§4 surfaced as a choice rather than a silent policy.

### 5.4 Segment editor (`E` / `N`)

```
 EDIT ENTRY › Thu 6 Aug 2026                                       Esc  Cancel
────────────────────────────────────────────────────────────────────────────────
   From        11:30
   To          12:15                                             0h 45m
 ► Project     Internal
   Task        Standup

   Day window  08:29 → 17:41
   Neighbours  #1 ends 11:00        #3 starts 12:15

 ⚠ Overlaps entry #3 (12:15–17:41). Adjust one of them before saving.

────────────────────────────────────────────────────────────────────────────────
 ↑↓ Field  Enter Edit  F10 Save  Esc Cancel
```

`Project` and `Task` push the picker
[`SwitchScreen`](../src/SheduleHelper.Cli/Screens/SwitchScreen.cs) already implements, in
"pick, don't start tracking" mode. The Neighbours line exists so the user can see what a boundary is
about to collide with without leaving the form.

### 5.5 Day type (`K`) and the planner

`K` on a single day is the one-day case of the same screen, pre-filled From = To = that day.

```
 PLAN DAYS                                                         Esc  Cancel
────────────────────────────────────────────────────────────────────────────────
   Type          ‹ Vacation ›
   From          2026-08-24     Mon
   To            2026-08-28     Fri
   Credit        8h 00m per day          balance-neutral
   Skip weekends Yes
   Note          Croatia

   Affects 5 days · Mon 24 – Fri 28 Aug 2026
     Mon 24   Tue 25   Wed 26   Thu 27   Fri 28

 ⚠ Wed 26 Aug already has attendance 08:30 → 17:05.
     ‹ Keep attendance, add the mark anyway ›   Skip this day
────────────────────────────────────────────────────────────────────────────────
 ↑↓ Field  ←→ Change  Enter Edit  F10 Apply  Esc Cancel
```

Reachable from Home (`N` — plan days) and from Reports/Day detail. The **preview + explicit conflict
choice before writing** is the point: this is the one screen in the app that writes several days at
once, and silent bulk writes to a timesheet are exactly the failure mode the day-start automation
rework in v1.0.2 was about.

`Credit` defaults from the type's policy (§3.2) and is editable for half-days. `Skip weekends` on by
default — booking Mon–Fri shouldn't consume Saturday's vacation allowance.

### 5.6 Home entry points

Two new keys, both routing into screens that already exist by then:

- **`E`** — *Fix today.* Opens Day detail for today. Answers "I clocked in at 09:00 by mistake" (§2.3)
  with no new screen. When a live session is open, the attendance editor allows editing the clock-in
  only, and points at `O` for the clock-out (invariant I6).
- **`N`** — *Plan days.* Opens the planner (§5.5).

Home should also **surface the day's mark**: on a marked day, `NOT CLOCKED IN` reads
`◇ VACATION — Croatia` with the clock-in options suppressed but still reachable, so opening the app
on a booked day doesn't nag, and working anyway is still possible.

---

## 6. Worked examples

### 6.1 "I clocked in at 09:00 but I actually started at 08:30"

`E` from Home → Day detail (today) → `A` → ↑ to Clock in → Enter → `0830` → Enter → `F10`.
**6 keystrokes plus the time.** No segment conflict, because moving clock-in *earlier* only widens
the window.

### 6.2 "Last Thursday I forgot to clock out and it recorded something wrong"

`R` → arrows to Thu (or PgUp then arrows if it's last month) → `Enter` → `A` → ↓ to Clock out →
Enter → `1730` → Enter. If a segment now hangs past 17:30, the conflict block from §5.3 appears;
`←→` to *Clamp*, `F10`.

### 6.3 "Last week never got entered at all"

For each day: Reports → cursor → `Enter` → `A` (defaults 08:30 → 17:00) — one keystroke if the
defaults are right, `M` if not. Then optionally `N` on the day to add the project segments.

Whether a five-day version of this is worth building (a "backfill week at defaults" bulk action) is
§9's call — it's the same `LogAttendanceAsync` call in a loop, but it's also a bulk silent write, so it
should get the planner's preview-then-confirm treatment if it happens at all.

### 6.4 "I'm on vacation 24–28 August"

`N` from Home → Type `Vacation` → From `2026-08-24` → To `2026-08-28` → review the preview → `F10`.
Five `DayMark` rows. The month calendar shows `V`, the balance is unaffected, and the report caption
stops calling them missing days.

### 6.5 "28 September is a state holiday"

Same screen, Type `Public holiday`, From = To. One row.

Whether the app should *know* the Czech holiday calendar and offer to seed a year at once is §9's
call — the fixed-date ones are trivial and Easter Monday needs a Computus implementation (~15 lines).
It's a nice-to-have, not a blocker, and manual entry is a minute a year.

### 6.6 "I left at 12:00 for a doctor's appointment"

Clock out at 12:00 as normal (`O`). Then `E` → `K` → Type `Other`, Credit `4h 00m`, Note
`doctor` — the day reads 4h worked + 4h credited = 8h, balance 0.

---

## 7. Invariants the edit service must enforce

These are free today because the live path is a state machine. Free-form editing removes that, so
they become explicit checks in `ITimesheetEditService` — with tests, since this runs against the real
timesheet.

| | Rule | Why |
|---|---|---|
| **I1** | One attendance log per user per date | Already a unique index; the editor must produce a friendly message instead of a `DbUpdateException`. |
| **I2** | `ClockOut > ClockIn` | Matches the existing live check ([AttendanceService.cs:86-89](../src/SheduleHelper.Core/Services/AttendanceService.cs)). |
| **I3** | Every segment lies within `[ClockIn, ClockOut]` of its parent day | **Not enforced anywhere today.** Editing either boundary can violate it. Resolution is the user's choice — clamp, delete, or cancel (§5.3). |
| **I4** | Segments within a day never overlap | Structurally guaranteed today by `StartProjectTimeLogAsync`; must become an explicit check. |
| **I5** | A retro-edited day is never left with `ClockOut == null` | Otherwise it shadows the live session — see §2.6. |
| **I6** | The edit service never touches the currently open live session | Two writers on one row. Editing today's clock-in is fine; its clock-out is Home's `O`. |
| **I7** | A `DayMark` is at most one per (user, date) | Unique index; the planner resolves collisions in its preview. |
| **I8** | A segment's `ClosedReason` is preserved on edit, and set to `Switched` on creation | It drives [`ResumeLastAsync`](../src/SheduleHelper.Core/Services/TrackingService.cs) — a backfilled segment shouldn't change what tomorrow resumes. |

Deleting a day cascades to its segments already
([OnModelCreating](../src/SheduleHelper.Core/Models/LocalDbContext.cs) configures
`AttendanceLog → ProjectTimeLogs` as `Cascade`), so `DeleteAttendanceLogAsync` needs no change — but
it does need a confirmation prompt, since it's the one genuinely destructive action in the update.

---

## 8. Effort and suggested order

| # | Item | Touches | Effort |
|---|---|---|---|
| 1 | `DayDetail` read model + `GetDayAsync` | Core (new service, read only) | S |
| 2 | Reports day cursor + drill-down | `ReportsScreen`, `CalendarGrid` | S–M |
| 3 | `DayDetailScreen` (read-only first) | new CLI screen | M |
| 4 | DbContext update/delete primitives | `LocalDbContext` | S |
| 5 | Attendance editing + invariants I1–I6 | Core service + tests | **M–L** |
| 6 | `AttendanceEditScreen` incl. conflict choice | new CLI screen | M |
| 7 | Segment add/edit/delete + `SegmentEditScreen` | Core + new CLI screen + picker reuse | M |
| 8 | `DayMark` entity, migration, CRUD | Core + migration on live data | M |
| 9 | Report/balance integration for marks | `ReportingService`, `ReportBucket`, `TimeBudgetCalculator` | M |
| 10 | Date field widget + `PlannerScreen` | new widget + new CLI screen | M |
| 11 | Home `E` / `N` + mark-aware Home state | `HomeScreen` | S |
| 12 | Provenance columns + `✎` markers | migration + 2 screens | S |

**Order.** Two independent tracks that only meet at the end:

1. **Editing track** — 1 → 2 → 3 → 4 → 5 → 6 → 7. Ship 1–3 first: a read-only Day detail is
   immediately useful (it's the reconciliation view that shows untracked gaps) and it de-risks
   everything after it, because the read model is proven before anything writes.
2. **Planning track** — 8 → 9 → 10. Independent of the editing track until step 11.
3. **Then** 11 and 12.

Two things deserve extra care against real data: **step 5** (the invariants — a wrong clamp silently
destroys recorded time) and **step 8's migration** (§3.5). Rehearse both against a throwaway SQLite
file.

Realistically this is a bigger update than v1.0.2 — two new subsystems, four new screens, one
migration. If it needs splitting: **v1.0.3 = the editing track** (a complete, useful answer to "fix
last Thursday"), **v1.0.4 = the planning track**. Editing first, because it's the one that currently
forces manual SQLite.

---

## 9. Open decisions

1. **Do vacation and public holidays credit hours, or vanish from the balance?** (§3.2) Czech
   practice is that both are paid, which argues for `Credited` at the daily target — but this decides
   what the reported monthly total says, so it needs an explicit answer rather than a default.
2. **Is a `Credit` field per mark worth it, or is a plain type enum enough?** It's what makes
   half-days and short appointments expressible (§6.6). Costs one field on one screen.
3. **Provenance: two columns, a full audit table, or nothing?** (§3.4) Two columns is the
   recommendation — cheap, visible, and it means an edited row is never silently indistinguishable
   from a live one.
4. **Should Reports' arrow keys really be repurposed?** (§5.1) Drill-down is the better model but it
   rewrites existing muscle memory. Alternative: keep ←→/↑↓ as they are and put the day cursor behind
   an explicit `Enter`-to-focus-grid mode — safer, one more keystroke per use.
5. **Bulk backfill of a whole week at defaults?** (§6.3) Useful, and exactly the kind of silent
   multi-day write that v1.0.2 spent effort designing against. Preview-then-confirm or not at all.
6. **Built-in Czech public-holiday calendar?** (§6.5) ~15 lines including Easter. Convenience only.
7. **Should a working day with no log go negative?** Today it contributes zero target, so a skipped
   day is invisible rather than a deficit (§3.3). Marks make it *possible* to distinguish "excused"
   from "missing" for the first time — whether missing days should then start counting against the
   balance is a separate, and bigger, decision.

---

## 10. Deliberately out of scope

- **Editing another user's timesheet.** The app is single-user
  ([`CurrentUserContext`](../src/SheduleHelper.Core/Services/CurrentUserContext.cs)).
- **Approval workflow / submitting a timesheet.** Local tool.
- **Recurring plans** ("every second Friday off"). One-off ranges cover the actual need; recurrence is
  a rule engine.
- **Vacation-allowance accounting** (days remaining per year). Real feature, separate one — needs an
  entitlement, a carry-over policy and a year boundary.
- **CSV export.** Still open from the TUI plan's step 8, and it pairs naturally with this work, but
  it's independent of both tracks here.
