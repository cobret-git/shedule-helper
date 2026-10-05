namespace Stint.Cli.Components
{
    /// <summary>
    /// One of the three selector rows on the date picker screen, in display order.
    /// </summary>
    public enum DatePickerRow
    {
        /// <summary>Year selector.</summary>
        Year,

        /// <summary>Month selector - wraps within the year, like the day row wraps within the month.</summary>
        Month,

        /// <summary>Day-of-month selector, shown with its weekday.</summary>
        Day
    }
}
