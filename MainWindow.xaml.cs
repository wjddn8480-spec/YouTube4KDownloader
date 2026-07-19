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

    public MainWindow()
    {
        InitializeComponent(); _downloader = new YtDlpService(_toolManager); _settings = _store.LoadSettings();
        _history = new ObservableCollection<DownloadHistoryItem>(_store.LoadHistory().OrderByDescending(x => x.DownloadedAt));
        QueueGrid.ItemsSource = _queue; HistoryGrid.ItemsSource = _history;
        var language = _settings.ManualLanguage ?? (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ko" ? "ko" : "en");
        LocalizationManager.SetLanguage(language); LanguageComboBox.SelectedIndex = language == "ko" ? 0 : 1;
        OutputFolderTextBox.Text = string.IsNullOrWhiteSpace(_settings.OutputFolder) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads") : _settings.OutputFolder;
        FileNameTemplateTextBox.Text = string.IsNullOrWhiteSpace(_settings.FileNameTemplate) ? "%(title).180B [%(id)s].%(ext)s" : _settings.FileNameTemplate;
        OpenAfterCheckBox.IsChecked = _settings.OpenFolderAfterComplete;
        SelectComboByTag(BrowserCookiesComboBox, _settings.BrowserCookies);
        ApplyLanguage();
    }

    private async void Inspect_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateUrl(out var url)) return; SetBusy(true, T("CheckingInfo")); _cts = new();
        try { await EnsureToolsAsync(false); var info = await _downloader.GetInfoAsync(url, _cts.Token); ShowInfo(info); }
        catch (OperationCanceledException) { AppendLog(T("InfoCancelled")); } catch (Exception ex) { ShowError(ex); }
        finally { SetBusy(false, T("Ready")); }
    }

    private void AddQueue_Click(object sender, RoutedEventArgs e)
    {
        var urls = UrlTextBox.Text.Split(new[] { '\r', '\n', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var url in urls.Where(IsHttpUrl).Distinct().Where(u => !_queue.Any(q => q.Url == u)))
            _queue.Add(new DownloadQueueItem { Url = url, Title = url, Status = T("Waiting"), Progress = 0 });
        QueueGrid.Items.Refresh(); UrlTextBox.Clear();
    }

    private async void StartQueue_Click(object sender, RoutedEventArgs e)
    {
        if (_queue.Count == 0 && ValidateUrl(out var single)) _queue.Add(new DownloadQueueItem { Url = single, Title = single, Status = T("Waiting") });
        if (_queue.Count == 0) return;
        SaveSettings(); SetBusy(true, T("PreparingDownload")); LogTextBox.Clear(); _cts = new();
        try
        {
            await EnsureToolsAsync(false);
            foreach (var item in _queue.Where(x => x.Status != T("Completed")).ToList())
            {
                _cts.Token.ThrowIfCancellationRequested(); item.Status = T("CheckingInfo"); QueueGrid.Items.Refresh();
                VideoInfo info;
                try { info = await _downloader.GetInfoAsync(item.Url, _cts.Token); item.Title = string.IsNullOrWhiteSpace(info.Title) ? item.Url : info.Title; }
                catch { info = new VideoInfo { Title = item.Url }; }
                item.Status = T("DownloadInProgress"); QueueGrid.Items.Refresh();
                var progress = new Progress<(double Percent, string Status)>(v => { item.Progress = v.Percent; item.Status = v.Status; DownloadProgressBar.Value = v.Percent; StatusText.Text = v.Status; QueueGrid.Items.Refresh(); });
                bool success = false;
                try
                {
                    await _downloader.DownloadAsync(item.Url, OutputFolderTextBox.Text.Trim(), SelectedHeight(), SelectedFormat(), PlaylistCheckBox.IsChecked == true,
                        SubtitleCheckBox.IsChecked == true, AutoSubtitleCheckBox.IsChecked == true, ThumbnailCheckBox.IsChecked == true, EmbedMetadataCheckBox.IsChecked == true,
                        string.IsNullOrWhiteSpace(CookiesTextBox.Text) ? null : CookiesTextBox.Text, SelectedTag(BrowserCookiesComboBox), FileNameTemplateTextBox.Text.Trim(),
                        new Progress<string>(AppendLog), progress, _cts.Token);
                    success = true; item.Progress = 100; item.Status = T("Completed");
                }
                catch (OperationCanceledException) { item.Status = T("Cancelled"); throw; }
                catch (Exception ex) { item.Status = T("Failed"); AppendLog(ex.Message); }
                finally
                {
                    _history.Insert(0, new DownloadHistoryItem { DownloadedAt = DateTime.Now, Title = item.Title, Url = item.Url, Format = SelectedFormat().ToUpperInvariant(), Folder = OutputFolderTextBox.Text.Trim(), Success = success });
                    _store.SaveHistory(_history); QueueGrid.Items.Refresh();
                }
            }
            if (OpenAfterCheckBox.IsChecked == true) OpenFolder(OutputFolderTextBox.Text.Trim());
            MessageBox.Show(T("QueueComplete"), T("Completed"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (OperationCanceledException) { AppendLog(T("UserCancelled")); }
        catch (Exception ex) { ShowError(ex); }
        finally { SetBusy(false, T("Ready")); }
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

    private async void UpdateTools_Click(object sender, RoutedEventArgs e) { SetBusy(true, T("UpdatingTools")); try { await EnsureToolsAsync(true); MessageBox.Show(T("ToolsUpdated")); } catch (Exception ex) { ShowError(ex); } finally { SetBusy(false, T("Ready")); } }
    private async Task EnsureToolsAsync(bool force) => await _toolManager.EnsureToolsAsync(new Progress<string>(m => { StatusText.Text = m; AppendLog(m); }), force);
    private async Task SetThumbnailAsync(string url) { try { var bytes = await ImageClient.GetByteArrayAsync(url); using var ms = new MemoryStream(bytes); var b = new BitmapImage(); b.BeginInit(); b.CacheOption = BitmapCacheOption.OnLoad; b.StreamSource = ms; b.EndInit(); b.Freeze(); ThumbnailImage.Source = b; } catch { ThumbnailImage.Source = null; } }

    private bool ValidateUrl(out string url) { url = UrlTextBox.Text.Trim(); if (!IsHttpUrl(url)) { MessageBox.Show(T("InvalidUrl"), T("UrlCheck"), MessageBoxButton.OK, MessageBoxImage.Warning); return false; } return true; }
    private static bool IsHttpUrl(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == "http" || uri.Scheme == "https");
    private int SelectedHeight() => int.Parse(((ComboBoxItem)QualityComboBox.SelectedItem).Tag?.ToString() ?? "2160");
    private string SelectedFormat() => SelectedTag(FormatComboBox);
    private static string SelectedTag(ComboBox combo) => (combo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "none";
    private static void SelectComboByTag(ComboBox combo, string? tag) { foreach (var x in combo.Items.OfType<ComboBoxItem>()) if (x.Tag?.ToString() == tag) { combo.SelectedItem = x; return; } combo.SelectedIndex = 0; }

    private void SetBusy(bool busy, string status) { StartQueueButton.IsEnabled = !busy; AddQueueButton.IsEnabled = !busy; CancelButton.IsEnabled = busy; StatusText.Text = status; }
    private void AppendLog(string message) => Dispatcher.Invoke(() => { LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}"); LogTextBox.ScrollToEnd(); });
    private void ShowError(Exception ex) { StatusText.Text = T("ErrorOccurred"); AppendLog(ex.Message); MessageBox.Show(ex.Message + Environment.NewLine + T("ErrorHelp"), T("ErrorPrefix"), MessageBoxButton.OK, MessageBoxImage.Error); }
    private void Cancel_Click(object sender, RoutedEventArgs e) { _cts?.Cancel(); _downloader.Cancel(); StatusText.Text = T("Cancelling"); }
    private void BrowseFolder_Click(object sender, RoutedEventArgs e) { var d = new OpenFolderDialog { Title = T("FolderDialogTitle"), InitialDirectory = OutputFolderTextBox.Text, Multiselect = false }; if (d.ShowDialog() == true) OutputFolderTextBox.Text = d.FolderName; }
    private void BrowseCookies_Click(object sender, RoutedEventArgs e) { var d = new OpenFileDialog { Title = T("CookiesDialogTitle"), Filter = T("CookiesFilter") }; if (d.ShowDialog() == true) CookiesTextBox.Text = d.FileName; }
    private void Paste_Click(object sender, RoutedEventArgs e) { if (Clipboard.ContainsText()) UrlTextBox.Text = Clipboard.GetText().Trim(); }
    private void OpenFolder_Click(object sender, RoutedEventArgs e) => OpenFolder(OutputFolderTextBox.Text.Trim());
    private static void OpenFolder(string folder) { if (string.IsNullOrWhiteSpace(folder)) return; Directory.CreateDirectory(folder); Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true }); }
    private void ClearLog_Click(object sender, RoutedEventArgs e) => LogTextBox.Clear();

    private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (!IsLoaded) return; var lang = SelectedTag(LanguageComboBox); LocalizationManager.SetLanguage(lang); _settings.ManualLanguage = lang; ApplyLanguage(); SaveSettings(); }
    private void SaveSettings() { _settings.OutputFolder = OutputFolderTextBox.Text.Trim(); _settings.FileNameTemplate = FileNameTemplateTextBox.Text.Trim(); _settings.BrowserCookies = SelectedTag(BrowserCookiesComboBox); _settings.OpenFolderAfterComplete = OpenAfterCheckBox.IsChecked == true; _store.SaveSettings(_settings); }
    private static string T(string key) => LocalizationManager.Get(key);
    private void ApplyLanguage()
    {
        Title=T("WindowTitle"); AppTitleText.Text=T("AppTitle"); CopyrightNoticeText.Text=T("CopyrightNotice"); LanguageLabel.Text=T("Language");
        DownloadTab.Header=T("DownloadTab"); HistoryTab.Header=T("HistoryTab"); VideoUrlLabel.Text=T("VideoUrl"); PasteButton.Content=T("Paste"); AddQueueButton.Content=T("AddQueue"); InspectButton.Content=T("Inspect");
        SaveFolderLabel.Text=T("SaveFolder"); BrowseFolderButton.Content=T("Browse"); QualityLabel.Text=T("Quality"); OutputFormatLabel.Text=T("OutputFormat"); Best4KItem.Content=T("Best4K"); Quality4KItem.Content=T("Quality4K"); QualityQHDItem.Content=T("QualityQHD"); QualityFHDItem.Content=T("QualityFHD"); QualityHDItem.Content=T("QualityHD"); QualitySDItem.Content=T("QualitySD");
        PlaylistCheckBox.Content=T("Playlist"); SubtitleCheckBox.Content=T("Subtitles"); AutoSubtitleCheckBox.Content=T("AutoSubtitles"); ThumbnailCheckBox.Content=T("Thumbnail"); EmbedMetadataCheckBox.Content=T("Metadata"); OpenAfterCheckBox.Content=T("OpenAfter");
        FileNameLabel.Text=T("FileName"); BrowserCookiesLabel.Text=T("BrowserCookies"); CookiesFileButton.Content=T("CookiesFile"); QueueGroup.Header=T("Queue"); LogGroup.Header=T("Log"); StartQueueButton.Content=T("StartQueue"); RemoveQueueButton.Content=T("Remove"); ClearQueueButton.Content=T("Clear"); CancelButton.Content=T("Cancel");
        QueueTitleColumn.Header=T("Title"); QueueStatusColumn.Header=T("Status"); QueueProgressColumn.Header=T("Progress"); HistoryDateColumn.Header=T("Date"); HistoryTitleColumn.Header=T("Title"); HistoryFormatColumn.Header=T("Format"); HistorySuccessColumn.Header=T("Success");
        RedownloadButton.Content=T("Redownload"); OpenHistoryFolderButton.Content=T("OpenFolder"); ClearHistoryButton.Content=T("ClearHistory"); OpenFolderButton.Content=T("OpenFolder"); UpdateToolsButton.Content=T("UpdateTools"); ClearLogButton.Content=T("ClearLog");
        if (string.IsNullOrWhiteSpace(TitleText.Text)) TitleText.Text=T("InfoPlaceholder"); if (string.IsNullOrWhiteSpace(StatusText.Text)) StatusText.Text=T("Ready");
        foreach (var q in _queue.Where(q => string.IsNullOrWhiteSpace(q.Status) || q.Status is "Waiting" or "대기")) q.Status=T("Waiting"); QueueGrid.Items.Refresh();
    }
    protected override void OnClosed(EventArgs e) { SaveSettings(); _store.SaveHistory(_history); _cts?.Cancel(); _downloader.Cancel(); base.OnClosed(e); }
}
