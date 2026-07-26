using Microsoft.UI.Xaml.Data;
using System;

namespace SheduleHelper.Modern.Converters
{
    /// <summary>
    /// Converts a BCP-47 culture tag into its native display name for the Settings page's
    /// language picker (e.g. so a user who ended up on the wrong language can still recognize
    /// their own in the list).
    /// </summary>
    public sealed class CultureDisplayConverter : IValueConverter
    {
        #region Methods

        /// <inheritdoc/>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value is string culture
                ? culture switch
                {
                    "en" => "English",
                    "uk" => "Українська",
                    _ => culture
                }
                : string.Empty;
        }

        /// <inheritdoc/>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotSupportedException($"{nameof(CultureDisplayConverter)} only supports one-way conversion.");
        }

        #endregion
    }
}
