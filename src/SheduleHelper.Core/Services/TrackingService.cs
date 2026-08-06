using Microsoft.EntityFrameworkCore;
using SheduleHelper.Core.Components.Entities;
using SheduleHelper.Core.Models;
using MSG = SheduleHelper.Core.Resources.Strings.Messages;

namespace SheduleHelper.Core.Services
{
    /// <summary>
    /// Default <see cref="ITrackingService"/> implementation, backed directly by <see cref="Models.LocalDbContext"/>.
    /// </summary>
    public class TrackingService : ITrackingService
    {
        #region Fields

        private readonly ILocalDbContextFactory _dbContextFactory;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="TrackingService"/> class.
        /// </summary>
        /// <param name="dbContextFactory">Creates the <see cref="Models.LocalDbContext"/> used for every operation.</param>
        public TrackingService(ILocalDbContextFactory dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        #endregion

        #region Methods

        /// <inheritdoc/>
        public async Task<ProjectTimeLog?> GetActiveTrackingAsync(int attendanceLogId, CancellationToken cancellationToken)
        {
            await using var db = _dbContextFactory.CreateDbContext();
            return await db.GetActiveProjectTimeLogAsync(attendanceLogId, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<ProjectTimeLog> SwitchAsync(int attendanceLogId, int projectId, int? taskId, DateTime startTime, CancellationToken cancellationToken)
        {
            if (startTime > DateTime.Now)
            {
                throw new TrackingOperationException(MSG.error_trackingStartTimeInFuture);
            }

            await using var db = _dbContextFactory.CreateDbContext();

            try
            {
                var segment = await db.StartProjectTimeLogAsync(attendanceLogId, projectId, taskId, startTime, cancellationToken);
                await MarkTaskInProgressAsync(db, taskId, cancellationToken);
                return segment;
            }
            catch (DbUpdateException)
            {
                throw new TrackingOperationException(MSG.error_trackingUnexpected);
            }
        }

        /// <inheritdoc/>
        public async Task StopTrackingAsync(int attendanceLogId, DateTime endTime, CancellationToken cancellationToken)
        {
            await using var db = _dbContextFactory.CreateDbContext();

            try
            {
                await db.EndProjectTimeLogAsync(attendanceLogId, endTime, TimeLogCloseReason.Stopped, cancellationToken);
            }
            catch (InvalidOperationException)
            {
                throw new TrackingOperationException(MSG.error_trackingNotActive);
            }
        }

        /// <inheritdoc/>
        public async Task<TrackingResumeResult> ResumeLastAsync(int userId, int attendanceLogId, DateTime startTime, CancellationToken cancellationToken)
        {
            await using var db = _dbContextFactory.CreateDbContext();

            if (await db.GetActiveProjectTimeLogAsync(attendanceLogId, cancellationToken) is not null)
            {
                return new TrackingResumeResult(TrackingResumeOutcome.AlreadyTracking, null, null, null);
            }

            var last = await db.GetLastProjectTimeLogAsync(userId, cancellationToken);
            if (last is null)
            {
                return new TrackingResumeResult(TrackingResumeOutcome.NothingToResume, null, null, null);
            }

            // A null ClosedReason on a segment from an earlier session means it was left open by a
            // clock-out that predates ClockOutAsync closing them - the same "ran out of day" case as
            // ClockedOut, so it stays resumable rather than being treated as unknown.
            if (last.ClosedReason == TimeLogCloseReason.Stopped)
            {
                return new TrackingResumeResult(TrackingResumeOutcome.StoppedDeliberately, null, last.Project.Name, last.Task?.Title);
            }

            // Project is always loaded: deleting one cascades to its segments, so a segment that
            // still exists still has its project. Archiving is the case that has to be handled.
            if (!last.Project.IsActive)
            {
                return new TrackingResumeResult(TrackingResumeOutcome.ProjectUnavailable, null, last.Project.Name, last.Task?.Title);
            }

            // Task may be null either because none was tracked or because it was deleted (the
            // cascade nulls TaskId); a Done task is dropped for the same reason - neither should
            // quietly accumulate more time.
            var carryTask = last.Task is { } task && task.Status != TaskItemStatus.Done;
            var segment = await SwitchAsync(attendanceLogId, last.ProjectId, carryTask ? last.TaskId : null, startTime, cancellationToken);

            var outcome = last.Task is not null && !carryTask
                ? TrackingResumeOutcome.ResumedWithoutTask
                : TrackingResumeOutcome.Resumed;

            return new TrackingResumeResult(outcome, segment, last.Project.Name, carryTask ? last.Task!.Title : null);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Moves a task out of <see cref="TaskItemStatus.Todo"/> the first time it's tracked, so the
        /// Switch screen's "not yet done" filter stops showing it once you've actually started it -
        /// otherwise nothing ever recorded that a task had been picked up, and it kept reappearing
        /// in the switch menu on every visit for as long as it stayed untouched at Todo. Leaves
        /// <see cref="TaskItemStatus.InProgress"/> and <see cref="TaskItemStatus.Done"/> alone -
        /// switching back to a task you already marked Done should not silently un-finish it.
        /// </summary>
        private static async Task MarkTaskInProgressAsync(LocalDbContext db, int? taskId, CancellationToken cancellationToken)
        {
            if (taskId is null)
            {
                return;
            }

            var task = await db.Tasks.FindAsync(new object?[] { taskId.Value }, cancellationToken);
            if (task is not null && task.Status == TaskItemStatus.Todo)
            {
                task.Status = TaskItemStatus.InProgress;
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        #endregion
    }
}
