using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace YouTube4KDownloader;

public partial class MainWindow
{
    private readonly DispatcherTimer _toolUpdateTimer = new() { Interval = TimeSpan.FromSeconds(30) };
    private DateTime _toolRetryAfter;

    private void InitializeToolUpdates()
    {
        AutoToolUpdateCheckBox.IsChecked = _settings.AutoToolUpdateEnabled;
        _toolUpdateTimer.Tick += async (_, _) => await TryAutoToolUpdateAsync();
        Loaded += async (_, _) => { _toolUpdateTimer.Start(); await TryAutoToolUpdateAsync(); };
    }

    private void AutoToolUpdate_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        _settings.AutoToolUpdateEnabled = AutoToolUpdateCheckBox.IsChecked == true;
        _store.SaveSettings(_settings);
        _toolRetryAfter = DateTime.MinValue;
    }

    private async Task TryAutoToolUpdateAsync()
    {
        var now = DateTime.UtcNow;
        if (!_settings.AutoToolUpdateEnabled || _isBusy || _isQueueRunning || _applyingUpdate || _checkingUpdate ||
            _updateLifetime.IsCancellationRequested || now < _toolRetryAfter) return;
        if (_pendingUpdate != null && (_settings.AutoUpdateEnabled || _manualPending)) return;
        if (now - _settings.LastToolUpdateCheckUtc < TimeSpan.FromHours(24)) return;
        // SetBusy is acquired before the first await so downloads cannot begin during tool replacement.
        SetBusy(true, LocalizationManager.Get("UpdatingTools"));
        _cts = CancellationTokenSource.CreateLinkedTokenSource(_updateLifetime.Token);
        _toolRetryAfter = now.AddHours(1);
        try
        {
            await EnsureToolsAsync(true);
            _settings.LastToolUpdateCheckUtc = DateTime.UtcNow;
            _store.SaveSettings(_settings);
        }
        catch (OperationCanceledException) { AppendLog(LocalizationManager.Get("Cancelled")); }
        catch (Exception ex) { AppendLog(LocalizationManager.Get("UpdateFailed") + ": " + ex.Message); }
        finally { SetBusy(false, T("Ready")); }
    }
}
