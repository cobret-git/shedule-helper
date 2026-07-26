using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using SheduleHelper.Core.Components.Entities;
using SheduleHelper.Core.Components.Settings;
using SheduleHelper.Core.Services;
using System;
using System.Collections.Generic;
using MSG = SheduleHelper.Core.Resources.Strings.Messages;
using CONTENT = SheduleHelper.Core.Resources.Strings.Content;

namespace SheduleHelper.Core.ViewModels
{
    /// <summary>
    /// ViewModel for the Settings page (The Rule Setter).
    /// </summary>
    public partial class SettingsViewModel : PageViewModelBase
    {
        #region Fields
        private readonly ILocalDbContextFactory _localDbFactory;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly IDispatcherService _dispatcherService;
        private readonly ISettingsService _settingsService;
        private readonly IThemeApplier _themeApplier;
        private readonly IDialogService _dialogService;
        private readonly ILogger _logger = Serilog.Log.ForContext<SettingsViewModel>();
        private CancellationTokenSource? _cts;
        private UserSetting _userSetting = null!;
        private bool _isLoadingSettings;
        private bool _isBusy;
        [ObservableProperty] private decimal _targetShiftHours;
        [ObservableProperty] private TimeOnly _defaultClockInTime;
        [ObservableProperty] private TimeOnly _defaultClockOutTime;
        [ObservableProperty] private LunchStrategy _lunchStrategy;
        [ObservableProperty] private TimeOnly _lunchStartTime;
        [ObservableProperty] private TimeOnly _lunchEndTime;
        [ObservableProperty] private int _lunchDurationMinutes;
        [ObservableProperty] private AppTheme _theme;
        [ObservableProperty] private string _culture = "en-US";
        private string? _message;
        private bool _hasChanges;
        #endregion

        #region Constructors
        public SettingsViewModel(ILocalDbContextFactory localDbFactory,
            ICurrentUserContext currentUserContext,
            IDispatcherService dispatcherService,
            ISettingsService settingsService,
            IThemeApplier themeApplier,
            IDialogService dialogService)
        {
            _localDbFactory = localDbFactory;
            _currentUserContext = currentUserContext;
            _dispatcherService = dispatcherService;
            _settingsService = settingsService;
            _themeApplier = themeApplier;
            _dialogService = dialogService;
            _ = LoadSettingsAsync();
        }
        #endregion

