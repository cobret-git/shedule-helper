using Microsoft.UI.Dispatching;
using SheduleHelper.Core.Services;
using System;

namespace SheduleHelper.Modern.Services
{
    /// <summary>
    /// Concrete WinUI implementation of <see cref="IDispatcherService"/>, wrapping a
    /// <see cref="DispatcherQueue"/>. Requires one-time setup via <see cref="Initialize"/> with the
    /// UI thread's dispatcher queue, mirroring <see cref="DialogService.Initialize"/>.
    /// </summary>
    public class DispatcherService : IDispatcherService
    {
        #region Fields

        private DispatcherQueue? _dispatcherQueue;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="DispatcherService"/> class.
        /// </summary>
        public DispatcherService()
        {
        }

        #endregion

        #region Methods

        /// <summary>
        /// Associates this service with the UI thread's <see cref="DispatcherQueue"/>. Must be
        /// called once, before any <see cref="Run"/> call.
        /// </summary>
        /// <param name="dispatcherQueue">The dispatcher queue of the thread that owns the UI.</param>
        public void Initialize(DispatcherQueue dispatcherQueue)
        {
            _dispatcherQueue = dispatcherQueue;
        }

        /// <inheritdoc/>
        public void Run(Action action)
        {
            if (_dispatcherQueue is null)
            {
                throw new InvalidOperationException($"{nameof(DispatcherService)} has not been initialized. Call {nameof(Initialize)} first.");
            }

            if (_dispatcherQueue.HasThreadAccess)
            {
                action();
                return;
            }

            _dispatcherQueue.TryEnqueue(() => action());
        }

        #endregion
    }
}
