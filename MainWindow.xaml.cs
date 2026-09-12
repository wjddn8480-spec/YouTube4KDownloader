using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace YouTube4KDownloader;

public partial class MainWindow : Window
{
    private readonly ToolManager _toolManager = new();
    private readonly YtDlpService _downloader;
    private readonly SettingsStore _store = new();
    private readonly ObservableCollection<DownloadQueueItem> _queue = new();
    private readonly ObservableCollection<DownloadHistoryItem> _history;
    private AppSettings _settings;
    private CancellationTokenSource? _cts;
    private static readonly HttpClient ImageClient = new();
    private readonly DispatcherTimer _clipboardTimer = new() { Interval = TimeSpan.FromMilliseconds(700) };
    private string _lastClipboardText = string.Empty;
    private bool _isInitializing = true;
    private bool _isQueueRunning;

    public MainWindow()
    {
        InitializeComponent(); _downloader = new YtDlpService(_toolManager); _settings = _store.LoadSettings();
        _history = new ObservableCollection<DownloadHistoryItem>(_store.LoadHistory().OrderByDescending(x => x.DownloadedAt));
        QueueGrid.ItemsSource = _queue; HistoryGrid.ItemsSource = _history;
        var languageSetting = string.IsNullOrWhiteSpace(_settings.ManualLanguage) ? "auto" : _settings.ManualLanguage;
        LocalizationManager.SetLanguage(languageSetting);
        SelectComboByTag(LanguageComboBox, languageSetting);
        // Apply the requested new default once, including installations with the old Premiere default.
        if (!_settings.StandardMp4DefaultApplied)
        {
            _settings.OutputFormat = OutputFormats.Mp4;
            _settings.StandardMp4DefaultApplied = true;
            _store.SaveSettings(_settings);
        }
        SelectComboByTag(FormatComboBox, _settings.OutputFormat);
        SelectComboByTag(EncoderComboBox, _settings.VideoEncoder is "amd" or "nvidia" or "intel" ? _settings.VideoEncoder : "cpu");
        OutputFolderTextBox.Text = string.IsNullOrWhiteSpace(_settings.OutputFolder) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads") : _settings.OutputFolder;
        FileNameTemplateTextBox.Text = string.IsNullOrWhiteSpace(_settings.FileNameTemplate) ? "%(title).180B [%(id)s].%(ext)s" : _settings.FileNameTemplate;
        OpenAfterCheckBox.IsChecked = _settings.OpenFolderAfterComplete;
        AutoClipboardCheckBox.IsChecked = _settings.AutoClipboardEnabled;
        AutoQueueCheckBox.IsChecked = _settings.AutoQueueFromClipboard;
        AutoStartCheckBox.IsChecked = _settings.AutoStartAfterAdd;
        if (_settings.BrowserCookies is "chrome" or "edge")
        {
            _settings.BrowserCookies = "none";
            _store.SaveSettings(_settings);
        }
        SelectComboByTag(BrowserCookiesComboBox, _settings.BrowserCookies);
        _clipboardTimer.Tick += ClipboardTimer_Tick;
        if (_settings.AutoClipboardEnabled) _clipboardTimer.Start();
        ApplyLanguage();
        _isInitializing = false;
        InitializeAppUpdates();
        InitializeToolUpdates();
    }

    private async void Inspect_Click(object sender, RoutedEventArgs e)
    {
        if (_applyingUpdate || _isBusy || !ValidateUrl(out var url)) return; SetBusy(true, T("CheckingInfo")); _cts = new();
        try { await EnsureToolsAsync(false); var info = await _downloader.GetInfoAsync(url, _cts.Token,
            string.IsNullOrWhiteSpace(CookiesTextBox.Text) ? null : CookiesTextBox.Text, SelectedTag(BrowserCookiesComboBox)); ShowInfo(info); }
        catch (OperationCanceledException) { AppendLog(T("InfoCancelled")); } catch (Exception ex) { ShowError(ex); }
        finally { SetBusy(false, T("Ready")); }
    }

    private async void AddQueue_Click(object sender, RoutedEventArgs e)
    {
        var added = AddUrlsToQueue(ExtractSupportedUrls(UrlTextBox.Text), true);
        if (added > 0 && AutoStartCheckBox.IsChecked == true)
            await StartQueueIfNeededAsync();
    }

