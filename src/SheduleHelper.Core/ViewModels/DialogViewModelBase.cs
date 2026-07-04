using CommunityToolkit.Mvvm.ComponentModel;
using SheduleHelper.Core.Services;
using System;

namespace SheduleHelper.Core.ViewModels
{
    /// <summary>
    /// Base class for ViewModels backing a modal dialog. Mirrors <see cref="PageViewModelBase"/>'s
    /// role for pages: lifecycle (disposal) is owned by <see cref="IDialogService"/>, which
    /// constructs, activates, and disposes dialog ViewModels around each <c>ShowXDialogAsync</c> call.
    /// </summary>
    public abstract class DialogViewModelBase : ObservableObject, IDisposable
    {
        #region Fields

        /// <summary>
        /// The dialog service that owns this ViewModel's dialog, used to close it programmatically.
        /// </summary>
        protected readonly IDialogService DialogService;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="DialogViewModelBase"/> class.
        /// </summary>
        /// <param name="dialogService">The dialog service that owns this ViewModel's dialog.</param>
        protected DialogViewModelBase(IDialogService dialogService)
        {
            DialogService = dialogService;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Closes the dialog currently showing this ViewModel.
        /// </summary>
        protected void Close() => DialogService.CloseDialog();

        /// <summary>
        /// Releases resources held by this ViewModel. Called by the dialog service once the dialog closes.
        /// </summary>
        public abstract void Dispose();

        #endregion
    }

    /// <summary>
    /// Base class for ViewModels backing a dialog that produces a result when closed.
    /// </summary>
    /// <typeparam name="TResult">The type of result this dialog produces.</typeparam>
    public abstract class DialogViewModel<TResult> : DialogViewModelBase
    {
        #region Constructors

        /// <inheritdoc/>
        protected DialogViewModel(IDialogService dialogService) : base(dialogService)
        {
        }

        #endregion

        #region Methods

        /// <summary>
        /// Called by the dialog service after the dialog is confirmed (its primary button was
        /// invoked), to retrieve the result to return to the caller.
        /// </summary>
        public abstract TResult? GetDialogResult();

        #endregion
    }

    /// <summary>
    /// Base class for ViewModels backing a dialog that requires a navigation context (e.g. the
    /// entity being edited) and produces a result when closed.
    /// </summary>
    /// <typeparam name="TResult">The type of result this dialog produces.</typeparam>
    /// <typeparam name="TContext">The type of context this dialog's ViewModel requires.</typeparam>
    public abstract class DialogViewModel<TResult, TContext> : DialogViewModel<TResult>
    {
        #region Constructors

        /// <inheritdoc/>
        protected DialogViewModel(IDialogService dialogService) : base(dialogService)
        {
        }

        #endregion

        #region Methods

        /// <summary>
        /// Called by the dialog service immediately before the dialog is shown, supplying the
        /// <typeparamref name="TContext"/> it needs to display.
        /// </summary>
        /// <param name="context">The context passed to the typed <c>ShowXDialogAsync</c> call.</param>
        public abstract void SetDialogContext(TContext context);

        #endregion
    }
}
