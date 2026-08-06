using Serilog;
using SheduleHelper.Cli.Infrastructure;
using SheduleHelper.Cli.Widgets;
using SheduleHelper.Core.Components.Entities;
using SheduleHelper.Core.Services;

namespace SheduleHelper.Cli.Screens
{
    /// <summary>
    /// Pushed from <see cref="HomeScreen"/> when the user presses <c>S</c> while clocked in. Lists
    /// active projects and their not-yet-done tasks as a flat, indented list - no filter/fold yet,
    /// those land with the Projects browser. <c>Enter</c> switches tracking to the highlighted row;
    /// <c>X</c> stops tracking outright.
    /// </summary>
    public sealed class SwitchScreen : IScreen
    {
        #region Fields

        private readonly ITrackingService _trackingService;
        private readonly ILocalDbContextFactory _dbContextFactory;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly int _attendanceLogId;
        private readonly DateTime _clockInTime;
        private readonly ILogger _logger = Log.ForContext<SwitchScreen>();

        private List<Row> _rows = new();
        private SelectList _list = new(0);
        private string? _message;
        private bool _hasTrackedToday;

        // Set while asking "now, or at clock-in time?" for the first pick of the day - see
        // SwitchToSelectedAsync.
        private Row? _pendingRow;
        private SelectList _startTimeOptions = new(2);

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="SwitchScreen"/> class.
        /// </summary>
        /// <param name="attendanceLogId">The open attendance session tracking should be recorded against.</param>
        /// <param name="clockInTime">Today's clock-in time - offered as an alternative start time when nothing has been tracked yet today.</param>
        public SwitchScreen(ITrackingService trackingService, ILocalDbContextFactory dbContextFactory, ICurrentUserContext currentUserContext, int attendanceLogId, DateTime clockInTime)
        {
            _trackingService = trackingService;
            _dbContextFactory = dbContextFactory;
            _currentUserContext = currentUserContext;
            _attendanceLogId = attendanceLogId;
            _clockInTime = clockInTime;
        }

        #endregion

        #region Methods

        /// <inheritdoc/>
        public Task OnEnter() => LoadAsync();

        /// <inheritdoc/>
        public void Render(Frame frame)
        {
            Header.Draw(frame, "SWITCH", "Esc Cancel");

            if (_pendingRow is { } pendingRow)
            {
                RenderStartTimeChoice(frame, pendingRow);
                return;
            }

            if (_rows.Count == 0)
            {
                frame.Write(1, 3, "No active projects yet - add one from the Projects browser once it exists.", ColorToken.Dim);
            }

            var labelAvailable = Math.Max(1, frame.Width - 1 - 2 - 1);

            for (var i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                var selected = _list.SelectedIndex == i;
                var marker = selected ? "►" : row.IsCurrent ? "●" : " ";
                var color = selected ? ColorToken.Accent : row.IsCurrent ? ColorToken.Positive : ColorToken.Default;
                frame.Write(1, 3 + i, $"{marker} {Formatting.Truncate(row.Label, labelAvailable)}", color);
            }

            if (!string.IsNullOrWhiteSpace(_message))
            {
                frame.Write(1, frame.Height - 4, _message, ColorToken.Negative);
            }

            KeyBar.Draw(frame, ("up/down", "Move"), ("Enter", "Track"), ("X", "Stop tracking"), ("Esc", "Cancel"));
        }

        /// <inheritdoc/>
        public async Task HandleKey(ConsoleKeyInfo key, ScreenStack screens)
        {
            if (_pendingRow is { } pendingRow)
            {
                await HandleStartTimeChoiceKey(key, screens, pendingRow);
                return;
            }

            if (_list.HandleKey(key))
            {
                return;
            }

            switch (key.Key)
            {
                case ConsoleKey.Escape:
                    await screens.Pop();
                    break;
                case ConsoleKey.Enter:
                    await SwitchToSelectedAsync(screens);
                    break;
                case ConsoleKey.X:
                    await StopAsync(screens);
                    break;
            }
        }

        #endregion

        #region Helpers

