using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace YouTube4KDownloader;

public partial class MainWindow
{
    private readonly AppUpdateService _appUpdater = new();
    private readonly CancellationTokenSource _updateLifetime = new();
    private readonly DispatcherTimer _updateTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private ReleaseUpdate? _pendingUpdate;
    private bool _manualPending;
    private bool _checkingUpdate;
    private bool _applyingUpdate;
    private bool _isBusy;
    private DateTime _nextUpdateCheck = DateTime.UtcNow.AddHours(6);
    private bool _skipAutomaticUpdate;

    private void ApplyUpdateLanguage()
    {
        AutoToolUpdateCheckBox.Content = LocalizationManager.Get("AutoTools");
        AutoUpdateCheckBox.Content = LocalizationManager.Get("AutoApp");
        CheckAppUpdateButton.Content = LocalizationManager.Get("AppUpdate");
    }

    private void InitializeAppUpdates()
    {
        AutoUpdateCheckBox.IsChecked = _settings.AutoUpdateEnabled;
        _skipAutomaticUpdate = Environment.GetEnvironmentVariable("YTD_UPDATE_FAILED") == "1";
        Environment.SetEnvironmentVariable("YTD_UPDATE_FAILED", null);
        Loaded += async (_, _) =>
        {
            // Report WPF startup before enabling background downloads in an updated process.
            var ack = Environment.GetEnvironmentVariable("YTD_UPDATE_ACK");
            Environment.SetEnvironmentVariable("YTD_UPDATE_ACK", null);
            if (!string.IsNullOrWhiteSpace(ack))
            {
                try { File.WriteAllText(ack, "started"); } catch { }
            }
            _updateTimer.Start();
            if (_settings.AutoUpdateEnabled && !_skipAutomaticUpdate) await CheckAppUpdateAsync(false);
        };
        _updateTimer.Tick += async (_, _) =>
        {
            if (_updateLifetime.IsCancellationRequested || _applyingUpdate || _checkingUpdate) return;
            if (_pendingUpdate != null && (_settings.AutoUpdateEnabled || _manualPending)) await ApplyPendingUpdateAsync();
            else if (_settings.AutoUpdateEnabled && !_skipAutomaticUpdate && DateTime.UtcNow >= _nextUpdateCheck)
                await CheckAppUpdateAsync(false);
        };
    }

    private void AutoUpdate_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        _settings.AutoUpdateEnabled = AutoUpdateCheckBox.IsChecked == true;
        _store.SaveSettings(_settings);
    }

    private async void CheckAppUpdate_Click(object sender, RoutedEventArgs e) => await CheckAppUpdateAsync(true);

    private async Task CheckAppUpdateAsync(bool manual)
    {
        if (_checkingUpdate || _applyingUpdate) return;
        if (_pendingUpdate != null)
        {
            if (manual) await ApplyPendingUpdateAsync(true);
            return;
        }
        _checkingUpdate = true;
        CheckAppUpdateButton.IsEnabled = false;
        _nextUpdateCheck = DateTime.UtcNow.AddHours(6);
        try
        {
            AppendLog(LocalizationManager.Get("AppUpdate"));
            var release = await _appUpdater.CheckAsync(_updateLifetime.Token);
            if (release == null)
            {
                if (manual) MessageBox.Show(LocalizationManager.Get("UpToDate"));
                return;
            }
            if (manual && MessageBox.Show(string.Format(LocalizationManager.Get("UpdateConfirm"), release.Tag),
                "YouTube4KDownloader", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            await _appUpdater.PrepareAsync(release, new Progress<string>(m => AppendLog(LocalizationManager.Get("AppUpdate") + ": " + m)), _updateLifetime.Token);
            _pendingUpdate = release;
            _manualPending = manual;
            AppendLog(LocalizationManager.Get("UpdateReady"));
        }
        catch (OperationCanceledException) { if (!_updateLifetime.IsCancellationRequested) AppendLog(LocalizationManager.Get("UpdateTimeout")); }
        catch (Exception ex)
        {
            AppendLog(LocalizationManager.Get("UpdateFailed") + ": " + ex.Message);
            if (manual) MessageBox.Show(ex.Message, LocalizationManager.Get("AppUpdate"));
        }
        finally { _checkingUpdate = false; CheckAppUpdateButton.IsEnabled = true; }
        if (_pendingUpdate != null && !_updateLifetime.IsCancellationRequested && (manual || _settings.AutoUpdateEnabled))
            await ApplyPendingUpdateAsync(manual);
    }

    private async Task ApplyPendingUpdateAsync(bool manual = false)
    {
        if (_pendingUpdate == null || _isBusy || _isQueueRunning || _applyingUpdate) return;
        // Never discard a manually queued, not-yet-started download on automatic restart.
        if (_queue.Any(x => x.Status != T("Completed") && x.Status != T("Failed") && x.Status != T("Cancelled")))
        {
            if (manual) MessageBox.Show(LocalizationManager.Get("FinishQueue"));
            return;
        }
        _applyingUpdate = true;
        IsEnabled = false;
        _clipboardTimer.Stop();
        try
        {
            SaveSettings();
            _store.SaveHistory(_history);
            AppendLog(LocalizationManager.Get("Installing"));
            await _appUpdater.LaunchInstallerAsync(_updateLifetime.Token);
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            _applyingUpdate = false;
            _pendingUpdate = null;
            _appUpdater.Cleanup();
            IsEnabled = true;
            if (_settings.AutoClipboardEnabled) _clipboardTimer.Start();
            if (!_updateLifetime.IsCancellationRequested)
            {
                AppendLog(LocalizationManager.Get("ApplyFailed") + ": " + ex.Message);
                MessageBox.Show(ex.Message, LocalizationManager.Get("ApplyFailed"));
            }
        }
    }
}
