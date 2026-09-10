# Project Blueprint: Universal Time & Task Tracker (MVP to Git-Integrated Platform)

## 1. Project Overview & Vision

This application is designed to replace tedious, manual spreadsheet logging with an effortless, low-friction time-tracking assistant.

Unlike traditional stopwatch-style tracking apps, this platform operates on a flexible daily budget system. It understands that developer workdays are fluid—some days run short, others run long, and breaks happen. By separating physical office presence (Attendance) from focused work assignments (Project Logs), the application automatically maintains a rolling hourly "bank balance" (surplus/deficit) dynamically computed based on customizable, user-defined rules.

## 2. Core Concepts & App Mechanics

### 2.1 The Two-Tiered Logging Model

To keep logs accurate and clean, the app tracks time on two independent layers:

- **Attendance Logs (The Wrapper)**: Tracks when you entered and exited the office/work space.
  - Arrival (Clock In) → establishes the day's boundaries.
  - Departure (Clock Out) → finalizes the total daily presence.
- **Project & Task Logs (The Focus)**: Track exactly what you worked on during those attendance hours.
  - Continuous timeline: Clocking into Project B automatically stops the clock on Project A.

```
[Arrival: 08:00] --------------------------------------------------> [Departure: 17:00]
    |-- Project A: 08:00 - 11:00 --|
                                   |-- Lunch: 11:00 - 11:30 --| (Auto-pause / Untracked)
                                                              |-- Project B: 11:30 - 17:00 --|
```

### 2.2 The Live Rolling Budget Formula

The core metric of the application is the Dynamic Rolling Budget. The application computes this without storing static totals, avoiding data-desync issues.

**Step 1: Calculate Raw Attendance Time ($T_{\text{raw}}$)**

$$T_{\text{raw}} = \text{ClockOut} - \text{ClockIn}$$

**Step 2: Subtract Break Time ($T_{\text{break}}$)**

Based on the user's `LunchStrategy`:

- **Fixed Window**: If $\text{ClockIn} \le \text{LunchStartTime}$ and $\text{ClockOut} \ge \text{LunchEndTime}$, then:
  $$T_{\text{break}} = \text{LunchEndTime} - \text{LunchStartTime}$$
- **Duration Based**: If $T_{\text{raw}} \ge 6 \text{ hours}$ (or custom threshold):
  $$T_{\text{break}} = \text{LunchDurationMinutes}$$
- **None**:
  $$T_{\text{break}} = 0$$

**Step 3: Compute Daily Net Work Time ($T_{\text{net}}$)**

$$T_{\text{net}} = T_{\text{raw}} - T_{\text{break}}$$

**Step 4: Calculate Daily Balance ($B_{\text{day}}$)**

$$B_{\text{day}} = T_{\text{net}} - \text{TargetShiftHours}$$

**Step 5: Rolling Monthly Balance ($B_{\text{month}}$)**

$$B_{\text{month}} = \sum_{i=1}^{N} B_{\text{day}, i}$$

Where $N$ is the number of logged working days in the current tracking period.

## 3. UX & Wireframe Blueprints

### Tab 1: Home (The Daily Control Center)

Designed for split-second interactions during a busy workday.

- **Top Header (The "Live Budget" Card)**:
  - Displays the cumulative month/week balance clearly (e.g., +2.5 hours in emerald green or -1.2 hours in warm amber).
  - Sub-text shows today's ongoing tally: "Worked 5h 15m of 8h 00m target".
- **The Smart Punch Action Bar**:
  - Displays a single primary button: "Start Day" or "Finish Day".
  - Shows default action values (e.g., "Press to start at 08:30").
  - A small edit icon beside it allows manual clock-in/out override adjustments directly from the landing view.
- **Active Project Context Switcher**:
  - Shows a list of active projects with an immediate "Switch To" action.
  - Clicking an inactive project instantly ends the running project session and starts tracking the selected project.
  - Shows a live inline ticking timer for the currently tracked project.
- **Daily Timeline Visualizer**:
  - A horizontal progress bar representing the workday.
  - Colors visually represent time slots: green for Project A, blue for Project B, and a dark-gray hatched pattern marking out the automatically deducted lunch window (e.g., 11:00 - 11:30).

### Tab 2: Projects & Tasks (The Organizer)

- **Project View**:
  - Cards showing active projects, aggregated total logged hours, and descriptions.
  - Simple toggle switch to archive/activate projects.
- **Task View**:
  - Under each project card, users can unfold simple task checklists (Todo, In Progress, Done).
  - Add a button: "Track This Task" which automatically updates the active home-screen tracker.
  - Visual Integration Hook Placeholder: A greyed-out or discrete integration badge next to task headers showing "Connect to Git repo to link tickets".

### Tab 3: History & Reports (The Evaluator)

