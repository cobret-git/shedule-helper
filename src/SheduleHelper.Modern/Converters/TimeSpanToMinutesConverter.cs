using Microsoft.UI.Xaml.Data;
using System;

namespace SheduleHelper.Modern.Converters
{
    /// <summary>
    /// Converts a <see cref="TimeSpan"/> into its total minutes, for controls like
    /// <see cref="Microsoft.UI.Xaml.Controls.ProgressBar"/> whose <c>Value</c>/<c>Maximum</c> are
    /// plain <see cref="double"/>.
    /// </summary>
    public sealed class TimeSpanToMinutesConverter : IValueConverter
    {
        #region Methods

        /// <inheritdoc/>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value is TimeSpan timeSpan ? timeSpan.TotalMinutes : 0d;
        }

        /// <inheritdoc/>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotSupportedException($"{nameof(TimeSpanToMinutesConverter)} only supports one-way conversion.");
        }

        #endregion
    }
}
