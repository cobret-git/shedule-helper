using System;

namespace SheduleHelper.Core.Services
{
    /// <summary>
    /// Runs an action on the application's main (UI) thread. An unavoidable dependency for any
    /// WPF/WinUI host - ViewModels are constructed off the UI thread in some flows (e.g. after an
    /// awaited async call), and only the UI thread is allowed to touch UI-bound state. Concrete
    /// implementations live in each host application, wrapping that platform's own dispatcher.
    /// </summary>
    public interface IDispatcherService
    {
        #region Methods

        /// <summary>
        /// Runs the given action on the UI thread, dispatching to it if the caller is on a different thread.
        /// </summary>
        /// <param name="action">The action to run on the UI thread.</param>
        void Run(Action action);

        #endregion
    }
}
