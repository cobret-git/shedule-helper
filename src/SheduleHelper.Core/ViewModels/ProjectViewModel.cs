using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using SheduleHelper.Core.Components.Entities;
using SheduleHelper.Core.Models;
using SheduleHelper.Core.Services;
using System;
using System.Collections.ObjectModel;

namespace SheduleHelper.Core.ViewModels
{
    /// <summary>
    /// ViewModel for the single Project page, drilled into from Projects &amp; Tasks.
    /// </summary>
    public partial class ProjectViewModel : PageViewModel<Project>
    {
        #region Fields
        private readonly ILocalDbContextFactory _localDbFactory;
        private readonly IDispatcherService _dispatcherService;
        private readonly IDialogService _dialogService;
        private readonly INavigationService _navigationService;
        private readonly ObservableRangeCollection<TaskItem> _tasks = new();
        private readonly ILogger _logger = Serilog.Log.ForContext<ProjectViewModel>();
        private Project _project = null!;
        private CancellationTokenSource? _cts;
        private bool _isBusy;
        [ObservableProperty] private TaskItem? _selectedTask;
        #endregion

        #region Constructors
        public ProjectViewModel(ILocalDbContextFactory localDbFactory, 
            IDispatcherService dispatcherService, 
            INavigationService navigationService,
            IDialogService dialogService)
        {
            _localDbFactory = localDbFactory;
            _dispatcherService = dispatcherService;
            _dialogService = dialogService;
            _navigationService = navigationService;
        }
        #endregion

        #region Properties
        public string ProjectName { get => _project.Name; }
        public string? ProjectDescruption { get => _project.Description; }
        public IReadOnlyCollection<TaskItem> Tasks { get => _tasks; }
        public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) NotifyCanExecuteChanged(); } }
        #endregion

        #region Commands
        [RelayCommand(CanExecute = nameof(CanEditProject))] private async Task EditProjectAsync()
        {
            await Task.Run(async () =>
            {
                var editResult = await _dialogService.ShowEditProjectDialogAsync(_project);
                if (editResult != null) SetContext(editResult);
            });
        }
        [RelayCommand(CanExecute = nameof(CanCreateTask))] private async Task CreateTaskAsync()
        {
            await Task.Run(async () =>
            {
                var createResult = await _dialogService.ShowEditTaskItemDialogAsync(new EditTaskItemDialogContext(_project));
                if (createResult != null)
                {
                    _dispatcherService.Run(() =>
                    {
                        _tasks.Add(createResult);
                    });
                }
            });
        }
        [RelayCommand(CanExecute = nameof(CanEditTask))] private async Task EditTaskAsync()
        {
            if (SelectedTask == null) return;
            await Task.Run(async () =>
            {
                var editResult = await _dialogService.ShowEditTaskItemDialogAsync(new EditTaskItemDialogContext(_project, SelectedTask));
                if (editResult != null)
                {
                    _dispatcherService.Run(() =>
                    {
                        var oldTaskIndex = _tasks.IndexOf(SelectedTask);
                        _tasks[oldTaskIndex] = editResult;
                    });
                }
            });
        }
        [RelayCommand(CanExecute = nameof(CanDeleteTask))] private async Task DeleteTaskAsync()
        {
            if (SelectedTask == null) return;
            try
            {
                IsBusy = true;
                var ct = CreateCancellationToken();
                await Task.Run(async () =>
                {
                    await using var dbContext = _localDbFactory.CreateDbContext();
                    await dbContext.DeleteTaskAsync(SelectedTask.Id, ct);
                });
                _dispatcherService.Run(() =>
                {
                    _tasks.Remove(SelectedTask);
                });
            }
            catch (Exception ex)
            {
                // no error message here for user yet
                _logger.Error(ex, "Failed to delete task {TaskId} from project {ProjectId}", SelectedTask.Id, _project.Id);
            }
            finally
            {
                IsBusy = false;
            }
        }
        [RelayCommand(CanExecute =  nameof(CanDeleteProject))] private async Task DeleteProjectAsync()
        {
            try
            {
                IsBusy = true;
                var ct = CreateCancellationToken();
                await Task.Run(async () =>
                {
                    await using var dbContext = _localDbFactory.CreateDbContext();
                    await dbContext.DeleteProjectAsync(_project.Id, ct);
                });
                _dispatcherService.Run(() => _navigationService.NavigateToProjectsAndTasks());
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to delete project {ProjectId}", _project.Id);
            }
            finally
            {
                IsBusy = false;
            }
        }
        #endregion

        #region Methods

        /// <inheritdoc/>
        public override void SetContext(Project context)
        {
            _project = context;
            OnPropertyChanged(nameof(ProjectName));
            OnPropertyChanged(nameof(ProjectDescruption));
            _ = RefreshTasksAsync();
        }

        private async Task RefreshTasksAsync()
        {
            try
            {
                IsBusy = true;
                var ct = CreateCancellationToken();
                var tasks = new List<TaskItem>();

                await Task.Run(async () =>
                {
                    await using var dbContext = _localDbFactory.CreateDbContext();
                    tasks = await dbContext.GetTasksByProjectIdAsync(_project.Id, ct);
                }, ct);
                _dispatcherService.Run(() =>
                {
                    _tasks.Clear();
                    _tasks.AddRange(tasks);
                });
            }
            catch (Exception ex)
            {
                // no yet user's message
                _logger.Error(ex, "Failed to refresh tasks for project {ProjectId}", _project.Id);
            }
            finally
            {
                IsBusy = false;
            }
        }
        #endregion

        #region CanExecute
        private bool CanEditProject() => !IsBusy;
        private bool CanCreateTask() => !IsBusy;
        private bool CanDeleteProject() => !IsBusy;
        private bool CanEditTask() => !IsBusy && SelectedTask != null;
        private bool CanDeleteTask() => !IsBusy && SelectedTask != null;
        #endregion

        #region Helpers
        private void NotifyCanExecuteChanged()
        {
            _dispatcherService.Run(() =>
            {
                EditProjectCommand.NotifyCanExecuteChanged();
                CreateTaskCommand.NotifyCanExecuteChanged();
                EditTaskCommand.NotifyCanExecuteChanged();
                DeleteTaskCommand.NotifyCanExecuteChanged();
                DeleteProjectCommand.NotifyCanExecuteChanged();
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

        #region IDisposable
        public override void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
        #endregion
    }
}
