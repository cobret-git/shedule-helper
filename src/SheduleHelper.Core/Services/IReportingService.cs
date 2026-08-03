using SheduleHelper.Core.Models;

namespace SheduleHelper.Core.Services
{
    /// <summary>
    /// Builds a <see cref="PeriodReport"/> - worked/target/balance and a project/task breakdown -
    /// for the week/month/quarter/year containing a given reference date.
    /// </summary>
    public interface IReportingService
    {
        #region Methods

        /// <summary>
        /// Builds the report for the <paramref name="zoom"/>-sized period containing <paramref name="referenceDate"/>.
        /// </summary>
        /// <param name="userId">The user whose attendance/project logs to summarize.</param>
        /// <param name="zoom">The granularity to bucket the period into.</param>
        /// <param name="referenceDate">Any date within the desired period - the service resolves the period's actual boundaries from it.</param>
        Task<PeriodReport> GetReportAsync(int userId, ReportZoom zoom, DateTime referenceDate, CancellationToken cancellationToken);

        #endregion
    }
}
