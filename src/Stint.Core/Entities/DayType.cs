namespace Stint.Core
{
    /// <summary>
    /// What kind of day an AttendanceLog represents. Determines whether ClockIn/ClockOut
    /// and ProjectTimeLogs are meaningful, and how the day counts toward balance.
    /// </summary>
    public enum DayType
    {
        /// <summary>Normal worked day - ClockIn/ClockOut and ProjectTimeLogs apply.</summary>
        Worked = 0,

        /// <summary>Paid vacation day.</summary>
        Vacation = 1,

        /// <summary>Sick leave.</summary>
        Sick = 2,

        /// <summary>Public/company holiday.</summary>
        Holiday = 3,

        /// <summary>Unpaid leave.</summary>
        Unpaid = 4
    }
}
