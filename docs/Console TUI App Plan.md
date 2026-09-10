# Plan: SheduleHelper.Console (Terminal UI host)

A third host application next to `SheduleHelper.Modern` (WinUI 3) and `SheduleHelper.Classic` (WPF),
reusing `SheduleHelper.Core`. This document analyses feasibility, sketches every screen, and covers
distribution and data storage.

## 0. Decisions taken

| Question | Decision |
|---|---|
| Role of the CLI | **Replaces WinUI for now.** WinUI polish is shelved; the CLI becomes the app in daily use. |
| Where new domain logic lives | **In `SheduleHelper.Core` as services** (`TrackingService`, `ReportingService`) — so WinUI inherits them when it resumes. |
| One-shot commands (`sh in`, `sh out`) | **Not now.** TUI only. Layerable later without redesign. |
| Rendering | **Raw `System.Console`.** No TUI library. See §3.4 for what that costs and how to keep it small. |

Because WinUI is shelved but not abandoned, §3's rule matters more, not less: anything that isn't
*about drawing characters on a terminal* belongs in Core.

---

## 1. Verdict on the idea

**The idea is sound, and better than it first looks — but not for the reason you gave.**

The reason it's worth doing isn't "a CLI is faster to build than WinUI". It's that
`SheduleHelper.Core` currently stops short of the domain layer, and the WinUI app is papering over
that gap with placeholders. Concretely, as of today:

| Core asset | State | Reusable by a TUI? |
|---|---|---|
| `Components/Entities/*` | Complete | Yes, as-is |
| `Models/LocalDbContext` (704 lines, full CRUD + clock-in/out + time-log API) | Complete | **Yes, as-is — this is the real asset** |
| `Models/TimeBudgetCalculator` | Complete, pure static | Yes, as-is |
| `Migrations/*`, `DatabaseMigrationService` | Complete | Yes, as-is |
| `Services/I*` (7 interfaces) | Platform-agnostic by design | Yes |
| `Resources/Strings` (en/uk resx) | Complete | Yes |
| `ViewModels/SettingsViewModel` | Complete | Consumable, but see §3 |
| `ViewModels/HomeViewModel` | **Clock-in/out only. No project tracking at all** — `TodaysTimeline` is `CreateDummyTimeline()`, hardcoded | Half of what's needed |
| `ViewModels/ProjectViewModel` | Complete | Consumable |
| `ViewModels/ProjectsAndTasksViewModel` | **Empty stub, 12 lines** | Nothing to reuse |
| `ViewModels/HistoryAndReportsViewModel` | **Empty stub, 12 lines** | Nothing to reuse |

So: the two screens you most want (projects browser, stats explorer) have no ViewModel behind them,
and the home screen's project switching — the feature that makes this a *time tracker* rather than a
*punch clock* — doesn't exist anywhere above `LocalDbContext.StartProjectTimeLogAsync`.

Building the TUI forces that logic to be written. If it's written **into Core as plain services**,
the WinUI app inherits it for free and its ViewModels get thinner. The TUI becomes the forcing
function that finishes the domain layer, not a detour from it.

`SheduleHelper.Core` targets plain `net10.0` (no Windows TFM), so a console project references it
with zero friction.

### 1.1 Honest effort estimate

Not one day for the full list. But one day for a genuinely useful app:

| Milestone | Effort |
|---|---|
| **Day 1** — skeleton (loop, frame buffer, screen stack, key bar, DI, paths) + Home + clock in/out + forgotten-session | ~1 day |
| `TrackingService` (project switch: stop A, start B; handle open segments at clock-out) | 0.5 d |
| Switch screen + Projects browser + Project observer | 0.5 d |
| `TextField` widget + Project editor + Task editor | 0.75 d |
| Settings | 0.25 d |
| `ReportingService` + Reports screen (4 zoom levels, calendar, charts) | 1 d |
| Day detail + retro-editing past entries | 0.5 d |
| Polish, resize handling, CSV export | 0.5 d |
| **Total** | **~5 days** |

Day 1 already replaces the spreadsheet for daily use. Everything after is upside.

(The editor step is priced at 0.75 d rather than 0.5 d because raw `System.Console` means writing the
text-input widget yourself — see §3.4.)

---

## 2. Layout rules

Decided up front so every screen is consistent.

- **Minimum 80×24**, grows gracefully to ~120. Every scratch below fits in 80 columns.
- **No outer box.** A top header line, a horizontal rule, content, a rule, a key bar. Full borders
  eat two columns and two rows, force padding on every line, and look fussy on resize. Rules and
  indentation read just as "app-like". (Internal boxes: avoid entirely — use blank lines and indent.)
- **Key binding scheme** (this is the rule that keeps the key bar honest):
  - `F1` help · `F10` settings · `Esc` back/cancel · `Q` quit (root only)
  - **Letters** = contextual verbs on the current screen
  - **Digits** = quick-pick the numbered row visible on screen
  - `↑↓` move · `←→` change a value · `Enter` open/confirm · `/` filter · `Ctrl+S` save
