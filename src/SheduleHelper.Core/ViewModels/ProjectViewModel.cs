using CommunityToolkit.Mvvm.ComponentModel;
using SheduleHelper.Core.Components.Entities;
using System;

namespace SheduleHelper.Core.ViewModels
{
    /// <summary>
    /// ViewModel for the single Project page, drilled into from Projects &amp; Tasks.
    /// </summary>
    public partial class ProjectViewModel : PageViewModel<Project>
    {
        #region Properties

        #endregion

        #region Methods

        /// <inheritdoc/>
        public override void SetContext(Project context)
        {
            throw new NotImplementedException();
        }
        #endregion

        #region IDisposable
        public override void Dispose()
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}
