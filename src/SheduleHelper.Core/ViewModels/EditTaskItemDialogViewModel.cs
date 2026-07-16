using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using SheduleHelper.Core.Components.Entities;
using SheduleHelper.Core.Models;
using SheduleHelper.Core.Services;
using System;
using MSG = SheduleHelper.Core.Resources.Strings.Messages;

namespace SheduleHelper.Core.ViewModels
{
    /// <summary>
    /// ViewModel for the Edit Task Item dialog. Takes an <see cref="EditTaskItemDialogContext"/>
    /// (the task being edited, or <see langword="null"/> to create a new one, together with the
    /// project it belongs to) as context, and returns the created/edited task as its result.
    /// </summary>
    public partial class EditTaskItemDialogViewModel : DialogViewModel<TaskItem?, EditTaskItemDialogContext>
    {
        #region Fields
        public const int TASK_TITLE_MAX_LENGTH = 150;
        public const int TASK_DESCRIPTION_MAX_LENGTH = 1000;
        private readonly ILocalDbContextFactory _localDbFactory;
        private readonly IDispatcherService _dispatcherService;
        private readonly ILogger _logger = Serilog.Log.ForContext<EditTaskItemDialogViewModel>();
        private CancellationTokenSource? _cts;
        private Project _project = null!;
        private TaskItem? _task;
        private bool _isBusy;
        [ObservableProperty] private string? _taskTitle;
        [ObservableProperty] private string? _taskDescription;
        [ObservableProperty] private TaskItemStatus _taskStatus;
        private string? _message;
        #endregion

        #region Constructors

        /// <inheritdoc/>
        public EditTaskItemDialogViewModel(ILocalDbContextFactory localDbFactory,
            IDispatcherService dispatcherService,
            IDialogService dialogService) : base(dialogService)
        {
            _localDbFactory = localDbFactory;
            _dispatcherService = dispatcherService;
        }

        #endregion

        #region Properties
        public string ProjectName { get => _project.Name; }
        public string? Message { get => _message; private set { if (SetProperty(ref _message, value)) OnPropertyChanged(nameof(MessageVisible)); } }
        public bool MessageVisible { get => !string.IsNullOrWhiteSpace(Message); }
        public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) NotifyCanExecuteChanged(); } }
        #endregion

        #region Commands
        [RelayCommand(CanExecute = nameof(CanSaveTaskChanges))] private async Task SaveChangesAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TaskTitle))
                {
                    Message = MSG.error_taskTitleEmpty;
                    return;
                }
                IsBusy = true;
                var ct = CreateCancellationToken();
                await using var dbContext = _localDbFactory.CreateDbContext();
                var updateExisting = _task != null;
                if (updateExisting)
                {
                    var trackedEntity = (await dbContext.GetTasksByProjectIdAsync(_project.Id, ct)).First(x => x.Id == _task!.Id);
                    trackedEntity.Title = TaskTitle;
                    trackedEntity.Description = TaskDescription;
                    trackedEntity.Status = TaskStatus;
                    _task = await dbContext.UpdateTaskAsync(trackedEntity, ct);
                }
                else
                {
                    _task = await dbContext.CreateTaskAsync(TaskTitle, TaskDescription, TaskStatus, _project.Id, ct);
                }
                Close();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to save changes for task {TaskId} in project {ProjectId}.", _task?.Id, _project.Id);
                Message = MSG.error_taskSaveUnexpected;
            }
            finally
            {
                IsBusy = false;
            }
        }
        [RelayCommand] private void Cancel()
        {
            Close();
        }
        #endregion

        #region Methods

        /// <inheritdoc/>
        public override void SetDialogContext(EditTaskItemDialogContext context)
        {
            _project = context.Project;
            _task = context.TaskItem;
            TaskTitle = _task?.Title;
            TaskDescription = _task?.Description;
            TaskStatus = _task?.Status ?? TaskItemStatus.Todo;
        }

        /// <inheritdoc/>
        public override TaskItem? GetDialogResult()
        {
            return _task;
        }

        /// <inheritdoc/>
        public override void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        #endregion

        #region Handlers
        partial void OnTaskTitleChanged(string? value)
        {
            Message = null;
            NotifyCanExecuteChanged();
        }
        #endregion

        #region CanExecute
        private bool CanSaveTaskChanges() => !string.IsNullOrWhiteSpace(TaskTitle) && !IsBusy;
        #endregion

        #region Helpers
        private void NotifyCanExecuteChanged()
        {
            _dispatcherService.Run(() =>
            {
                SaveChangesCommand.NotifyCanExecuteChanged();
            });
        }
        private CancellationToken CreateCancellationToken()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new();
            return _cts.Token;
        }
        #endregion
    }
}