- **Colour** carries meaning, never decoration: green = surplus/positive, amber = deficit, dim =
  weekend/archived/disabled, one accent for the selected row. Must degrade to readable when
  `NO_COLOR` is set or the terminal is monochrome.
- **Live clock**: the loop redraws on keypress *or* on a 1-second tick, so elapsed timers tick.

---

## 3. Architecture: reuse Core, but write TUI-native "Screens"

Your instinct to write your own controllers is right. Recommended shape:

```
SheduleHelper.Core                        (net10.0 — unchanged except two additions)
  Components/Entities/         reuse as-is
  Models/LocalDbContext        reuse as-is   ← the real asset
  Models/TimeBudgetCalculator  reuse as-is
  Services/I*                  reuse as-is
  Resources/Strings            reuse as-is
  + Services/TrackingService   NEW  start / stop / switch project+task, resolve open segments
  + Services/ReportingService  NEW  week / month / quarter / year aggregates, per-project splits
  ViewModels/*                 NOT consumed by the TUI

SheduleHelper.Console                     (net10.0, ProjectReference → Core, no other packages)
  Program.cs                   ~30 lines: build DI, migrate, ensure user, run loop
  Infrastructure/
    ConsoleApp.cs              the loop: await key-or-tick → route → render
    ScreenStack.cs             push / pop / replace — takes the place of INavigationService
    IScreen.cs                 OnEnter() / Render(Frame) / HandleKey(key) / OnLeave()
    Frame.cs                   the char/colour buffer screens draw into (§3.4)
    Terminal.cs                VT setup, alt screen buffer, cursor, resize, teardown
    Theme.cs                   colour tokens, NO_COLOR / mono fallback
    ConsolePathProvider.cs     IPathProvider impl (see §6)
    Widgets/                   Header, KeyBar, Rule, SelectList, TextField, Toggle, Cycle,
                               ProgressBar, BlockChart, CalendarGrid, TimelineStrip
  Screens/
    HomeScreen  ClockInScreen  ClockOutScreen  ResolveForgottenScreen  SwitchScreen
    ProjectsScreen  ProjectScreen  ProjectEditScreen  TaskEditScreen
    ReportsScreen  DayDetailScreen  SettingsScreen  HelpScreen
```

Zero NuGet dependencies beyond what Core already pulls in.

`Program.cs` stays tiny because every screen is its own file and the loop is generic.

### 3.1 Why not bind the WinUI ViewModels directly

You *can* — `[ObservableProperty]` raises `PropertyChanged`, `[RelayCommand]` exposes
`IRelayCommand` with `CanExecute`. But:

1. Two of the four page ViewModels are empty stubs; there is nothing to bind to.
2. `HomeViewModel` is missing project tracking, so you'd inherit half a screen and bolt the rest on
   the side.
3. In a TUI you redraw the whole frame on each keystroke. Change notification buys you nothing —
   you'd pay the MVVM tax with no binding engine to spend it on.
4. `HomeViewModel` and `SettingsViewModel` kick off `_ = InitializeAsync()` from the constructor
   (fire-and-forget). A TUI wants to `await` load before first paint; working around that means
   polling `IsBusy` or subscribing to `PropertyChanged` just to know when it's safe to draw.

Put the shared logic in Core services instead. Then the TUI screen is ~80 lines of "read state,
draw it, handle a key" and WinUI's ViewModels shrink to adapters over the same services.

### 3.2 Services the TUI actually needs

Only five, and **zero UI abstractions**:

| Service | Impl |
|---|---|
| `IPathProvider` | new `ConsolePathProvider` (§6) |
| `ILocalDbContextFactory` | Core's, as-is |
| `DatabaseMigrationService` | Core's, as-is |
| `ICurrentUserContext` | Core's, as-is |
| `ISettingsService` | Core's, as-is |

`INavigationService`, `IDialogService`, `IDispatcherService` are **not needed** — `ScreenStack`
replaces navigation, a pushed screen replaces a dialog, and the loop is single-threaded so there is
nothing to dispatch to. (If you later decide to consume the ViewModels after all,
`IDispatcherService.Run(a) => a()` is a valid console implementation.)

### 3.3 The input loop

```
input task ──► Channel<ConsoleKeyInfo> ──► main loop:
                                             await WaitToReadAsync(timeout: 1s)
                                             key?  → screen.HandleKey(key)
                                             tick? → nothing
                                             always → Render()
```

One background reader (`Console.ReadKey(intercept: true)` in a loop), one render thread, a live clock
for free, and no `Console.KeyAvailable` busy-polling.

### 3.4 Rendering on raw `System.Console`

No library. This is very doable — the whole rendering layer is a few hundred lines — but there are
five things that must be got right, and they're all easy to get wrong by accident.

**1. Draw into a buffer, never straight to the console.**

