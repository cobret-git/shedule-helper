using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using SheduleHelper.Core.Components.Entities;
using System;

namespace SheduleHelper.Modern.Converters
{
    /// <summary>
    /// Compares an <see cref="AttendanceDayState"/> value against the state named in
    /// <c>ConverterParameter</c>, for switching which of Home's clock-in/clock-out panels is shown.
    /// </summary>
    public sealed class AttendanceDayStateToVisibilityConverter : IValueConverter
    {
        #region Methods

        /// <inheritdoc/>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var isMatch = value is AttendanceDayState state
                && parameter is string parameterText
                && Enum.TryParse<AttendanceDayState>(parameterText, out var comparand)
                && state == comparand;

            return isMatch ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <inheritdoc/>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotSupportedException($"{nameof(AttendanceDayStateToVisibilityConverter)} only supports one-way conversion.");
        }

        #endregion
    }
}
