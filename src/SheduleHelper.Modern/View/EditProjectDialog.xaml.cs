using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using SheduleHelper.Core.ViewModels;

namespace SheduleHelper.Modern.View
{
    /// <summary>
    /// Edit Project dialog. Resolves its own <see cref="EditProjectDialogViewModel"/> into its
    /// DataContext; <see cref="Services.DialogService"/> assigns XamlRoot and reads the DataContext
    /// back to drive the context/result lifecycle.
    /// </summary>
    public sealed partial class EditProjectDialog : ContentDialog
    {
        public EditProjectDialog()
        {
            InitializeComponent();
            DataContext = App.Current.Services.GetRequiredService<EditProjectDialogViewModel>();
        }
    }
}