```csharp
sealed class Frame          // Width × Height grid of (char, fg, bg)
{
    void Write(int x, int y, string text, Style style);
    void Rule(int y);                   // ─────
    void Fill(int x, int y, int w, char c, Style style);
}
```

Every screen's `Render(Frame f)` writes into the buffer. Nothing touches `Console` until the frame is
complete. This is what makes composition possible at all — otherwise widgets fight over the cursor.

**2. Flush by diffing against the previous frame.** Compare cell-by-cell against the last frame and
emit only the runs that changed, each preceded by one cursor-position escape. Redrawing all 80×24
cells every second makes the whole screen shimmer; diffing means an idle screen with a ticking clock
rewrites five characters. Roughly 40 lines, and it's the single highest-value piece of the whole
infrastructure layer.

**3. Enable virtual terminal processing, and fall back if it fails.** .NET does *not* enable
`ENABLE_VIRTUAL_TERMINAL_PROCESSING` for you. One P/Invoke at startup:

```csharp
GetConsoleMode(GetStdHandle(STD_OUTPUT_HANDLE), out var mode);
SetConsoleMode(handle, mode | 0x0004);   // ENABLE_VIRTUAL_TERMINAL_PROCESSING
```

With VT you get 256-colour/truecolor and the alternate screen buffer. If it fails (legacy conhost),
fall back to `Console.ForegroundColor` and the 16 standard colours — `Theme.cs` should expose colour
*tokens* so screens never name a colour directly and the fallback is one file's problem. Also honour
`NO_COLOR`.

**4. Use the alternate screen buffer.** `ESC[?1049h` on start, `ESC[?1049l` on exit, and
`Console.CursorVisible = false` in between. The user's shell history and prompt come back untouched
on quit. Register it in a `try/finally` **and** on `Console.CancelKeyPress` + `ProcessExit`, or a
Ctrl+C leaves their terminal in a broken state with no cursor.

**5. Poll for resize.** There's no resize event on Windows in .NET — read `Console.WindowWidth` /
`WindowHeight` at the top of each frame, and if either changed, allocate a new buffer and force a
full (non-diffed) repaint.

**What you write yourself that a library would have given you**

| Need | Cost | Notes |
|---|---|---|
| Frame buffer + diffing flush | ~120 lines | Do this first; everything else sits on it |
| Table / column layout | ~60 lines | A `PadRight`/truncate-to-width helper covers every §4 screen |
| `BlockChart` (`▁▂▃▄▅▆▇█`) | ~20 lines | Ratio → glyph lookup. Genuinely trivial |
| `CalendarGrid` | ~50 lines | Month grid + per-day cell renderer callback |
| `ProgressBar`, `Rule`, `KeyBar` | ~40 lines | One-liners each |
| **`TextField`** | **~90 lines** | **The real cost.** Cursor position, insert, backspace, delete, Home/End, ←→, horizontal scroll when the value is wider than the field |

Only `TextField` is genuinely tedious. Two mitigations, pick either:
- **Modal single-field edit** — `Enter` on a form row clears the content area, shows one prompt for
  that field, `Enter` commits and returns. Reuses one `TextField` instance everywhere. Simplest.
- **In-place editing** — the field edits where it sits in the form. Nicer to use, same widget, just
  needs the form to route keys to the focused field. Also fine, slightly more wiring.

Either way, write `TextField` **once**, before the first editor screen, and never again.

**Character-width caveat**: all layout maths assumes one column per char. True for Latin and Cyrillic
(both shipping locales), false for CJK and emoji. If a project name ever contains an emoji the row
will be one column off. Acceptable — but avoid emoji in the chrome itself, which is why the scratches
use `●○▶▓░⚠` (all single-width) rather than emoji.

---

## 4. Screen scratches

### 4.1 Home — clocked in

```
 SCHEDULE HELPER                                    Mon 2 Aug   14:32   F1 Help
────────────────────────────────────────────────────────────────────────────────
 ● CLOCKED IN since 08:34                                     Balance  +2h 15m
   ████████████████████████████░░░░░░░░░░░  5h 28m / 8h 00m   this month

 Active   ▶ MotorPal / API refactor                                    1h 12m
            started 13:20

 Today    08:34 ──────────────────────────────────────────────────────  14:32
          ▓▓▓▓▓▓▓▓▓▓▓▓▓▒▒▒▒▒▒▒░░░░░▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓
          ▓ MotorPal 3h 05m    ▒ Internal 1h 10m    ░ lunch 0h 30m

 Recent   1 MotorPal / API refactor          2 Internal / Standup
          3 MotorPal / Code review

────────────────────────────────────────────────────────────────────────────────
 O Clock out   S Switch   1-3 Quick switch   P Projects   R Reports   Q Quit
```

The `Today` strip is the CLI equivalent of `TimelineBar` from the WinUI app — same
`TimelineSegment` data, one row of block characters instead of a custom control.

### 4.2 Home — not clocked in

