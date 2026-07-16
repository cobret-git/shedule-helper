using Microsoft.UI.Xaml.Data;
using System;

namespace SheduleHelper.Modern.Converters
{
    /// <summary>
    /// Converts between <see cref="TimeOnly"/> view-model values and the <see cref="TimeSpan"/>
    /// used by <see cref="Microsoft.UI.Xaml.Controls.TimePicker.Time"/>.
    /// </summary>
    public sealed class TimeOnlyToTimeSpanConverter : IValueConverter
    {
        #region Methods

        /// <inheritdoc/>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value is TimeOnly timeOnly ? timeOnly.ToTimeSpan() : TimeSpan.Zero;
        }

        /// <inheritdoc/>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value is TimeSpan timeSpan ? TimeOnly.FromTimeSpan(timeSpan) : TimeOnly.MinValue;
        }

        #endregion
    }
}