        #region Properties
        public IReadOnlyList<LunchStrategy> LunchStrategyOptions { get; } = Enum.GetValues<LunchStrategy>();
        public IReadOnlyList<AppTheme> ThemeOptions { get; } = Enum.GetValues<AppTheme>();
        public IReadOnlyList<string> CultureOptions { get; } = new[] { "en-US", "cs-CZ" };
        public string? Message { get => _message; private set { if (SetProperty(ref _message, value)) OnPropertyChanged(nameof(MessageVisible)); } }
        public bool MessageVisible { get => !string.IsNullOrWhiteSpace(Message); }
        public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) NotifyCanExecuteChanged(); } }
        public bool HasChanges { get => _hasChanges; private set { if (SetProperty(ref _hasChanges, value)) NotifyCanExecuteChanged(); } }
        #endregion

        #region Commands
        [RelayCommand(CanExecute = nameof(CanSaveChanges))] private async Task SaveChangesAsync()
        {
            try
            {
                IsBusy = true;
                var ct = CreateCancellationToken();

                // Theme/language are local app-instance preferences (ISettingsService, a small
                // JSON file) - independent of the UserSetting/EF save below, so a failure in one
                // shouldn't block the other. Only act when the value actually changed, so saving
                // unrelated fields (e.g. shift hours) doesn't re-apply the theme or re-show the
                // restart prompt every time.
                var cultureChanged = _settingsService.Settings.Culture != Culture;
                if (_settingsService.Settings.Theme != Theme)
                {
                    _themeApplier.Apply(Theme);
                }
                if (cultureChanged)
                {
                    _settingsService.Settings.Culture = Culture;
                    _settingsService.Save();
                }

                _userSetting.TargetShiftHours = TargetShiftHours;
                _userSetting.DefaultClockInTime = DefaultClockInTime;
                _userSetting.DefaultClockOutTime = DefaultClockOutTime;
                _userSetting.LunchStrategy = LunchStrategy;
                _userSetting.LunchStartTime = LunchStartTime;
                _userSetting.LunchEndTime = LunchEndTime;
                _userSetting.LunchDurationMinutes = LunchDurationMinutes;

                await using var dbContext = _localDbFactory.CreateDbContext();
                _userSetting = await dbContext.UpdateUserSettingAsync(_userSetting, ct);
                HasChanges = false;

                if (cultureChanged)
                {
                    await _dialogService.ShowMessageDialogAsync(CONTENT.restart_required_title, CONTENT.restart_required_message);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to save settings for user {UserId}.", _currentUserContext.UserId);
                Message = MSG.error_settingsSaveUnexpected;
            }
            finally
            {
                IsBusy = false;
            }
        }
        [RelayCommand(CanExecute = nameof(CanDiscardChanges))] private void DiscardChanges()
        {
            ApplySettingsToProperties(_userSetting);
            Message = null;
        }
        #endregion

        #region Methods

        /// <inheritdoc/>
        public override void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        #endregion

        #region Handlers
        partial void OnTargetShiftHoursChanged(decimal value) => OnSettingChanged();
        partial void OnDefaultClockInTimeChanged(TimeOnly value) => OnSettingChanged();
        partial void OnDefaultClockOutTimeChanged(TimeOnly value) => OnSettingChanged();
        partial void OnLunchStrategyChanged(LunchStrategy value) => OnSettingChanged();
        partial void OnLunchStartTimeChanged(TimeOnly value) => OnSettingChanged();
        partial void OnLunchEndTimeChanged(TimeOnly value) => OnSettingChanged();
        partial void OnLunchDurationMinutesChanged(int value) => OnSettingChanged();
        partial void OnThemeChanged(AppTheme value) => OnSettingChanged();
        partial void OnCultureChanged(string value) => OnSettingChanged();
        #endregion

        #region CanExecute
        private bool CanSaveChanges() => !IsBusy && HasChanges;
        private bool CanDiscardChanges() => !IsBusy && HasChanges;
        #endregion

        #region Helpers
        private async Task LoadSettingsAsync()
        {
            try
            {
                IsBusy = true;
                var ct = CreateCancellationToken();
                await _currentUserContext.EnsureInitializedAsync();
                var userId = _currentUserContext.UserId;

                await using var dbContext = _localDbFactory.CreateDbContext();
                var userSetting = await dbContext.GetUserSettingAsync(userId, ct)
                    ?? await dbContext.CreateUserSettingAsync(userId, ct);

                _dispatcherService.Run(() =>
                {
                    _userSetting = userSetting;
                    ApplySettingsToProperties(_userSetting);
                });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load settings for user {UserId}.", _currentUserContext.UserId);
                Message = MSG.error_settingsLoadUnexpected;
            }
            finally
            {
                IsBusy = false;
            }
        }
        private void ApplySettingsToProperties(UserSetting userSetting)
        {
            _isLoadingSettings = true;
            TargetShiftHours = userSetting.TargetShiftHours;
            DefaultClockInTime = userSetting.DefaultClockInTime;
            DefaultClockOutTime = userSetting.DefaultClockOutTime;
            LunchStrategy = userSetting.LunchStrategy;
            LunchStartTime = userSetting.LunchStartTime;
            LunchEndTime = userSetting.LunchEndTime;
            LunchDurationMinutes = userSetting.LunchDurationMinutes;
            // Theme/language aren't part of UserSetting - they're local app-instance preferences
            // read straight from ISettingsService instead (see SaveChangesAsync).
            Theme = _settingsService.Settings.Theme;
            Culture = _settingsService.Settings.Culture;
            _isLoadingSettings = false;
            HasChanges = false;
        }
        private void OnSettingChanged()
        {
            if (_isLoadingSettings) return;
            Message = null;
            HasChanges = true;
        }
        private void NotifyCanExecuteChanged()
        {
            _dispatcherService.Run(() =>
            {
                SaveChangesCommand.NotifyCanExecuteChanged();
                DiscardChangesCommand.NotifyCanExecuteChanged();
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
