namespace Stint.Core
{
    /// <summary>
    /// What a part-day absence (an <see cref="AwayLog"/>) is for. Values are stored as their
    /// underlying int - don't reorder or remove them; only append.
    /// </summary>
    public enum AwayKind
    {
        /// <summary>A doctor's or dentist's appointment.</summary>
        DoctorDental = 0,

        /// <summary>Family, childcare or other personal business.</summary>
        FamilyPersonal = 1,

        /// <summary>Anything else.</summary>
        Other = 2
    }
}
