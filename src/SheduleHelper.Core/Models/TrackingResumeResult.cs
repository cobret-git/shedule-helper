using SheduleHelper.Core.Components.Entities;

namespace SheduleHelper.Core.Models
{
    /// <summary>
    /// Why <see cref="Services.ITrackingService.ResumeLastAsync"/> did or did not pick the previously
    /// tracked project back up. Every non-<see cref="Resumed"/> value is a reason worth telling the
    /// user about rather than failing silently - a morning where tracking quietly didn't resume
    /// looks identical to one where it did, until the day's timeline turns out to be empty.
    /// </summary>
    public enum TrackingResumeOutcome
    {
        /// <summary>
        /// Something is already being tracked in this session, so there was nothing to resume onto.
        /// </summary>
        AlreadyTracking = 0,

        /// <summary>
        /// The user has never tracked a project, so there is no history to continue from.
        /// </summary>
        NothingToResume = 1,

        /// <summary>
        /// The last segment was closed with <see cref="TimeLogCloseReason.Stopped"/> - the user
        /// deliberately stopped tracking, which is honoured rather than undone.
        /// </summary>
        StoppedDeliberately = 2,

        /// <summary>
        /// The project to resume has since been archived, so it is not offered again automatically.
        /// </summary>
        ProjectUnavailable = 3,

        /// <summary>
        /// Tracking resumed on the same project and task as before.
        /// </summary>
        Resumed = 4,

        /// <summary>
        /// Tracking resumed on the project, but without the previous task - it has since been
        /// completed or deleted, and quietly logging more time against a finished task would be
        /// worse than asking the user to pick the next one.
        /// </summary>
        ResumedWithoutTask = 5
    }

    /// <summary>
    /// The outcome of a tracking-resume attempt, along with enough labelling for a host to say what
    /// happened. <see cref="ProjectName"/>/<see cref="TaskTitle"/> are carried explicitly rather
    /// than read off <see cref="Segment"/> because a freshly created segment has no navigation
    /// properties loaded, and because they are also needed for the outcomes where no segment was
    /// created at all.
    /// </summary>
    /// <param name="Outcome">What happened - see <see cref="TrackingResumeOutcome"/>.</param>
    /// <param name="Segment">The newly opened segment, when <paramref name="Outcome"/> is <see cref="TrackingResumeOutcome.Resumed"/> or <see cref="TrackingResumeOutcome.ResumedWithoutTask"/>; otherwise <see langword="null"/>.</param>
    /// <param name="ProjectName">The project involved, where one was identified - including the outcomes that declined to resume it.</param>
    /// <param name="TaskTitle">The task involved, where one was identified.</param>
    public sealed record TrackingResumeResult(
        TrackingResumeOutcome Outcome,
        ProjectTimeLog? Segment,
        string? ProjectName,
        string? TaskTitle);
}
