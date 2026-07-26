using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Markup;
using SheduleHelper.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SheduleHelper.Modern.Components.Helpers
{
    [MarkupExtensionReturnType(ReturnType = typeof(string))]
    public class LocalizedStringExtension : MarkupExtension
    {
        public string Key { get; set; } = string.Empty;

        protected override object ProvideValue()
        {
            var binding = new Binding
            {
                Source = LocalizationManager.Instance,
                Path = new PropertyPath($"[{Key}]"), // Indexer binding
                Mode = BindingMode.OneWay
            };
            return binding;
        }
    }
}
