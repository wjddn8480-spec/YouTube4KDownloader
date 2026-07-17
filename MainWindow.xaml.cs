using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
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
    private CancellationTokenSource? _cts;
    private static readonly HttpClient ImageClient = new();

    public MainWindow()
    {
        InitializeComponent();
        _downloader = new YtDlpService(_toolManager);

        var systemLanguage = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        var startupLanguage = string.Equals(systemLanguage, "ko", StringComparison.OrdinalIgnoreCase)
            ? "ko"
            : "en";

        LocalizationManager.SetLanguage(startupLanguage);
        LanguageComboBox.SelectedIndex = startupLanguage == "ko" ? 0 : 1;
        ApplyLanguage();

        OutputFolderTextBox.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");
    }

    private async void Inspect_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateUrl(out var url))
            return;

        SetBusy(true, T("CheckingInfo"));
        _cts = new CancellationTokenSource();

        try
        {
            await EnsureToolsAsync(false);
            var info = await _downloader.GetInfoAsync(url, _cts.Token);

            TitleText.Text = string.IsNullOrWhiteSpace(info.Title) ? T("NoTitle") : info.Title;
            var duration = info.Duration.HasValue
                ? TimeSpan.FromSeconds(info.Duration.Value).ToString(@"hh\:mm\:ss")
                : T("Unknown");
            MetaText.Text = $"{T("Channel")}: {info.Uploader}    {T("Duration")}: {duration}    ID: {info.Id}";

            if (!string.IsNullOrWhiteSpace(info.Thumbnail))
                await SetThumbnailAsync(info.Thumbnail);
        }
        catch (OperationCanceledException)
        {
            AppendLog(T("InfoCancelled"));
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            SetBusy(false, T("Ready"));
        }
    }

    private async void Download_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateUrl(out var url))
            return;

        var outputDirectory = OutputFolderTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            MessageBox.Show(T("SelectFolderWarning"), T("Confirm"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Directory.CreateDirectory(outputDirectory);

        var qualityItem = (ComboBoxItem)QualityComboBox.SelectedItem;
        var formatItem = (ComboBoxItem)FormatComboBox.SelectedItem;
        var maxHeight = int.Parse(qualityItem.Tag?.ToString() ?? "2160");
        var outputFormat = formatItem.Tag?.ToString() ?? "mp4";

        SetBusy(true, T("PreparingDownload"));
        DownloadProgressBar.Value = 0;
        LogTextBox.Clear();
        _cts = new CancellationTokenSource();

        try
        {
            await EnsureToolsAsync(false);

            var logProgress = new Progress<string>(AppendLog);
            var downloadProgress = new Progress<(double Percent, string Status)>(value =>
            {
                DownloadProgressBar.Value = Math.Clamp(value.Percent, 0, 100);
                StatusText.Text = value.Status;
            });

            await _downloader.DownloadAsync(
                url,
                outputDirectory,
                maxHeight,
                outputFormat,
                PlaylistCheckBox.IsChecked == true,
                SubtitleCheckBox.IsChecked == true,
                AutoSubtitleCheckBox.IsChecked == true,
                ThumbnailCheckBox.IsChecked == true,
                EmbedMetadataCheckBox.IsChecked == true,
                string.IsNullOrWhiteSpace(CookiesTextBox.Text) ? null : CookiesTextBox.Text,
                logProgress,
                downloadProgress,
                _cts.Token);

            MessageBox.Show(T("DownloadComplete"), T("Completed"),
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (OperationCanceledException)
        {
            AppendLog(T("UserCancelled"));
            StatusText.Text = T("Cancelled");
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            SetBusy(false, StatusText.Text == T("Cancelled") ? T("Cancelled") : T("Ready"));
        }
    }

    private async void UpdateTools_Click(object sender, RoutedEventArgs e)
    {
        SetBusy(true, T("UpdatingTools"));
        try
        {
            await EnsureToolsAsync(true);
            MessageBox.Show(T("ToolsUpdated"), T("Completed"),
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            SetBusy(false, T("Ready"));
        }
    }

    private async Task EnsureToolsAsync(bool force)
    {
        var progress = new Progress<string>(message =>
        {
            StatusText.Text = message;
            AppendLog(message);
        });
        await _toolManager.EnsureToolsAsync(progress, force);
    }

    private async Task SetThumbnailAsync(string url)
    {
        try
        {
            var bytes = await ImageClient.GetByteArrayAsync(url);
            using var memory = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = memory;
            bitmap.EndInit();
            bitmap.Freeze();
            ThumbnailImage.Source = bitmap;
        }
        catch
        {
            ThumbnailImage.Source = null;
        }
    }

    private bool ValidateUrl(out string url)
    {
        url = UrlTextBox.Text.Trim();

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            MessageBox.Show(T("InvalidUrl"), T("UrlCheck"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        return true;
    }

    private void SetBusy(bool busy, string status)
    {
        DownloadButton.IsEnabled = !busy;
        CancelButton.IsEnabled = busy;
        StatusText.Text = status;
    }

    private void AppendLog(string message)
    {
        Dispatcher.Invoke(() =>
        {
            LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            LogTextBox.ScrollToEnd();
        });
    }

    private void ShowError(Exception ex)
    {
        StatusText.Text = T("ErrorOccurred");
        AppendLog(T("ErrorPrefix") + ": " + ex.Message);
        MessageBox.Show(
            ex.Message + Environment.NewLine + Environment.NewLine +
            T("ErrorHelp"),
            T("ErrorPrefix"),
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        _downloader.Cancel();
        StatusText.Text = T("Cancelling");
    }

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = T("FolderDialogTitle"),
            InitialDirectory = Directory.Exists(OutputFolderTextBox.Text)
                ? OutputFolderTextBox.Text
                : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
            OutputFolderTextBox.Text = dialog.FolderName;
    }

    private void BrowseCookies_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = T("CookiesDialogTitle"),
            Filter = T("CookiesFilter")
        };

        if (dialog.ShowDialog() == true)
            CookiesTextBox.Text = dialog.FileName;
    }

    private void ClearCookies_Click(object sender, RoutedEventArgs e) => CookiesTextBox.Clear();

    private void Paste_Click(object sender, RoutedEventArgs e)
    {
        if (Clipboard.ContainsText())
            UrlTextBox.Text = Clipboard.GetText().Trim();
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = OutputFolderTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(folder))
            return;

        Directory.CreateDirectory(folder);
        Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
    }

    private void ClearLog_Click(object sender, RoutedEventArgs e) => LogTextBox.Clear();


    private static string T(string key) => LocalizationManager.Get(key);

    private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || LanguageComboBox.SelectedItem is not ComboBoxItem item)
            return;

        LocalizationManager.SetLanguage(item.Tag?.ToString() ?? "ko");
        ApplyLanguage();
        StatusText.Text = T("LanguageChanged");
    }

    private void ApplyLanguage()
    {
        Title = T("WindowTitle");
        AppTitleText.Text = T("AppTitle");
        CopyrightNoticeText.Text = T("CopyrightNotice");
        LanguageLabel.Text = T("Language");
        VideoUrlLabel.Text = T("VideoUrl");
        PasteButton.Content = T("Paste");
        InspectButton.Content = T("Inspect");
        SaveFolderLabel.Text = T("SaveFolder");
        BrowseFolderButton.Content = T("Browse");
        QualityLabel.Text = T("Quality");
        OutputFormatLabel.Text = T("OutputFormat");
        Best4KItem.Content = T("Best4K");
        Quality4KItem.Content = T("Quality4K");
        QualityQHDItem.Content = T("QualityQHD");
        QualityFHDItem.Content = T("QualityFHD");
        QualityHDItem.Content = T("QualityHD");
        QualitySDItem.Content = T("QualitySD");
        PlaylistCheckBox.Content = T("Playlist");
        SubtitleCheckBox.Content = T("Subtitles");
        AutoSubtitleCheckBox.Content = T("AutoSubtitles");
        ThumbnailCheckBox.Content = T("Thumbnail");
        EmbedMetadataCheckBox.Content = T("Metadata");
        BrowseCookiesButton.Content = T("Select");
        ClearCookiesButton.Content = T("Clear");
        DownloadButton.Content = T("DownloadStart");
        CancelButton.Content = T("Cancel");
        OpenFolderButton.Content = T("OpenFolder");
        UpdateToolsButton.Content = T("UpdateTools");
        ClearLogButton.Content = T("ClearLog");

        if (string.IsNullOrWhiteSpace(TitleText.Text) ||
            TitleText.Text == "영상 정보를 확인하면 제목이 표시됩니다." ||
            TitleText.Text == "Video title will appear after checking the information.")
        {
            TitleText.Text = T("InfoPlaceholder");
        }

        if (StatusText.Text == "준비됨" || StatusText.Text == "Ready")
            StatusText.Text = T("Ready");
    }

    protected override void OnClosed(EventArgs e)
    {
        _cts?.Cancel();
        _downloader.Cancel();
        base.OnClosed(e);
    }
}