```
 SCHEDULE HELPER                                    Mon 2 Aug   08:12   F1 Help
────────────────────────────────────────────────────────────────────────────────
 ○ NOT CLOCKED IN                                             Balance  +2h 15m
   ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  0h 00m / 8h 00m  this month

 Good morning. Ready when you are.

     I   Clock in now                     08:12
     D   Clock in at default              08:30
     M   Clock in at custom time…

 Yesterday   08:29 → 17:04    net 8h 05m    +0h 05m

────────────────────────────────────────────────────────────────────────────────
 I Clock in   P Projects   R Reports   F10 Settings   Q Quit
```

Maps 1:1 onto `HomeViewModel`'s existing `ClockInNow` / `ClockInAtDefaultTime` /
`ClockInAtCustomTime` commands.

### 4.3 Home — forgotten session

`AttendanceDayState.ForgottenSession` already exists in Core and the WinUI app has nowhere good to
put it. A CLI handles it better — it's a blocking modal by nature.

```
 SCHEDULE HELPER                                    Tue 3 Aug   09:02   F1 Help
────────────────────────────────────────────────────────────────────────────────
 ⚠ UNFINISHED DAY

   Mon 2 Aug — clocked in at 08:34, never clocked out.
   Close it before starting today.

     D   Clock out at default          Mon 2 Aug  17:00   → net 7h 56m
     L   At last tracked activity      Mon 2 Aug  16:41   → net 7h 37m
     M   Clock out at custom time…

────────────────────────────────────────────────────────────────────────────────
 Esc Decide later   Q Quit
```

`L` is new — infer the clock-out from the last `ProjectTimeLog.EndTime` of that day. Cheap to add,
and it's usually the right answer.

### 4.4 Clock out

```
 CLOCK OUT                                                          Esc  Cancel
────────────────────────────────────────────────────────────────────────────────
 Clocked in 08:34 · worked 8h 02m · target 8h 00m

 ► Now                        17:06      net 8h 02m     balance  +0h 02m
   Default clock-out          17:00      net 7h 56m     balance  −0h 04m
   Exactly on target          17:04      net 8h 00m     balance   0h 00m
   Custom time…             [ 17:06 ]

   Also stop active project (MotorPal / API refactor)            [ YES ]

────────────────────────────────────────────────────────────────────────────────
 ↑↓ Select   Enter Confirm   Esc Cancel
```

"Exactly on target" is a CLI-native affordance — trivial arithmetic, surprisingly useful, awkward to
express as a button in a GUI.

### 4.5 Switch project / task

```
 SWITCH                                                             Esc  Cancel
────────────────────────────────────────────────────────────────────────────────
 Filter  ▏mot▕                                                        3 of 12
────────────────────────────────────────────────────────────────────────────────
 ► MotorPal                                        today 3h 05m    ▾ 4 tasks
     ├ API refactor                 in progress     today 1h 12m
     ├ Code review                  todo            today     —
     └ (project only — no task)
   MotorPal Internal                                today 1h 10m    ▸ 2 tasks
   MotorPal Docs                    archived                        ▸ 1 task

────────────────────────────────────────────────────────────────────────────────
 ↑↓ Move  ←→ Fold  Enter Start  X Stop tracking  N New project  / Filter  Esc
```

Type-to-filter is the killer feature here and the reason a CLI beats the GUI for this interaction —
three keystrokes to switch project, versus reach-for-mouse-and-click.

### 4.6 Projects browser

```
 PROJECTS                                                            Esc  Back
────────────────────────────────────────────────────────────────────────────────
  #  Project                Tasks    This week   This month      Total   Status
────────────────────────────────────────────────────────────────────────────────
► 1  MotorPal                  12     14h 20m     58h 45m     241h 10m   active
  2  Internal                   4      3h 05m     11h 30m      47h 22m   active
  3  Onboarding                 2          —       2h 15m      18h 40m   archived

────────────────────────────────────────────────────────────────────────────────
 ↑↓ Move  Enter Open  N New  E Edit  A Archive  Del Delete  / Filter  Esc Back
```

### 4.7 Project observer

```
 PROJECTS › MotorPal                                                 Esc  Back
────────────────────────────────────────────────────────────────────────────────
 Refactor of the dispatch API and its clients.            active · since 4 Mar

 Time     week 14h 20m    month 58h 45m    quarter 132h 05m    total 241h 10m
          ▁▂▅█▇▃▁  last 7 days

 Tasks                                                          12 · 3 done
────────────────────────────────────────────────────────────────────────────────
  #  Task                          Status          Logged     Last worked
► 1  API refactor                  in progress    34h 12m     today 13:20
  2  Code review                   todo            2h 05m     Fri 29 Jul
  3  Migration script              done           19h 40m     Tue 26 Jul

────────────────────────────────────────────────────────────────────────────────
 Enter Track  N New task  E Edit  D Cycle status  Del Delete  P Edit project  Esc
```

