using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using SheduleHelper.Core.Components.Entities;
using SheduleHelper.Core.Services;
using System;
using MSG = SheduleHelper.Core.Resources.Strings.Messages;

namespace SheduleHelper.Core.ViewModels
{
    /// <summary>
    /// ViewModel for the Edit Project dialog. Takes an existing project (or <see langword="null"/>
    /// to create a new one) as context, and returns the created/edited project as its result.
    /// </summary>
    public partial class EditProjectDialogViewModel : DialogViewModel<Project?, Project?>
    {
        #region Fields
        public const int PROJECT_NAME_MAX_LENGTH = 100;
        public const int PROJECT_DESCRIPTION_MAX_LENGTH = 500;
        private readonly ILocalDbContextFactory _localDbFactory;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly IDispatcherService _dispatcherService;
        private readonly ILogger _logger = Serilog.Log.ForContext<EditProjectDialogViewModel>();
        private CancellationTokenSource? _cts;
        private Project? _project = null!;
        private bool _isBusy;
        [ObservableProperty] private string? _projectName;
        [ObservableProperty] private string? _projectDescription;
        private string? _message;
        #endregion

        #region Constructors

        /// <inheritdoc/>
        public EditProjectDialogViewModel(ILocalDbContextFactory localDbFactory, 
            ICurrentUserContext currentUserContext, 
            IDispatcherService dispatcherService,
            IDialogService dialogService) : base(dialogService)
        {
            _localDbFactory = localDbFactory;
            _currentUserContext = currentUserContext;
            _dispatcherService = dispatcherService;
        }

        #endregion

        #region Properties
        public string? Message { get => _message; private set { if (SetProperty(ref _message, value)) OnPropertyChanged(nameof(MessageVisible)); } }
        public bool MessageVisible { get => !string.IsNullOrWhiteSpace(Message); }
        public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) NotifyCanExecuteChanged(); } }
        #endregion

        #region Commands
        [RelayCommand(CanExecute = nameof(CanSaveProjectsChanges))] private async Task SaveChangesAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ProjectName))
                {
                    Message = MSG.error_projectNameEmpty;
                    return;
                }
                IsBusy = false;
                var ct = CreateCancellationToken();
                await using var dbContext = _localDbFactory.CreateDbContext();
                var updateExisting = _project != null;
                if (updateExisting)
                {
                    var trackedEntity = (await dbContext.GetProjectsByUserIdAsync(_currentUserContext.UserId, ct)).First(x => x.Id == _project!.Id);
                    trackedEntity.Name = ProjectName;
                    trackedEntity.Description = ProjectDescription;
                    trackedEntity.UserId = _currentUserContext.UserId;
                    _project = await dbContext.UpdateProjectAsync(trackedEntity, ct);
                }
                else
                {
                    _project = await dbContext.CreateProjectAsync(ProjectName, _currentUserContext.UserId, ProjectDescription, ct);
                }
                Close();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to save changes for project {ProjectId}.", _project?.Id);
                Message = MSG.error_projectSaveUnexpected;
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
        public override void SetDialogContext(Project? context)
        {
            _project = context ?? new Project();
            ProjectName = _project.Name;
            ProjectDescription = _project.Description;
        }

        /// <inheritdoc/>
        public override Project? GetDialogResult()
        {
            return _project;
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
        partial void OnProjectNameChanged(string? value)
        {
            Message = null;
            NotifyCanExecuteChanged();
        }
        #endregion

        #region CanExecute
        private bool CanSaveProjectsChanges() => !string.IsNullOrWhiteSpace(ProjectName) && !IsBusy;
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
