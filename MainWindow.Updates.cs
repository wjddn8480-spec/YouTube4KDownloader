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
    private static string U(string korean, string english) => LocalizationManager.CurrentLanguage == "ko" ? korean : english;

    private void ApplyUpdateLanguage()
    {
        AutoToolUpdateCheckBox.Content = U("도구 자동 업데이트", "Auto-update tools");
        AutoUpdateCheckBox.Content = U("프로그램 자동 업데이트", "Auto-update app");
        CheckAppUpdateButton.Content = U("앱 업데이트 확인", "Check app update");
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
            AppendLog(U("프로그램 업데이트 확인 중...", "Checking application updates..."));
            var release = await _appUpdater.CheckAsync(_updateLifetime.Token);
            if (release == null)
            {
                if (manual) MessageBox.Show(U("최신 버전입니다.", "You are up to date."));
                return;
            }
            if (manual && MessageBox.Show(U($"{release.Tag} 버전을 다운로드하고 작업 완료 후 적용할까요?", $"Download {release.Tag} and install when idle?"),
                "YouTube4KDownloader", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            await _appUpdater.PrepareAsync(release, new Progress<string>(m => AppendLog(U("앱 업데이트 다운로드: ", "App update download: ") + m)), _updateLifetime.Token);
            _pendingUpdate = release;
            _manualPending = manual;
            AppendLog(U("업데이트 준비 완료. 대기열과 진행 중인 작업이 끝나면 적용합니다.", "Update ready. Installation waits for active work and the queue to finish."));
        }
        catch (OperationCanceledException) { if (!_updateLifetime.IsCancellationRequested) AppendLog(U("업데이트 요청 시간이 초과되었습니다.", "Update request timed out.")); }
        catch (Exception ex)
        {
            AppendLog(U("앱 업데이트 실패: ", "App update failed: ") + ex.Message);
            if (manual) MessageBox.Show(ex.Message, U("앱 업데이트", "App update"));
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
            if (manual) MessageBox.Show(U("대기열 작업을 완료하거나 비운 뒤 다시 눌러주세요.", "Finish or clear the waiting queue before installing."));
            return;
        }
        _applyingUpdate = true;
        IsEnabled = false;
        _clipboardTimer.Stop();
        try
        {
            SaveSettings();
            _store.SaveHistory(_history);
            AppendLog(U("업데이트 적용 후 프로그램을 다시 시작합니다.", "Installing update and restarting the application."));
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
                AppendLog(U("업데이트 적용 실패: ", "Update installation failed: ") + ex.Message);
                MessageBox.Show(ex.Message, U("업데이트 적용 실패", "Update installation failed"));
            }
        }
    }
}