Backed by `ProjectViewModel`'s existing shape (`Tasks`, `SelectedTask`, create/edit/delete task,
edit/delete project) — if you go the reuse route, this is the one screen where it's a clean fit.

### 4.8 Project editor

```
 NEW PROJECT                                                       Esc  Cancel
────────────────────────────────────────────────────────────────────────────────

   Name           ▏MotorPal                                     ▕
 ► Description    ▏Refactor of the dispatch API and its clients ▕
   Active         [ YES ]

   ⚠ A project with this name already exists.

────────────────────────────────────────────────────────────────────────────────
 ↑↓ Field   Enter Edit   ←→ Toggle   Ctrl+S Save   Esc Cancel
```

### 4.9 Task editor

```
 MotorPal › EDIT TASK                                              Esc  Cancel
────────────────────────────────────────────────────────────────────────────────

   Title          ▏API refactor                                 ▕
   Description    ▏Split the dispatch controller, add tests.    ▕
 ► Status         ‹ Todo · [ In progress ] · Done ›

   Logged         34h 12m across 9 sessions

────────────────────────────────────────────────────────────────────────────────
 ↑↓ Field   Enter Edit   ←→ Change   Ctrl+S Save   Esc Cancel
```

### 4.10 Settings

Refined from your sketch. Every row here maps to an existing property on `UserSetting` or
`AppSettingsData` — no new persistence needed.

```
 SETTINGS                                                            Esc  Back
────────────────────────────────────────────────────────────────────────────────
 Shift
 ► Target shift hours                                            [ 8.00 ]
   Default clock-in                                              [ 08:30 ]
   Default clock-out                                             [ 17:00 ]

 Lunch
   Strategy                          ‹ None · [ Fixed window ] · Duration ›
   Window                                          [ 11:00 ] – [ 11:30 ]
   Duration                                                     [ 30 min ]

 Application
   Language                                 ‹ [ English ] · Українська ›
   Colour scheme                            ‹ [ Dark ] · Light · None ›
   Week starts on                           ‹ [ Monday ] · Sunday ›

 ● unsaved changes

────────────────────────────────────────────────────────────────────────────────
 ↑↓ Select  ←→ Change  Enter Edit  Ctrl+S Save  Ctrl+Z Discard  Esc Back
```

Rows irrelevant to the current strategy render dim (`Duration` when Fixed window is selected, and
vice versa) rather than disappearing — the layout stays stable as you cycle.

`Week starts on` is new and needs a field on `AppSettingsData`; reporting needs it.

---

## 5. Reports — solving the "small window" problem

This is the part you said you couldn't figure out. Here's the design.

**The trick: change the bucket size with the zoom level so the bar count stays constant.**

| Period | Bucket | Bars | Columns needed |
|---|---|---|---|
| Week | day | 7 | 7 |
| Month | day | 28–31 | 31 |
| Quarter | week | 13 | 13 |
| Year | month | 12 | 12 |

The chart never outgrows 80 columns, at any zoom. That's what makes a single screen work.

**Three stacked bands, each answering a different question:**

1. **The number** — balance, worked vs target. What you actually came to find out.
2. **The shape** — how it's distributed over the period.
3. **The split** — which projects it went to.

### 5.1 Reports — month (calendar mode)

For **Month**, the calendar grid beats a bar chart: it's the mental model people already have, it
makes weekends legible for free, and it's the natural entry point for retro-editing a specific day.

```
 REPORTS                                                             Esc  Back
────────────────────────────────────────────────────────────────────────────────
 ‹ Week · [ MONTH ] · Quarter · Year ›       August 2026        ‹ › change period
────────────────────────────────────────────────────────────────────────────────
 Worked  58h 45m       Target  64h 00m       Balance  −5h 15m

 Days    8 of 21 logged      avg 7h 20m/day      best Tue 5th  9h 12m

     Mon    Tue    Wed    Thu    Fri    Sat    Sun
                                    1      2      3
                                +0:12     ·      ·
       4      5      6      7      8      9     10
   −0:20  +1:12  +0:05  −2:40      ·      ·      ·
      11     12     13     14     15     16     17
   +0:35  +0:05      ·      —      —      ·      ·
    ► 18     19     20     21     22     23     24
       —      —      —      —      —      ·      ·

 By project
   MotorPal      ████████████████████████████░░░░░░░░   38h 10m    65%
   Internal      ██████████░░░░░░░░░░░░░░░░░░░░░░░░░░   14h 25m    25%
   Onboarding    ████░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░    6h 10m    10%

────────────────────────────────────────────────────────────────────────────────
 ←→ Period  ↑↓ Zoom  Enter Day detail  T By task  X Export CSV  Esc Back
```

Green `+`, amber `−`, dim `·` weekend, `—` not logged. `Enter` on a day opens §5.3.

### 5.2 Reports — quarter (bar mode)

Same screen, different band 2. Week and Year use this shape too.

