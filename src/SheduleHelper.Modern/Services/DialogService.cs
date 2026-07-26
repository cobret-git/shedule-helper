using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SheduleHelper.Core.Components.Entities;
using SheduleHelper.Core.Models;
using SheduleHelper.Core.Services;
using SheduleHelper.Core.ViewModels;
using SheduleHelper.Modern.View;
using System;
using System.Threading.Tasks;

namespace SheduleHelper.Modern.Services
{
    /// <summary>
    /// Concrete WinUI implementation of <see cref="IDialogService"/>. Shows dialogs via
    /// <see cref="ContentDialog.ShowAsync"/> - no manual message-pump blocking is needed (unlike a
    /// hand-rolled WPF dialog host), since <c>ShowAsync</c> is already an awaitable that completes
    /// when the dialog closes. Each dialog resolves its own ViewModel into its DataContext (mirroring
    /// how pages resolve their own ViewModel); this service just reads it back to apply the
    /// context/result lifecycle. Requires one-time setup via <see cref="Initialize"/> with the
    /// hosting <see cref="XamlRoot"/>, which every <see cref="ContentDialog"/> needs before it can
    /// be shown - a WinUI-specific concern, so it lives only here, not on the interface.
    /// </summary>
    public class DialogService : IDialogService
    {
        #region Fields

        private XamlRoot? _xamlRoot;
        private ContentDialog? _currentDialog;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="DialogService"/> class.
        /// </summary>
        public DialogService()
        {
        }

        #endregion

        #region Methods

        /// <summary>
        /// Associates this service with the <see cref="XamlRoot"/> that dialogs should be shown
        /// against. Must be called once, before any <c>ShowXDialogAsync</c> method.
        /// </summary>
        /// <param name="xamlRoot">The XamlRoot of the window hosting dialogs.</param>
        public void Initialize(XamlRoot xamlRoot)
        {
            _xamlRoot = xamlRoot;
        }

        /// <inheritdoc/>
        public Task<Project?> ShowEditProjectDialogAsync(Project? existingProject)
        {
            return ShowCoreAsync<EditProjectDialog, EditProjectDialogViewModel, Project?, Project?>(existingProject);
        }

        /// <inheritdoc/>
        public Task<TaskItem?> ShowEditTaskItemDialogAsync(EditTaskItemDialogContext context)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public async Task ShowMessageDialogAsync(string title, string message)
        {
            if (_xamlRoot is null)
            {
                throw new InvalidOperationException($"{nameof(DialogService)} has not been initialized. Call {nameof(Initialize)} first.");
            }

            var dialog = new ContentDialog
            {
                XamlRoot = _xamlRoot,
                Title = title,
                Content = message,
                CloseButtonText = "OK",
            };

            _currentDialog = dialog;
            await dialog.ShowAsync();
            _currentDialog = null;
        }

        /// <inheritdoc/>
        public void CloseDialog()
        {
            _currentDialog?.Hide();
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Shows <typeparamref name="TDialog"/> against the registered <see cref="XamlRoot"/>,
        /// supplies its self-resolved ViewModel's context, and returns that ViewModel's result if the
        /// dialog was confirmed (its primary button was invoked) - or <see langword="null"/>/
        /// <see langword="default"/> if it was dismissed any other way, so callers never observe a
        /// half-edited ViewModel's state as if it were a confirmed result.
        /// </summary>
        private async Task<TResult?> ShowCoreAsync<TDialog, TViewModel, TContext, TResult>(TContext context)
            where TDialog : ContentDialog, new()
            where TViewModel : DialogViewModel<TResult, TContext>
        {
            if (_xamlRoot is null)
            {
                throw new InvalidOperationException($"{nameof(DialogService)} has not been initialized. Call {nameof(Initialize)} first.");
            }

            var dialog = new TDialog
            {
                XamlRoot = _xamlRoot
            };

            if (dialog.DataContext is not TViewModel viewModel)
            {
                throw new InvalidOperationException($"{typeof(TDialog).Name} did not resolve a {typeof(TViewModel).Name} as its DataContext.");
            }

            viewModel.SetDialogContext(context);
            _currentDialog = dialog;

            var dialogResult = await dialog.ShowAsync();
            var result = dialogResult == ContentDialogResult.Primary ? viewModel.GetDialogResult() : default;

            viewModel.Dispose();
            _currentDialog = null;

            return result;
        }

        #endregion
    }
}
