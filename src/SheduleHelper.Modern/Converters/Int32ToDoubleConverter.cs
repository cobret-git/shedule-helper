using Microsoft.UI.Xaml.Data;
using System;

namespace SheduleHelper.Modern.Converters
{
    /// <summary>
    /// Converts between <see cref="int"/> view-model values and the <see cref="double"/> used by
    /// controls like <see cref="Microsoft.UI.Xaml.Controls.NumberBox"/>.
    /// </summary>
    public sealed class Int32ToDoubleConverter : IValueConverter
    {
        #region Methods

        /// <inheritdoc/>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value is int intValue ? (double)intValue : 0d;
        }

        /// <inheritdoc/>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value is double doubleValue ? (int)doubleValue : 0;
        }

        #endregion
    }
}
