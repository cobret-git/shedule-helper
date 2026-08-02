using Microsoft.EntityFrameworkCore;
using SheduleHelper.Core.Components.Entities;
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
                return await db.StartProjectTimeLogAsync(attendanceLogId, projectId, taskId, startTime, cancellationToken);
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
                await db.EndProjectTimeLogAsync(attendanceLogId, endTime, cancellationToken);
            }
            catch (InvalidOperationException)
            {
                throw new TrackingOperationException(MSG.error_trackingNotActive);
            }
        }

        #endregion
    }
}
