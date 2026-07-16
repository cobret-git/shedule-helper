using SheduleHelper.Core.Components.Entities;
using SheduleHelper.Core.Services;

namespace SheduleHelper.Core.ViewModels
{
    public partial class EditTaskItemDialogViewModel : DialogViewModel<TaskItem?, TaskItem?>
    {
        #region Fields
        #endregion

        #region Constructors
        public EditTaskItemDialogViewModel(IDialogService dialogService) : base(dialogService)
        {
        }
        #endregion

        #region Properties
        #endregion

        #region Methods
        public override TaskItem? GetDialogResult()
        {
            throw new NotImplementedException();
        }

        public override void SetDialogContext(TaskItem? context)
        {
            throw new NotImplementedException();
        }
        #endregion

        #region Handlers
        #endregion

        #region Helpers
        #endregion

        #region IDisposable

        public override void Dispose()
        {
            throw new NotImplementedException();
        }
        #endregion        
    }
}
