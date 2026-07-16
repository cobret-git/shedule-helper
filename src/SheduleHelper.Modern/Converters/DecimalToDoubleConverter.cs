using Microsoft.UI.Xaml.Data;
using System;

namespace SheduleHelper.Modern.Converters
{
    /// <summary>
    /// Converts between <see cref="decimal"/> view-model values and the <see cref="double"/> used
    /// by controls like <see cref="Microsoft.UI.Xaml.Controls.NumberBox"/>.
    /// </summary>
    public sealed class DecimalToDoubleConverter : IValueConverter
    {
        #region Methods

        /// <inheritdoc/>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value is decimal decimalValue ? (double)decimalValue : 0d;
        }

        /// <inheritdoc/>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value is double doubleValue ? (decimal)doubleValue : 0m;
        }

        #endregion
    }
}