    private int AddUrlsToQueue(IEnumerable<string> urls, bool clearInput)
    {
        var normalized = urls.Select(NormalizeUrl).Where(IsSupportedVideoUrl).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var added = 0;
        foreach (var url in normalized.Where(u => !_queue.Any(q => string.Equals(q.Url, u, StringComparison.OrdinalIgnoreCase))))
        {
            _queue.Add(new DownloadQueueItem { Url = url, Title = url, Status = T("Waiting"), Progress = 0 });
            added++;
        }
        QueueGrid.Items.Refresh();
        if (clearInput && added > 0) UrlTextBox.Clear();
        if (added > 0) StatusText.Text = string.Format(T("UrlsAdded"), added);
        else if (normalized.Count > 0) StatusText.Text = T("DuplicateUrls");
        return added;
    }

    private static IEnumerable<string> ExtractSupportedUrls(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<string>();
        return Regex.Matches(text, "https?://[^\\s<>\"']+", RegexOptions.IgnoreCase)
            .Select(m => m.Value.TrimEnd('.', ',', ';', ')', ']', '}'));
    }

    private static string NormalizeUrl(string url)
    {
        url = url.Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return url;
        if (uri.Host.Contains("youtu.be", StringComparison.OrdinalIgnoreCase)) return url;
        if (!uri.Host.Contains("youtube.com", StringComparison.OrdinalIgnoreCase)) return url;
        return url;
    }

    private static bool IsSupportedVideoUrl(string value)
    {
        if (!IsHttpUrl(value) || !Uri.TryCreate(value, UriKind.Absolute, out var uri)) return false;
        var host = uri.Host.ToLowerInvariant();
        return host == "youtu.be" || host.EndsWith(".youtu.be") || host == "youtube.com" || host.EndsWith(".youtube.com");
    }

    private async void ClipboardTimer_Tick(object? sender, EventArgs e)
    {
        if (_applyingUpdate || _isBusy || AutoClipboardCheckBox.IsChecked != true) return;
        string text;
        try
        {
            if (!Clipboard.ContainsText()) return;
            text = Clipboard.GetText().Trim();
        }
        catch { return; }

        if (string.IsNullOrWhiteSpace(text) || string.Equals(text, _lastClipboardText, StringComparison.Ordinal)) return;
        _lastClipboardText = text;

        var copiedUrls = ExtractSupportedUrls(text)
            .Where(IsSupportedVideoUrl)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (copiedUrls.Count == 0) return;

        var existingUrls = ExtractSupportedUrls(UrlTextBox.Text).Where(IsSupportedVideoUrl);
        var merged = existingUrls.Concat(copiedUrls).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        UrlTextBox.Text = string.Join(Environment.NewLine, merged);
        UrlTextBox.CaretIndex = UrlTextBox.Text.Length;
        StatusText.Text = string.Format(T("ClipboardPasted"), copiedUrls.Count);

        if (AutoQueueCheckBox.IsChecked == true)
        {
            var added = AddUrlsToQueue(copiedUrls, clearInput: true);
            if (added > 0)
            {
                StatusText.Text = string.Format(T("ClipboardQueued"), added);
                if (AutoStartCheckBox.IsChecked == true)
                    await StartQueueIfNeededAsync();
            }
        }
    }