        private async Task LoadAsync()
        {
            try
            {
                await using var db = _dbContextFactory.CreateDbContext();
                var userId = _currentUserContext.UserId;

                var activeTracking = await _trackingService.GetActiveTrackingAsync(_attendanceLogId, CancellationToken.None);
                _hasTrackedToday = await db.HasAnyProjectTimeLogAsync(_attendanceLogId, CancellationToken.None);

                var projects = (await db.GetProjectsByUserIdAsync(userId, CancellationToken.None))
                    .Where(p => p.IsActive)
                    .OrderBy(p => p.Name)
                    .ToList();

                var rows = new List<Row>();
                foreach (var project in projects)
                {
                    rows.Add(new Row(project.Id, null, project.Name, IsCurrentRow(activeTracking, project.Id, null)));

                    var tasks = (await db.GetTasksByProjectIdAsync(project.Id, CancellationToken.None))
                        .Where(t => t.Status != TaskItemStatus.Done)
                        .OrderBy(t => t.Title);

                    foreach (var task in tasks)
                    {
                        rows.Add(new Row(project.Id, task.Id, $"  {task.Title}", IsCurrentRow(activeTracking, project.Id, task.Id)));
                    }
                }

                _rows = rows;
                _list = new SelectList(Math.Max(rows.Count, 1));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load projects for the switch screen.");
                _message = "Failed to load projects.";
            }
        }

        private static bool IsCurrentRow(ProjectTimeLog? activeTracking, int projectId, int? taskId)
        {
            return activeTracking is not null && activeTracking.ProjectId == projectId && activeTracking.TaskId == taskId;
        }

        private async Task SwitchToSelectedAsync(ScreenStack screens)
        {
            if (_rows.Count == 0 || _list.SelectedIndex >= _rows.Count)
            {
                return;
            }

            var row = _rows[_list.SelectedIndex];

            // Picking the very first task of the day defaults to "now" every time otherwise, even
            // when the user clocked in a while ago and only just got around to choosing what to
            // work on - backdating the very first segment to the clock-in time is the whole point
            // of offering the choice, so it's skipped once anything has already been tracked today.
            if (!_hasTrackedToday && DateTime.Now - _clockInTime > TimeSpan.FromMinutes(1))
            {
                _startTimeOptions = new SelectList(2);
                _pendingRow = row;
                return;
            }

            await SwitchAsync(screens, row, DateTime.Now);
        }

        private void RenderStartTimeChoice(Frame frame, Row row)
        {
            frame.Write(1, 3, $"Start \"{row.Label.Trim()}\" at:", ColorToken.Dim);

            DrawStartTimeRow(frame, 5, 0, "Now", Formatting.Time(DateTime.Now));
            DrawStartTimeRow(frame, 6, 1, "At clock-in", Formatting.Time(_clockInTime));

            if (!string.IsNullOrWhiteSpace(_message))
            {
                frame.Write(1, frame.Height - 4, _message, ColorToken.Negative);
            }

            KeyBar.Draw(frame, ("up/down", "Select"), ("Enter", "Confirm"), ("Esc", "Cancel"));
        }

        private void DrawStartTimeRow(Frame frame, int y, int index, string label, string value)
        {
            var selected = _startTimeOptions.SelectedIndex == index;
            var marker = selected ? "►" : " ";
            frame.Write(1, y, $"{marker} {label}", selected ? ColorToken.Accent : ColorToken.Default);
            frame.Write(20, y, value);
        }

        private async Task HandleStartTimeChoiceKey(ConsoleKeyInfo key, ScreenStack screens, Row row)
        {
            if (_startTimeOptions.HandleKey(key))
            {
                return;
            }

            switch (key.Key)
            {
                case ConsoleKey.Escape:
                    _pendingRow = null;
                    break;
                case ConsoleKey.Enter:
                    var startTime = _startTimeOptions.SelectedIndex == 0 ? DateTime.Now : _clockInTime;
                    await SwitchAsync(screens, row, startTime);
                    break;
            }
        }

        private async Task SwitchAsync(ScreenStack screens, Row row, DateTime startTime)
        {
            try
            {
                await _trackingService.SwitchAsync(_attendanceLogId, row.ProjectId, row.TaskId, startTime, CancellationToken.None);
                await screens.Pop();
            }
            catch (TrackingOperationException ex)
            {
                _message = ex.Message;
                _pendingRow = null;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to switch tracking to project {ProjectId}.", row.ProjectId);
                _message = "Something went wrong switching projects.";
                _pendingRow = null;
            }
        }

        private async Task StopAsync(ScreenStack screens)
        {
            try
            {
                await _trackingService.StopTrackingAsync(_attendanceLogId, DateTime.Now, CancellationToken.None);
                await screens.Pop();
            }
            catch (TrackingOperationException ex)
            {
                _message = ex.Message;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to stop tracking for attendance log {AttendanceLogId}.", _attendanceLogId);
                _message = "Something went wrong stopping tracking.";
            }
        }

        #endregion

        #region Structures

        private readonly record struct Row(int ProjectId, int? TaskId, string Label, bool IsCurrent);

        #endregion
    }
}
