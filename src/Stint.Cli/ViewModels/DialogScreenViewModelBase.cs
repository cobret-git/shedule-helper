namespace Stint.Cli.ViewModels
{
    /// <summary>
    /// Base class for a dialog - see <see cref="IDialogScreenViewModel{TContext, TResult}"/>.
    /// Derived classes receive their context in <see cref="IScreenViewModel{TContext}.Initialize"/>
    /// and call <see cref="Close"/> with the answer when the user confirms or cancels. There is
    /// intentionally no navigation service here: closing is all a dialog can do.
    /// </summary>
    /// <typeparam name="TContext">The type of object this dialog is opened with.</typeparam>
    /// <typeparam name="TResult">The type of answer this dialog closes with.</typeparam>
    public abstract class DialogScreenViewModelBase<TContext, TResult> : ScreenViewModelCore, IDialogScreenViewModel<TContext, TResult>
    {
        #region Fields

        private readonly TaskCompletionSource _closed = new();
        #endregion

        #region Properties

        /// <inheritdoc />
        public Task Closed => _closed.Task;

        /// <inheritdoc />
        public TResult Result { get; private set; } = default!;
        #endregion

        #region Methods

        /// <inheritdoc />
        public abstract void Initialize(TContext context);

        /// <summary>
        /// Closes this dialog with <paramref name="result"/>. Only the first call has any effect,
        /// so a double key press can't answer the caller twice.
        /// </summary>
        protected void Close(TResult result)
        {
            if (_closed.Task.IsCompleted)
            {
                return;
            }

            Result = result;
            _closed.SetResult();
        }
        #endregion
    }
}