```
 REPORTS                                                             Esc  Back
────────────────────────────────────────────────────────────────────────────────
 ‹ Week · Month · [ QUARTER ] · Year ›       Q3 2026         ‹ › change period
────────────────────────────────────────────────────────────────────────────────
 Worked  412h 30m      Target  424h 00m      Balance  −11h 30m

 Weeks   9 of 13 logged     avg 45h 50m/week     best W31  52h 10m

 Per week                                          ─ ─ ─ target 40h ─ ─ ─
        █           █
      █ █ █   ▆   █ █ ▆
    ▄ █ █ █ ▄ █ ▂ █ █ █
  ──┴─┴─┴─┴─┴─┴─┴─┴─┴─┴─┴─┴─┴──
   W27      W30      W33      W36      W39

 By project
   MotorPal      ██████████████████████████████░░░░░░  268h 05m    65%
   Internal      ████████████░░░░░░░░░░░░░░░░░░░░░░░░   99h 00m    24%
   Onboarding    ██████░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░   45h 25m    11%

────────────────────────────────────────────────────────────────────────────────
 ←→ Period  ↑↓ Zoom  Enter Week detail  T By task  X Export CSV  Esc Back
```

Bars use `▁▂▃▄▅▆▇█` — eight levels per row, stack 3–4 rows for real resolution. About 15 lines of
code, no library needed. The dashed target line is what makes the chart readable at a glance.

`T` swaps the "By project" band for "By task", same rendering.

### 5.3 Day detail (and the retro-edit path)

This is blueprint Tab 3's "click a day to adjust it", which nothing implements yet.

```
 REPORTS › Tue 5 Aug 2026                                            Esc  Back
────────────────────────────────────────────────────────────────────────────────
 Attendance   08:29 → 17:41       raw 9h 12m   lunch −0h 30m   net 8h 42m
                                  target 8h 00m           balance  +0h 42m

 08 ─── 09 ─── 10 ─── 11 ─── 12 ─── 13 ─── 14 ─── 15 ─── 16 ─── 17 ─── 18
    ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓░░░░░▒▒▒▒▒▒▒▒▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓

  #  From    To      Project / Task                            Duration
────────────────────────────────────────────────────────────────────────────────
► 1  08:29   11:00   MotorPal / API refactor                     2h 31m
  2  11:00   11:30   (lunch — auto-deducted)                     0h 30m
  3  11:30   12:15   Internal / Standup                          0h 45m
  4  12:15   17:41   MotorPal / Code review                      5h 26m

  ⚠ 0h 00m untracked — all attendance time is assigned

────────────────────────────────────────────────────────────────────────────────
 ↑↓ Move  E Edit entry  N Add  Del Delete  A Edit attendance  Esc Back
```

The "untracked" line is worth keeping — it's the honest reconciliation between the attendance
wrapper and the project logs, and it's how you notice a forgotten switch.

### 5.4 Help overlay (`F1`)

```
 HELP                                                                Esc  Close
────────────────────────────────────────────────────────────────────────────────
 Global          F1  this help          F10  settings
                 Esc back / cancel        Q  quit (from Home)

 Lists           ↑↓  move                 /  filter
                 Enter open           1..9  jump to numbered row

 Home             I  clock in            O  clock out
                  S  switch project      P  projects        R  reports

 Editors        Tab  next field     Ctrl+S  save        Ctrl+Z  discard

────────────────────────────────────────────────────────────────────────────────
 Esc Close
```

---

## 6. Data, logs, settings, and distribution

`IPathProvider` already exists as the single seam for all of this, so none of it is locked in.

### 6.1 Where data lives

```
%LOCALAPPDATA%\SheduleHelper\
    data.db
    settings.json
    logs\
        log-20260802.txt     (rolling daily, keep 7)
```

**Default to `%LOCALAPPDATA%`** (`Environment.GetFolderPath(SpecialFolder.LocalApplicationData)`),
not next to the exe. Writing beside the executable breaks the moment the app lands under
`C:\Program Files`, so it can't be the unconditional default.

**Opt into true portable mode** with either:
- a marker file `portable.txt` next to the exe → everything goes in the exe's folder, or
- `--data-dir <path>` on the command line, or
- the `SHEDULEHELPER_DATA` environment variable.

Explicit opt-in beats a "is this folder writable?" heuristic — predictable is worth more than clever.

**Logging**: Serilog is already referenced in Core, and only the **Debug** and **File** sinks are
pulled in. Keep it that way — a Console sink would write over the TUI frame and corrupt the display.

### 6.2 Distribution: recommended path

**Primary — GitHub Releases + winget.**

