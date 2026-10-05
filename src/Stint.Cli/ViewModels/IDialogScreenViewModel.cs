namespace Stint.Cli.ViewModels
{
    /// <summary>
    /// A modal screen that is opened with a context and answers with a result - the console
    /// equivalent of <c>MessageBox.Show</c> / <c>ContentDialog.ShowAsync</c>: the caller opens it
    /// with <see cref="Services.INavigationService.ShowDialogAsync{TDialog, TContext, TResult}"/>
    /// and simply awaits the answer.
    /// </summary>
    /// <remarks>
    /// A dialog can only close, never navigate: it is given no navigation service, and ends by
    /// completing <see cref="Closed"/> (derived classes do that through
    /// <see cref="DialogScreenViewModelBase{TContext, TResult}"/>). The navigation service reacts
    /// by popping and disposing the dialog, and only then reads <see cref="Result"/> and resumes
    /// the caller. Cancelling is an outcome like any other and belongs inside
    /// <typeparamref name="TResult"/> (e.g. an <c>IsPicked</c> flag).
    /// </remarks>
    /// <typeparam name="TContext">The type of object this dialog is opened with.</typeparam>
    /// <typeparam name="TResult">The type of answer this dialog closes with.</typeparam>
    public interface IDialogScreenViewModel<in TContext, out TResult> : IScreenViewModel<TContext>
    {
        #region Properties

        /// <summary>Completes when the dialog has its answer and wants to be closed.</summary>
        Task Closed { get; }

        /// <summary>The dialog's answer. Only meaningful once <see cref="Closed"/> has completed.</summary>
        TResult Result { get; }

        #endregion
    }
}
