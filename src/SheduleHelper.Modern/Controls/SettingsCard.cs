using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SheduleHelper.Modern.Controls
{
    /// <summary>
    /// A labeled settings row: an optional icon, a header, an optional description, and an
    /// arbitrary input control (via <see cref="ContentControl.Content"/>) aligned to the right.
    /// Mirrors the look of the built-in Windows 11 Settings app without pulling in the
    /// CommunityToolkit SettingsCard control. Default style/template lives in Themes/Generic.xaml.
    /// </summary>
    public sealed class SettingsCard : ContentControl
    {
        #region Fields

        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register(nameof(Header), typeof(string), typeof(SettingsCard), new PropertyMetadata(null));

        public static readonly DependencyProperty DescriptionProperty =
            DependencyProperty.Register(nameof(Description), typeof(string), typeof(SettingsCard), new PropertyMetadata(null, OnDescriptionChanged));

        public static readonly DependencyProperty HeaderIconProperty =
            DependencyProperty.Register(nameof(HeaderIcon), typeof(IconElement), typeof(SettingsCard), new PropertyMetadata(null, OnHeaderIconChanged));

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsCard"/> class.
        /// </summary>
        public SettingsCard()
        {
            DefaultStyleKey = typeof(SettingsCard);
            IsEnabledChanged += (_, _) => VisualStateManager.GoToState(this, IsEnabled ? "Normal" : "Disabled", true);
        }

        #endregion

        #region Properties

        /// <summary>
        /// The row's title text.
        /// </summary>
        public string? Header
        {
            get => (string?)GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }

        /// <summary>
        /// Optional secondary text shown below the header. Hidden entirely when null/empty.
        /// </summary>
        public string? Description
        {
            get => (string?)GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        /// <summary>
        /// Optional icon shown to the left of the header/description. Hidden entirely when null.
        /// </summary>
        public IconElement? HeaderIcon
        {
            get => (IconElement?)GetValue(HeaderIconProperty);
            set => SetValue(HeaderIconProperty, value);
        }

        #endregion

        #region Handlers

        /// <inheritdoc/>
        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            UpdateDescriptionVisibility();
            UpdateHeaderIconVisibility();
            VisualStateManager.GoToState(this, IsEnabled ? "Normal" : "Disabled", false);
        }

        #endregion

        #region Helpers

        private static void OnDescriptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
            ((SettingsCard)d).UpdateDescriptionVisibility();

        private static void OnHeaderIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
            ((SettingsCard)d).UpdateHeaderIconVisibility();

        private void UpdateDescriptionVisibility()
        {
            if (GetTemplateChild("DescriptionTextBlock") is TextBlock descriptionTextBlock)
            {
                descriptionTextBlock.Visibility = string.IsNullOrEmpty(Description) ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private void UpdateHeaderIconVisibility()
        {
            if (GetTemplateChild("HeaderIconPresenter") is ContentPresenter headerIconPresenter)
            {
                headerIconPresenter.Visibility = HeaderIcon is null ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        #endregion
    }
}