```bash
dotnet publish src/SheduleHelper.Console -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Notes that will bite otherwise:
- **Set `PublishTrimmed=false`.** The Modern csproj turns trimming on for Release; EF Core leans on
  reflection and trimming it is a reliable way to get runtime failures. Don't inherit that setting.
- **Set `IncludeNativeLibrariesForSelfExtract=true`.** `SQLitePCLRaw.bundle_e_sqlite3` carries a
  native `e_sqlite3.dll`; without this it gets extracted to a temp directory on every launch.
- Self-contained, untrimmed lands around 60–80 MB. Framework-dependent (requires the .NET 10 runtime)
  is ~2 MB — fine if the audience is you and colleagues who already have it.

Then publish a manifest to `microsoft/winget-pkgs` pointing at the release asset:
`winget install SheduleHelper`. Free, no account fees, and it is *the* distribution channel developers
actually use for CLI tools.

### 6.3 Distribution: Microsoft Store

Possible, but a poorer fit — worth knowing the shape of it before committing:

- The Store accepts console apps packaged as MSIX. To make `sh` callable from an already-open
  terminal you declare an **app execution alias** in the manifest
  (`<uap:Extension Category="windows.appExecutionAlias">`) — the same mechanism Windows Terminal uses.
  Without it, launching from Start just spawns a new console window.
- Requires a Partner Center account (individual ~$19 one-time, company ~$99), Store logos and
  screenshots, an age rating, and a **privacy policy URL** — a time tracker handles personal data,
  even though it never leaves the machine, so plan on having one.
- **Data location under MSIX** becomes `%LOCALAPPDATA%\Packages\<PackageFamilyName>\LocalState\`.
  `SheduleHelper.Modern`'s `PathProvider` already does exactly this via `ApplicationData.Current`.
  But `ApplicationData` needs a Windows TFM (`net10.0-windows10.0.19041.0`), which would stop the
  same binary from being portable. Two clean ways out:
  - detect package identity with a one-line `GetCurrentPackageFullName` P/Invoke (returns
    `APPMODEL_ERROR_NO_PACKAGE` when unpackaged) and branch inside `ConsolePathProvider`; or
  - ship the Store build from a separate csproj with the Windows TFM and a `PackagedPathProvider`.
- Either way this is a **later** decision, not a now decision — `IPathProvider` keeps it cheap.

**Recommendation: portable + winget first. Revisit the Store only if you want the app discoverable
by non-developers.**

### 6.4 The WinUI database, when it comes back

Since the CLI replaces WinUI for now, there's nothing to reconcile *today* — the CLI owns
`%LOCALAPPDATA%\SheduleHelper\data.db` and that's the only database in play.

The thing to be aware of for later: WinUI Release resolves to the MSIX package's `LocalState`
(`%LOCALAPPDATA%\Packages\<PFN>\LocalState\`), which is **not** the same folder. When WinUI resumes,
one of three things has to happen:

- point WinUI's `PathProvider` at `%LOCALAPPDATA%\SheduleHelper` — requires going unpackaged, or
  `runFullTrust` plus an explicit path;
- keep them separate and accept two databases; or
- write a one-time import that copies the CLI's `data.db` into `LocalState` on first WinUI launch.

No action needed now. Noted only so the CLI's months of real data don't come as a surprise later.

---

## 7. Suggested build order

1. **Skeleton** — console project, DI, `ConsolePathProvider`, migrate, `Terminal` (VT + alt buffer +
   teardown), `Frame` + diffing flush, `ScreenStack`, loop, `Header`/`Rule`/`KeyBar`, `HelpScreen`.
   Nothing domain-specific. Get the flush right here and every later screen is easy.
2. **Home + attendance** — `HomeScreen`, `ClockInScreen`, `ClockOutScreen`, `ResolveForgottenScreen`,
   `SelectList` + `ProgressBar`. All four map onto `LocalDbContext` methods that already exist.
   *End of day 1: it replaces the spreadsheet.*
3. **`TrackingService` in Core** — start / stop / switch, close the open segment on clock-out. The
   first genuinely new domain logic, and the thing WinUI needs too.
4. **`SwitchScreen`** + the live `Active` and `Today` bands on Home (`TimelineStrip`).
5. **`TextField`**, then **Projects** — browser, observer, project editor, task editor. Write the
   widget before the first screen that needs it, not during.
6. **Settings** — a direct transcription of `UserSetting` + `AppSettingsData` (`Toggle`, `Cycle`).
7. **`ReportingService` in Core** + `ReportsScreen` — four zoom levels, `BlockChart` + `CalendarGrid`.
8. **`DayDetailScreen`** + retro-editing + CSV export.

Steps 3 and 7 are the ones that pay a dividend back to the WinUI app when it resumes — they're the
justification for building the CLI rather than just a reason it's fun.

---

## 8. Automation and live data (added after two days of daily use)

Three things surfaced from actually living with the app: the morning ritual is repetitive, work that
spans days has to be re-selected every morning, and Home looks alive (the clock ticks) while the
numbers that matter are frozen. All three are one change to the same area.

### 8.1 The bug this uncovered

`ClockOutAsync` closed the `AttendanceLog` but not the still-running `ProjectTimeLog`. Both
`SummarizeByProject` and `SummarizeByTask` skip segments with a null `EndTime`, so **every clock-out
made while tracking a project silently dropped that segment's time from every report.** §4.4's
sketch had this as an optional "Also stop active project [ YES ]" row; it isn't optional, because the
alternative to stopping it is losing it. `LocalDbContext.ClockOutAsync` now closes the open segment
at the clock-out timestamp (clamped to the segment's own start, so a retro clock-out can't produce a
negative duration).

Segments left open by clock-outs *before* this fix stay as they are — they're treated as resumable
(see below) rather than rewritten, since guessing an end time for them is worse than leaving the data
visibly odd.

### 8.2 `ClosedReason` — why a segment stopped

New nullable column on `ProjectTimeLog` (`Switched` / `ClockedOut` / `Stopped`; null while open).

It exists because "continue what I was doing" and "I deliberately stopped" are indistinguishable from
timestamps alone. With it, the resume target is simply *the most recent segment whose reason isn't
`Stopped`* — which means switching projects moves the target for free, and no separate "current
project" pointer has to be kept in sync with the logs.

### 8.3 `ResolveDayStartAsync`

`IAttendanceService` gains one method that performs the morning sequence and reports what it did:

1. Close a previous day left open, at **that day's** `DefaultClockOutTime`.
2. Clock today in at `DefaultClockInTime`, or at the current time if the default is still in the
   future (an 08:12 arrival can't be written as 08:30 — the service rejects future timestamps).
3. Resume the last tracked project.

Guards, each of which exists because the alternative writes something wrong:

| Situation | Behaviour |
|---|---|
| Saturday or Sunday | No clock-in. Opening the app to read a report must not start a shift. |
| Default clock-out is before that day's clock-in (a late shift) | Day left open for manual resolution rather than closed at a nonsense time. |
| Today already has a log | Nothing; not an error. |
| Project since archived | Not resumed, and says so. |
| Task since `Done` | Project resumes, task dropped — quietly logging more time against a finished task is worse than asking for the next one. |

Two `UserSetting` columns drive it: `DayStartAutomation` (`Off` / `CloseForgottenDays` /
`CloseAndClockIn` — cumulative rather than independent flags, since "clock today in while yesterday
is still open" isn't a state worth offering) and `ResumeTrackingOnClockIn`. Migration
`DayStartAutomation`; its scaffolded `AddColumn` defaults were hand-corrected — `""` is not a valid
enum name and would fail on read, and `false` contradicted the entity's own initializer.

Automation is **off by default** because it writes attendance on the user's behalf. Resume is **on**,
because it only takes effect at a clock-in the user asked for and is visible and switchable the
moment it happens.

`HomeScreen` owns the call, keyed on calendar date rather than on activation: it runs before the
first frame, doesn't re-run when you pop back from Settings, and *does* run again when the date rolls
over under an app left running overnight. Whatever it changed is reported in a banner above the key
bar (`⚙ Auto · closed Fri 31 Jul at 17:00 · clocked in 08:30 · resumed MotorPal / API refactor`),
dismissed with `Esc`. Silent timesheet edits are the failure mode to design against here; every
action it takes is correctable with the same `O` and `S` the user would have pressed anyway.

Resume is deliberately tied to the clock-in itself, not to how it was triggered — a manual `I`/`D`/`M`
continues yesterday's project exactly as an automatic clock-in does.

### 8.4 Live Home

The loop already ticks every second and `FrameRenderer` already diffs, so the frame was never the
problem: `_snapshot` was captured once in `OnEnter` and `WorkedToday` was a fixed number. The active
segment's elapsed time only looked alive because it was computed as `DateTime.Now - StartTime` at
render time.

`AttendanceDaySnapshot` now exposes `WorkedAsOf(asOf)`, `DailyTarget` and `OpenDayBalanceAsOf(asOf)` —
pure arithmetic over data the snapshot already carries, no database access, safe to call once per
frame from `Render` without breaking the "render is a pure projection" rule in `IScreen`. The progress
bar, the worked-versus-target readout and the lunch deduction all move in real time.

**No polling.** Nothing but this process writes the database, so re-querying every second would only
burn I/O. The one thing that genuinely has to be re-read is the calendar date, which is what the new
`IScreen.OnTick` (default no-op, awaited on the loop thread like `HandleKey`) is for.

`RollingMonthlyBalance` counts completed days only, so it *cannot* move during the day. Rather than
redefine it, Home shows today's still-moving contribution as a separate dim figure —
`Balance +2h 15m · today +0h 12m`. The two answer different questions: one is banked, the other is a
projection a late clock-out will still change. `OpenDayBalanceAsOf` returns null once the day is
complete, since by then the rolling balance already includes it.

### 8.5 What this leaves for later

- `L — at last tracked activity` from §4.3 is still unimplemented; it would be a better default for
  closing a forgotten day than `DefaultClockOutTime`, and would make a good second option on the
  `Day start` setting.
- Step 8 of §7 (day detail, retro-editing, CSV export) is still open. `ClosedReason` will earn its
  keep there too — it's the difference between a gap in the timeline and a deliberate break.
