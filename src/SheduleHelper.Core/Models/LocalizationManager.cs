using Microsoft.EntityFrameworkCore.Diagnostics.Internal;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Text;

namespace SheduleHelper.Core.Models
{
    public class LocalizationManager : INotifyPropertyChanged
    {
        public static LocalizationManager Instance { get; } = new LocalizationManager();

        private readonly ResourceManager _resourceManager;

        // Deliberately not read from CultureInfo.CurrentUICulture at lookup time - on WinUI/WinAppSDK,
        // something (most likely ApplicationLanguages/resource-context negotiation against the app's
        // declared supported languages) resyncs the UI thread's ambient CurrentUICulture back to the
        // OS-negotiated language on later XAML parses (e.g. a fresh page from Frame.Navigate), even
        // though SetCulture just set it. Tracking our own field here means resx lookups stay correct
        // regardless of what the ambient thread culture drifts back to.
        private CultureInfo _currentCulture = CultureInfo.CurrentUICulture;

        private LocalizationManager()
        {
            _resourceManager = SheduleHelper.Core.Resources.Strings.Content.ResourceManager;
        }

        public string this[string key] => GetString(key);

        public string GetString(string key)
        {
            return _resourceManager.GetString(key, _currentCulture) ?? $"[{key}]";
        }

        public void SetCulture(CultureInfo culture)
        {
            _currentCulture = culture;

            // Also update the ambient thread culture, for anything else that reads it directly
            // (e.g. WinUI date/time formatting) - just not relied upon for the resx lookup above.
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;

            // Unlike WPF, WinUI/UWP's binding engine only treats an empty/null PropertyName as
            // "refresh all NON-indexer properties" - our LocalizedStringExtension bindings go
            // through this[key], an indexer, which needs the "Item[]" sentinel instead to refresh.
            OnPropertyChanged("Item[]");
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
