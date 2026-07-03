using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace SheduleHelper.WpfApp.ViewModel
{
    public partial class MainWindowViewModel : ObservableObject
    {
        #region Constructors
        public MainWindowViewModel()
        {
            Tabs = new ObservableCollection<ITabViewModel>()
            {

            };
        }
        #endregion

        #region Properties
        public ObservableCollection<ITabViewModel> Tabs { get; } = new();
        #endregion

        #region Methods

        #endregion
    }
}
