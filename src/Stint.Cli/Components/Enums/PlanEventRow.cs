namespace Stint.Cli.Components
{
    /// <summary>
    /// One of the rows of the Plan event form, in display order.
    /// </summary>
    public enum PlanEventRow
    {
        /// <summary>First day of the event.</summary>
        From,

        /// <summary>Last day of the event (inclusive).</summary>
        To,

        /// <summary>Optional free-text note.</summary>
        Note
    }
}