- **Monthly Grid Calendar**:
  - Days colored green for positive budget days, orange for negative budget days, and grey for holidays/weekends.
  - Click on any day to open a details modal to retroactively adjust arrival, departure, or project/task duration allocations.
- **Export Engine**:
  - A quick export panel with the option to "Export to CSV/Excel" with columns custom-formatted for month-end workspace evaluations.

### Tab 4: Settings (The Rule Setter)

- **Baseline Settings**:
  - Shift duration input (e.g., 8.00, 7.50, 6.00 hours).
- **Default Values**:
  - Set Standard Arrival (e.g., 08:30) and Standard Departure (e.g., 17:00) to enable one-click smart logging.
- **Break Management**:
  - Toggle list: Fixed Window (start/end parameters), Duration-based (deduct after $X$ hours of presence), or Manual/No deduction.

## 4. Technical Architecture (SQLite Native)

The SQLite schema is optimized to keep the core operations light while maintaining total integrity. Database operations must be run with `PRAGMA foreign_keys = ON;`.

```
                  +-------------------+
                  |       Users       |
                  +-------------------+
                            | (1:1)
                  +-------------------+
                  |   UserSettings    |
                  +-------------------+
                            | (1:N)
                  +-------------------+
                  |  AttendanceLogs   | <---+
                  +-------------------+     |
                            | (1:N)         | (1:N)
   +------------+ 1:N  +------------------+ |
   |  Projects  | <--- |  ProjectTimeLogs |-+
   +------------+      +------------------+
         | (1:N)            | (0..1:N)
   +------------+           |
   |   Tasks    | <---------+
   +------------+
```

### SQL Table Schema (SQLite Syntax)

```sql
PRAGMA foreign_keys = ON;

CREATE TABLE Users (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Username TEXT NOT NULL,
    Email TEXT NOT NULL UNIQUE,
    CreatedAt TEXT DEFAULT (CURRENT_TIMESTAMP)
);

CREATE TABLE UserSettings (
    UserId INTEGER PRIMARY KEY,
    TargetShiftHours NUMERIC DEFAULT 8.00,
    DefaultClockInTime TEXT DEFAULT '08:30:00',
    DefaultClockOutTime TEXT DEFAULT '17:00:00',
    LunchStrategy TEXT DEFAULT 'FIXED_WINDOW', -- 'FIXED_WINDOW', 'DURATION_BASED', or 'NONE'
    LunchStartTime TEXT DEFAULT '11:00:00',
    LunchEndTime TEXT DEFAULT '11:30:00',
    LunchDurationMinutes INTEGER DEFAULT 30,
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);

CREATE TABLE Projects (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId INTEGER NOT NULL,
    Name TEXT NOT NULL,
    Description TEXT,
    IsActive INTEGER DEFAULT 1, -- 1: Active, 0: Archived
    CreatedAt TEXT DEFAULT (CURRENT_TIMESTAMP),
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);

CREATE TABLE Tasks (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ProjectId INTEGER NOT NULL,
    Title TEXT NOT NULL,
    Description TEXT,
    Status TEXT DEFAULT 'Todo', -- 'Todo', 'InProgress', 'Done'
    CreatedAt TEXT DEFAULT (CURRENT_TIMESTAMP),
    FOREIGN KEY (ProjectId) REFERENCES Projects(Id) ON DELETE CASCADE
);

CREATE TABLE AttendanceLogs (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId INTEGER NOT NULL,
    WorkDate TEXT NOT NULL,
    ClockIn TEXT NOT NULL, -- 'YYYY-MM-DD HH:MM:SS'
    ClockOut TEXT NULL, -- 'YYYY-MM-DD HH:MM:SS' or NULL
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_User_WorkDate UNIQUE (UserId, WorkDate)
);

CREATE TABLE ProjectTimeLogs (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    AttendanceLogId INTEGER NOT NULL,
    ProjectId INTEGER NOT NULL,
    TaskId INTEGER NULL,
    StartTime TEXT NOT NULL, -- 'YYYY-MM-DD HH:MM:SS'
    EndTime TEXT NULL, -- 'YYYY-MM-DD HH:MM:SS' or NULL
    FOREIGN KEY (AttendanceLogId) REFERENCES AttendanceLogs(Id) ON DELETE CASCADE,
    FOREIGN KEY (ProjectId) REFERENCES Projects(Id) ON DELETE CASCADE,
    FOREIGN KEY (TaskId) REFERENCES Tasks(Id) ON DELETE SET NULL
);
```

## 5. Phased Development Roadmap

