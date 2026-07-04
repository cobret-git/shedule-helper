using SheduleHelper.Core.Components.Entities;
using SheduleHelper.Core.Services;
using System;

namespace SheduleHelper.Core.ViewModels
{
    /// <summary>
    /// ViewModel for the Edit Project dialog. Takes an existing project (or <see langword="null"/>
    /// to create a new one) as context, and returns the created/edited project as its result.
    /// </summary>
    public class EditProjectDialogViewModel : DialogViewModel<Project?, Project?>
    {
        #region Constructors

        /// <inheritdoc/>
        public EditProjectDialogViewModel(IDialogService dialogService) : base(dialogService)
        {
        }

        #endregion

        #region Methods

        /// <inheritdoc/>
        public override void SetDialogContext(Project? context)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Project? GetDialogResult()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override void Dispose()
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
