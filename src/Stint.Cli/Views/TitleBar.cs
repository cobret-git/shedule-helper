namespace Stint.Cli.Views
{
    /// <summary>
    /// Turns a screen's <c>Title</c> into the centered, fill-padded banner every screen shows
    /// on its first line (e.g. <c>***** HOME *****</c>).
    /// </summary>
    public static class TitleBar
    {
        #region Methods

        public static string Render(string title, int width = ScreenBuffer.Width, char fill = '*')
        {
            var label = $" {title.ToUpperInvariant()} ";
            var fillCount = Math.Max(0, width - label.Length);
            var leftFill = fillCount / 2;
            var rightFill = fillCount - leftFill;
            return new string(fill, leftFill) + label + new string(fill, rightFill);
        }

        #endregion
    }
}
