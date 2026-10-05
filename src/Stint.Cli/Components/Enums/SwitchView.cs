namespace Stint.Cli.Components
{
    /// <summary>
    /// What the Switch screen is listing to switch to - toggled with Tab. Separate from
    /// <see cref="SwitchMode"/>, which is about pending marks, not about what is listed.
    /// </summary>
    public enum SwitchView
    {
        /// <summary>The project/task tree.</summary>
        Projects,

        /// <summary>The part-day absences (doctor, errand, ...) one can go away for.</summary>
        Away
    }
}
