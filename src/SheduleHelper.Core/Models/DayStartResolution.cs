using SheduleHelper.Core.Components.Entities;

namespace SheduleHelper.Core.Models
{
    /// <summary>
    /// What <see cref="Services.IAttendanceService.ResolveDayStartAsync"/> actually did, so a host
    /// can tell the user rather than leaving them to notice. Automation that edits a timesheet
    /// without saying so is the failure mode worth designing against here: every field below exists
    /// to be reported back, and each action is individually correctable through the same clock-in,
    /// clock-out and switch commands the user would have used by hand.
    /// </summary>
    /// <param name="Snapshot">The day state after any actions were applied - callers use this instead of re-reading it.</param>
    /// <param name="Automation">The setting the resolution ran under, so a host can distinguish "nothing to do" from "not enabled".</param>
    /// <param name="ClosedForgottenDay">The clock-out timestamp written to a previous day that had been left open, or <see langword="null"/> if no day needed closing.</param>
    /// <param name="ClockedIn">The clock-in timestamp written for today, or <see langword="null"/> if today was not clocked in automatically.</param>
    /// <param name="Resume">The outcome of continuing the previously tracked project, or <see langword="null"/> if no clock-in happened or resuming is switched off.</param>
    /// <param name="SkippedBecauseWeekend"><see langword="true"/> when today's clock-in was withheld only because today is a Saturday or Sunday.</param>
    public sealed record DayStartResolution(
        AttendanceDaySnapshot Snapshot,
        DayStartAutomation Automation,
        DateTime? ClosedForgottenDay,
        DateTime? ClockedIn,
        TrackingResumeResult? Resume,
        bool SkippedBecauseWeekend)
    {
        #region Properties

        /// <summary>
        /// Whether anything was actually changed - <see langword="false"/> means the app opened into
        /// exactly the state it was left in, and a host has nothing to report.
        /// </summary>
        public bool DidSomething =>
            ClosedForgottenDay is not null
            || ClockedIn is not null
            || Resume?.Outcome is TrackingResumeOutcome.Resumed or TrackingResumeOutcome.ResumedWithoutTask;

        #endregion
    }
}
