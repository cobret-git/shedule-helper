using Microsoft.UI.Xaml.Data;
using SheduleHelper.Core.Components.Entities;
using System;

namespace SheduleHelper.Modern.Converters
{
    /// <summary>
    /// Compares a <see cref="LunchStrategy"/> value against the strategy named in
    /// <c>ConverterParameter</c>, for enabling/disabling the settings rows that only apply to one
    /// specific strategy (e.g. the fixed-window start/end times, or the duration threshold).
    /// </summary>
    public sealed class LunchStrategyToBooleanConverter : IValueConverter
    {
        #region Methods

        /// <inheritdoc/>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value is LunchStrategy strategy
                && parameter is string parameterText
                && Enum.TryParse<LunchStrategy>(parameterText, out var comparand)
                && strategy == comparand;
        }

        /// <inheritdoc/>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotSupportedException($"{nameof(LunchStrategyToBooleanConverter)} only supports one-way conversion.");
        }

        #endregion
    }
}
