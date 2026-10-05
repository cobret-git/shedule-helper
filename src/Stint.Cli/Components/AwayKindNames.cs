using Stint.Core;

namespace Stint.Cli.Components
{
    /// <summary>
    /// How each <see cref="AwayKind"/> is worded on screen.
    /// </summary>
    public static class AwayKindNames
    {
        #region Methods

        /// <summary>The user-facing name of <paramref name="kind"/>, e.g. "Doctor / dental".</summary>
        public static string GetName(AwayKind kind) => kind switch
        {
            AwayKind.DoctorDental => "Doctor / dental",
            AwayKind.FamilyPersonal => "Family / personal",
            AwayKind.Other => "Other",
            _ => kind.ToString()
        };

        #endregion
    }
}