```
+-----------------------------------+
|  PHASE 1: Core Engine & Settings   | <- Local SQLite Database, Clock In/Out, Settings
+-----------------------------------+
                  |
+-----------------------------------+
|  PHASE 2: Task Board & Reports    | <- Task List, History Overrides, CSV Exporters
+-----------------------------------+
                  |
+-----------------------------------+
|  PHASE 3: Git Integration Hub     | <- DB Migration, API sync with GitHub/Gitea/GitLab
+-----------------------------------+
                  |
+-----------------------------------+
|  PHASE 4: Enterprise Sync & Cloud | <- Secure Sync, Multi-Device, Shared Projects
+-----------------------------------+
```

### Phase 1: Core Engine & Settings (The MVP)

**Goal**: Build a robust, working local tracker that replaces your Excel spreadsheet.

**Deliverables**:
- Configure SQLite data layer.
- Settings view for target hours, arrival/departure defaults, and break rules.
- Home Screen tracker interface featuring basic "Clock In/Out" operations.
- Basic Project tracking list (Project level only, no tasks).
- Mathematical integration: Live dynamic hour calculations and rolling budget calculations.

### Phase 2: Task-Level Detail & Analytics

**Goal**: Expand tracking precision to individual tasks and build the exporter engine.

**Deliverables**:
- Add Tasks schema to the app logic.
- Kanban-lite task lists under Projects.
- History Tab: Editable calendar interface to override incorrect logs manually.
- Exporter tool generating CSV reports structured for HR and supervisor reviews.

### Phase 3: Git Integration Hub (The Smart Link)

**Goal**: Connect your offline application directly to your active code bases.

**Deliverables**:
- Apply DB migration to append `GitProviders` and `GitTaskMappings` tables.
- Authenticate integration via OAuth or Personal Access Tokens (stored securely/encrypted).
- Integrate Issue-to-Task sync: Select a repository, automatically pull open issues into your task list, and track time against active issues.

### Phase 4: Enterprise Sync & Cloud Collaboration

**Goal**: Turn this personal offline tool into an optional collaborative platform.

**Deliverables**:
- Introduce optional Cloud Storage/Sync.
- Workspace/Team features (share total project times across team projects, while preserving private attendance logging constraints).

## 6. Detailed Git Integration Technical Specification (Future Phase 3)

When you are ready to implement Git connections, here is the technical schema expansion and synchronization strategy.

### 6.1 Database Migration DDL

This migration should run to seamlessly append external provider context to your existing task list.

```sql
CREATE TABLE GitProviders (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId INTEGER NOT NULL,
    ProviderName TEXT NOT NULL, -- 'GitHub', 'Gitea', 'GitLab'
    BaseUrl TEXT NULL, -- Required for self-hosted instances (e.g. enterprise Gitea/GitLab)
    AccessTokenEncrypted TEXT NOT NULL,
    CreatedAt TEXT DEFAULT (CURRENT_TIMESTAMP),
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);

CREATE TABLE GitTaskMappings (
    TaskId INTEGER PRIMARY KEY,
    GitProviderId INTEGER NOT NULL,
    ExternalIssueId TEXT NOT NULL, -- Provider's unique internal ID
    ExternalIssueNumber INTEGER NOT NULL, -- Visible reference (e.g., Issue #104)
    ExternalRepoFullName TEXT NOT NULL, -- e.g., "org/project-repo"
    LastSyncedAt TEXT DEFAULT (CURRENT_TIMESTAMP),
    FOREIGN KEY (TaskId) REFERENCES Tasks(Id) ON DELETE CASCADE,
    FOREIGN KEY (GitProviderId) REFERENCES GitProviders(Id) ON DELETE CASCADE
);
```

### 6.2 The Integration Sync Logic Flow

To avoid slowing down your daily tracker, the synchronization should be pull-on-demand and non-blocking:

```
              +--------------------------+
              | User triggers Git Import |
              +--------------------------+
                           |
                           v
          +----------------------------------+
          | API request fetch assigned issues|
          +----------------------------------+
                           |
         (Map through JSON response payload)
                           |
             +-------------v-------------+
             | Is Issue already tracked? |
             +---------------------------+
               /                       \
        (Yes) /                         \ (No)
             v                           v
+------------------------+   +------------------------------------+
| Match mapping TaskId   |   | 1. Insert row to Tasks             |
| Skip to avoid conflict |   | 2. Insert row to GitTaskMappings  |
+------------------------+   +------------------------------------+
```

- **Pull Engine**: User opens a project, clicks "Import from Gitea/GitHub".
- **API Request**: Query the provider's API for issues assigned to the authenticated user on that repository.
- **Upsert Logic**:
  - Iterate through the list. If `GitTaskMappings.ExternalIssueId` already exists, skip it (preserves local tracking updates).
  - If it doesn't exist, create a matching record in `Tasks` (mapping the Issue Title to `Tasks.Title`) and write the metadata directly into `GitTaskMappings`.
- **Local Tracking Continuity**: When time is recorded against this task, it writes directly to `ProjectTimeLogs` exactly like any standard local task. This protects your core time logging data from API disconnections.