    private void AutomationOption_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        if (AutoClipboardCheckBox.IsChecked == true)
        {
            _lastClipboardText = string.Empty;
            _clipboardTimer.Start();
        }
        else _clipboardTimer.Stop();
        SaveSettings();
    }

    private async void StartQueue_Click(object sender, RoutedEventArgs e)
    {
        await StartQueueIfNeededAsync();
    }

    private async Task StartQueueIfNeededAsync()
    {
        if (_isQueueRunning || _isBusy || _applyingUpdate) return;
        if (_queue.Count == 0 && ValidateUrl(out var single))
            _queue.Add(new DownloadQueueItem { Url = single, Title = single, Status = T("Waiting") });
        if (_queue.Count == 0) return;

        ClipSelection[] clips;
        try { clips = GetClipSelections(); }
        catch (Exception ex) { MessageBox.Show(ex.Message, LocalizationManager.Get("Section")); return; }
        _isQueueRunning = true;
        SaveSettings();
        SetBusy(true, T("PreparingDownload"));
        LogTextBox.Clear();
        _cts = new();
        try
        {
            await EnsureToolsAsync(false);
            while (true)
            {
                var pendingItems = _queue
                    .Where(x => x.Status != T("Completed") && x.Status != T("Failed") && x.Status != T("Cancelled"))
                    .ToList();
                if (pendingItems.Count == 0) break;

                if(clips.Length>0)
                {
                    SectionQueue.Expand(_queue,pendingItems,clips,T("Waiting"));
                    pendingItems=_queue.Where(x=>x.Status!=T("Completed") && x.Status!=T("Failed") && x.Status!=T("Cancelled")).ToList();
                }
                foreach (var item in pendingItems)
                {
                    var clip=item.Clip;
                    _cts.Token.ThrowIfCancellationRequested();
                    item.Status = T("CheckingInfo");
                    DownloadProgressBar.IsIndeterminate = false; DownloadProgressBar.Value = 0;
                    QueueGrid.Items.Refresh();
                    VideoInfo info;
                    try
                    {
                        info = await _downloader.GetInfoAsync(item.Url, _cts.Token,
                            string.IsNullOrWhiteSpace(CookiesTextBox.Text) ? null : CookiesTextBox.Text, SelectedTag(BrowserCookiesComboBox));
                        item.Title = (string.IsNullOrWhiteSpace(info.Title) ? item.Url : info.Title) + (clip==null?"":" · "+clip.Description);
                    }
                    catch { info = new VideoInfo { Title = item.Url }; }

                    item.Status = T("DownloadInProgress");
                    QueueGrid.Items.Refresh();
                    bool acceptingProgress = true;
                    var progress = new Progress<DownloadProgress>(v =>
                    {
                        if (!acceptingProgress) return;
                        item.Speed = v.Speed; item.Eta = v.Eta;
                        item.Progress = v.Percent;
                        item.Status = v.Status;
                        DownloadProgressBar.IsIndeterminate = double.IsNaN(v.Percent);
                        DownloadProgressBar.Value = double.IsNaN(v.Percent) ? 0 : v.Percent;
                        StatusText.Text = $"{v.Status} {item.ProgressText} · {LocalizationManager.Get("Speed")} {v.Speed} · {T("RemainingTime")} {v.Eta}";
                    });
                    bool success = false;
                    try
                    {
                        await _downloader.DownloadAsync(item.Url, OutputFolderTextBox.Text.Trim(), SelectedHeight(), SelectedFormat(), clip==null && PlaylistCheckBox.IsChecked == true,
                            SubtitleCheckBox.IsChecked == true, AutoSubtitleCheckBox.IsChecked == true, ThumbnailCheckBox.IsChecked == true, EmbedMetadataCheckBox.IsChecked == true,
                            string.IsNullOrWhiteSpace(CookiesTextBox.Text) ? null : CookiesTextBox.Text, SelectedTag(BrowserCookiesComboBox), FileNameTemplateTextBox.Text.Trim(),
                            new Progress<string>(AppendLog), progress, _cts.Token, clip, info.Duration, SelectedTag(EncoderComboBox));
                        acceptingProgress = false;
                        DownloadProgressBar.IsIndeterminate = false; DownloadProgressBar.Value = 100;
                        success = true;
                        item.Progress = 100;
                        item.Status = T("Completed");
                    }
                    catch (OperationCanceledException) { item.Status = T("Cancelled"); throw; }
                    catch (Exception ex) { item.Status = T("Failed"); AppendLog(ex.Message); }
                    finally
                    {
                        acceptingProgress = false;
                        item.Speed = item.Eta = "—";
                        DownloadProgressBar.IsIndeterminate = false;
                        _history.Insert(0, new DownloadHistoryItem { DownloadedAt = DateTime.Now, Title = item.Title, Url = item.Url, Format = OutputFormats.DisplayName(SelectedFormat()) + (clip == null ? "" : " · " + clip.Description), Folder = OutputFolderTextBox.Text.Trim(), Success = success });
                        _store.SaveHistory(_history);
                        QueueGrid.Items.Refresh();
                    }
                }
            }

            if (OpenAfterCheckBox.IsChecked == true) OpenFolder(OutputFolderTextBox.Text.Trim());
            var completedCount = _queue.Count(x => x.Status == T("Completed"));
            var failedCount = _queue.Count(x => x.Status == T("Failed"));
            var notificationMessage = string.Format(T("QueueCompleteNotification"), completedCount, failedCount);
            if (!WindowsNotificationService.ShowDownloadComplete(T("DownloadCompleteTitle"), notificationMessage))
                AppendLog(T("NotificationFailed"));
        }
        catch (OperationCanceledException) { AppendLog(T("UserCancelled")); }
        catch (Exception ex) { ShowError(ex); }
        finally
        {
            _isQueueRunning = false;
            SetBusy(false, T("Ready"));
        }
    }

    private void ShowInfo(VideoInfo info)
    {
        TitleText.Text = string.IsNullOrWhiteSpace(info.Title) ? T("NoTitle") : info.Title;
        var duration = info.Duration.HasValue ? TimeSpan.FromSeconds(info.Duration.Value).ToString(@"hh\:mm\:ss") : T("Unknown");
        MetaText.Text = $"{T("Channel")}: {info.Uploader}    {T("Duration")}: {duration}    ID: {info.Id}";
        if (!string.IsNullOrWhiteSpace(info.Thumbnail)) _ = SetThumbnailAsync(info.Thumbnail);
    }

    private void RemoveQueue_Click(object sender, RoutedEventArgs e) { if (QueueGrid.SelectedItem is DownloadQueueItem i) _queue.Remove(i); }
    private void ClearQueue_Click(object sender, RoutedEventArgs e) => _queue.Clear();
    private void Redownload_Click(object sender, RoutedEventArgs e) { if (HistoryGrid.SelectedItem is DownloadHistoryItem h) { _queue.Add(new DownloadQueueItem { Url = h.Url, Title = h.Title, Status = T("Waiting") }); DownloadTab.IsSelected = true; } }
    private void OpenHistoryFolder_Click(object sender, RoutedEventArgs e) { if (HistoryGrid.SelectedItem is DownloadHistoryItem h) OpenFolder(h.Folder); }
    private void ClearHistory_Click(object sender, RoutedEventArgs e) { _history.Clear(); _store.SaveHistory(_history); }

    private async void UpdateTools_Click(object sender, RoutedEventArgs e) { if (_isBusy || _applyingUpdate) return; SetBusy(true, T("UpdatingTools")); _cts = CancellationTokenSource.CreateLinkedTokenSource(_updateLifetime.Token); try { await EnsureToolsAsync(true); _settings.LastToolUpdateCheckUtc = DateTime.UtcNow; _store.SaveSettings(_settings); MessageBox.Show(T("ToolsUpdated")); } catch (Exception ex) { ShowError(ex); } finally { SetBusy(false, T("Ready")); } }
    private async Task EnsureToolsAsync(bool force) => await _toolManager.EnsureToolsAsync(new Progress<string>(m => { StatusText.Text = m; AppendLog(m); }), force, _cts?.Token ?? _updateLifetime.Token);
    private async Task SetThumbnailAsync(string url) { try { var bytes = await ImageClient.GetByteArrayAsync(url); using var ms = new MemoryStream(bytes); var b = new BitmapImage(); b.BeginInit(); b.CacheOption = BitmapCacheOption.OnLoad; b.StreamSource = ms; b.EndInit(); b.Freeze(); ThumbnailImage.Source = b; } catch { ThumbnailImage.Source = null; } }

    private bool ValidateUrl(out string url) { url = ExtractSupportedUrls(UrlTextBox.Text).FirstOrDefault(IsSupportedVideoUrl) ?? string.Empty; if (string.IsNullOrWhiteSpace(url)) { MessageBox.Show(T("InvalidUrl"), T("UrlCheck"), MessageBoxButton.OK, MessageBoxImage.Warning); return false; } return true; }
    private static bool IsHttpUrl(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == "http" || uri.Scheme == "https");
    private int SelectedHeight() => int.Parse(((ComboBoxItem)QualityComboBox.SelectedItem).Tag?.ToString() ?? "2160");
    private string SelectedFormat() => SelectedTag(FormatComboBox);
    private static string SelectedTag(ComboBox combo) => (combo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "none";
    private static void SelectComboByTag(ComboBox combo, string? tag) { foreach (var x in combo.Items.OfType<ComboBoxItem>()) if (x.Tag?.ToString() == tag) { combo.SelectedItem = x; return; } combo.SelectedIndex = 0; }

    private void SetBusy(bool busy, string status) { _isBusy = busy; RefreshClipControls(); FormatComboBox.IsEnabled = !busy; InspectButton.IsEnabled = !busy; UpdateToolsButton.IsEnabled = !busy; StartQueueButton.IsEnabled = !busy; AddQueueButton.IsEnabled = !busy; CancelButton.IsEnabled = busy; StatusText.Text = status; }
    private void AppendLog(string message) => Dispatcher.Invoke(() => { LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}"); LogTextBox.ScrollToEnd(); });
    private void ShowError(Exception ex) { StatusText.Text = T("ErrorOccurred"); AppendLog(ex.Message); MessageBox.Show(ex.Message + Environment.NewLine + T("ErrorHelp"), T("ErrorPrefix"), MessageBoxButton.OK, MessageBoxImage.Error); }
    private void Cancel_Click(object sender, RoutedEventArgs e) { _cts?.Cancel(); _downloader.Cancel(); StatusText.Text = T("Cancelling"); }
    private void BrowseFolder_Click(object sender, RoutedEventArgs e) { var d = new OpenFolderDialog { Title = T("FolderDialogTitle"), InitialDirectory = OutputFolderTextBox.Text, Multiselect = false }; if (d.ShowDialog() == true) OutputFolderTextBox.Text = d.FolderName; }
    private void BrowseCookies_Click(object sender, RoutedEventArgs e) { var d = new OpenFileDialog { Title = T("CookiesDialogTitle"), Filter = T("CookiesFilter") }; if (d.ShowDialog() == true) CookiesTextBox.Text = d.FileName; }
    private void Paste_Click(object sender, RoutedEventArgs e)
    {
        if (!Clipboard.ContainsText()) return;
        var text = Clipboard.GetText().Trim();
        var urls = ExtractSupportedUrls(text).Where(IsSupportedVideoUrl).ToList();
        UrlTextBox.Text = urls.Count > 0 ? string.Join(Environment.NewLine, urls) : text;
        UrlTextBox.CaretIndex = UrlTextBox.Text.Length;
    }
    private void OpenFolder_Click(object sender, RoutedEventArgs e) => OpenFolder(OutputFolderTextBox.Text.Trim());
    private static void OpenFolder(string folder) { if (string.IsNullOrWhiteSpace(folder)) return; Directory.CreateDirectory(folder); Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true }); }
    private void ClearLog_Click(object sender, RoutedEventArgs e) => LogTextBox.Clear();

    private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        var lang = SelectedTag(LanguageComboBox);
        LocalizationManager.SetLanguage(lang);
        _settings.ManualLanguage = lang == "auto" ? null : lang;
        ApplyLanguage();
        SaveSettings();
    }
    private void SaveSettings() { _settings.VideoEncoder=SelectedTag(EncoderComboBox); _settings.OutputFormat = SelectedFormat(); _settings.OutputFolder = OutputFolderTextBox.Text.Trim(); _settings.FileNameTemplate = FileNameTemplateTextBox.Text.Trim(); _settings.BrowserCookies = SelectedTag(BrowserCookiesComboBox); _settings.OpenFolderAfterComplete = OpenAfterCheckBox.IsChecked == true; _settings.AutoClipboardEnabled = AutoClipboardCheckBox.IsChecked == true; _settings.AutoQueueFromClipboard = AutoQueueCheckBox.IsChecked == true; _settings.AutoStartAfterAdd = AutoStartCheckBox.IsChecked == true; _store.SaveSettings(_settings); }
    private static string T(string key) => LocalizationManager.Get(key);
    private void ApplyLanguage()
    {
        ApplyUpdateLanguage(); ApplyClipLanguage();
        StandardMp4Item.Content = LocalizationManager.Get("StandardMP4");
        PremiereMp4Item.Content = LocalizationManager.Get("PremiereMP4");
        FormatComboBox.ToolTip = LocalizationManager.Get("PremiereMP4");
        Title=T("WindowTitle") + " v" + AppUpdateService.CurrentVersion.ToString(3); AppTitleText.Text=T("AppTitle"); CopyrightNoticeText.Text=T("CopyrightNotice"); LanguageLabel.Text=T("Language");
        DownloadTab.Header=T("DownloadTab"); HistoryTab.Header=T("HistoryTab"); VideoUrlLabel.Text=T("VideoUrl"); UrlTextBox.ToolTip=T("UrlInputHint"); PasteButton.Content=T("Paste"); AddQueueButton.Content=T("AddQueue"); InspectButton.Content=T("Inspect"); AutoClipboardCheckBox.Content=T("AutoClipboard"); AutoQueueCheckBox.Content=T("AutoQueueClipboard"); AutoStartCheckBox.Content=T("AutoStartAfterAdd");
        SaveFolderLabel.Text=T("SaveFolder"); BrowseFolderButton.Content=T("Browse"); QualityLabel.Text=T("Quality"); OutputFormatLabel.Text=T("OutputFormat"); Best4KItem.Content=T("Best4K"); Quality4KItem.Content=T("Quality4K"); QualityQHDItem.Content=T("QualityQHD"); QualityFHDItem.Content=T("QualityFHD"); QualityHDItem.Content=T("QualityHD"); QualitySDItem.Content=T("QualitySD");
        PlaylistCheckBox.Content=T("Playlist"); SubtitleCheckBox.Content=T("Subtitles"); AutoSubtitleCheckBox.Content=T("AutoSubtitles"); ThumbnailCheckBox.Content=T("Thumbnail"); EmbedMetadataCheckBox.Content=T("Metadata"); OpenAfterCheckBox.Content=T("OpenAfter");
        FileNameLabel.Text=T("FileName"); BrowserCookiesLabel.Text=T("BrowserCookies"); CookiesFileButton.Content=T("CookiesFile"); QueueGroup.Text=T("Queue"); LogGroup.Text=T("Log"); StartQueueButton.Content=T("StartQueue"); RemoveQueueButton.Content=T("Remove"); ClearQueueButton.Content=T("Clear"); CancelButton.Content=T("Cancel");
        QueueTitleColumn.Header=T("Title"); QueueStatusColumn.Header=T("Status"); QueueProgressColumn.Header=T("Progress"); QueueSpeedColumn.Header=LocalizationManager.Get("Speed"); QueueEtaColumn.Header=T("RemainingTime"); HistoryDateColumn.Header=T("Date"); HistoryTitleColumn.Header=T("Title"); HistoryFormatColumn.Header=T("Format"); HistorySuccessColumn.Header=T("Success");
        RedownloadButton.Content=T("Redownload"); OpenHistoryFolderButton.Content=T("OpenFolder"); ClearHistoryButton.Content=T("ClearHistory"); OpenFolderButton.Content=T("OpenFolder"); UpdateToolsButton.Content=T("UpdateTools"); ClearLogButton.Content=T("ClearLog");
        if (string.IsNullOrWhiteSpace(TitleText.Text)) TitleText.Text=T("InfoPlaceholder"); if (string.IsNullOrWhiteSpace(StatusText.Text)) StatusText.Text=T("Ready");
        foreach (var q in _queue.Where(q => string.IsNullOrWhiteSpace(q.Status) || q.Status is "Waiting" or "대기")) q.Status=T("Waiting"); QueueGrid.Items.Refresh();
    }
    protected override void OnClosed(EventArgs e) { _updateLifetime.Cancel(); _updateTimer.Stop(); _toolUpdateTimer.Stop(); if (!_applyingUpdate) _appUpdater.Cleanup(); _clipboardTimer.Stop(); SaveSettings(); _store.SaveHistory(_history); _cts?.Cancel(); _downloader.Cancel(); base.OnClosed(e); }
}
