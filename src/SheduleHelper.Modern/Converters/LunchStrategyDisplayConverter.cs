using Microsoft.UI.Xaml.Data;
using SheduleHelper.Core.Components.Entities;
using System;

namespace SheduleHelper.Modern.Converters
{
    /// <summary>
    /// Converts a <see cref="LunchStrategy"/> value into its display text for the Settings page's
    /// lunch strategy picker.
    /// </summary>
    public sealed class LunchStrategyDisplayConverter : IValueConverter
    {
        #region Methods

        /// <inheritdoc/>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value is LunchStrategy strategy
                ? strategy switch
                {
                    LunchStrategy.None => "No automatic deduction",
                    LunchStrategy.FixedWindow => "Fixed window",
                    LunchStrategy.DurationBased => "Duration-based",
                    _ => strategy.ToString()
                }
                : string.Empty;
        }

        /// <inheritdoc/>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotSupportedException($"{nameof(LunchStrategyDisplayConverter)} only supports one-way conversion.");
        }

        #endregion
    }
}
