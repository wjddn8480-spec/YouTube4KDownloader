using System.Collections.Generic;

namespace YouTube4KDownloader;

public static class LocalizationManager
{
    private static readonly Dictionary<string, Dictionary<string, string>> Texts = new()
    {
        ["ko"] = new Dictionary<string, string>
        {
            ["WindowTitle"] = "4K 영상 다운로더",
            ["AppTitle"] = "4K 영상 다운로더",
            ["CopyrightNotice"] = "본인이 소유하거나 다운로드 허가를 받은 영상에만 사용하세요.",
            ["Language"] = "언어",
            ["VideoUrl"] = "영상 URL",
            ["Paste"] = "붙여넣기",
            ["Inspect"] = "정보 확인",
            ["InfoPlaceholder"] = "영상 정보를 확인하면 제목이 표시됩니다.",
            ["SaveFolder"] = "저장 폴더",
            ["Browse"] = "찾아보기",
            ["Quality"] = "화질",
            ["OutputFormat"] = "출력 형식",
            ["Best4K"] = "최고 화질 (최대 4K)",
            ["Quality4K"] = "4K 2160p",
            ["QualityQHD"] = "QHD 1440p",
            ["QualityFHD"] = "FHD 1080p",
            ["QualityHD"] = "HD 720p",
            ["QualitySD"] = "SD 480p",
            ["Playlist"] = "재생목록 전체 다운로드",
            ["Subtitles"] = "자막 다운로드",
            ["AutoSubtitles"] = "자동 생성 자막 포함",
            ["Thumbnail"] = "썸네일 저장",
            ["Metadata"] = "메타데이터 삽입",
            ["Select"] = "선택",
            ["Clear"] = "해제",
            ["Ready"] = "준비됨",
            ["DownloadStart"] = "다운로드 시작",
            ["Cancel"] = "취소",
            ["OpenFolder"] = "저장 폴더 열기",
            ["UpdateTools"] = "도구 업데이트",
            ["ClearLog"] = "로그 지우기",
            ["Channel"] = "채널",
            ["Duration"] = "길이",
            ["Unknown"] = "알 수 없음",
            ["NoTitle"] = "제목 없음",
            ["CheckingInfo"] = "영상 정보 확인 중...",
            ["InfoCancelled"] = "정보 확인이 취소되었습니다.",
            ["PreparingDownload"] = "다운로드 준비 중...",
            ["DownloadComplete"] = "영상 다운로드가 완료되었습니다.",
            ["Completed"] = "완료",
            ["UserCancelled"] = "사용자가 다운로드를 취소했습니다.",
            ["Cancelled"] = "취소됨",
            ["UpdatingTools"] = "도구 업데이트 중...",
            ["ToolsUpdated"] = "yt-dlp와 FFmpeg 업데이트가 완료되었습니다.",
            ["InvalidUrl"] = "올바른 영상 URL을 입력해 주세요.",
            ["UrlCheck"] = "URL 확인",
            ["SelectFolderWarning"] = "저장 폴더를 선택해 주세요.",
            ["Confirm"] = "확인",
            ["ErrorOccurred"] = "오류 발생",
            ["ErrorPrefix"] = "오류",
            ["ErrorHelp"] = "로그 창의 메시지를 확인하고 필요하면 '도구 업데이트'를 실행해 주세요.",
            ["Cancelling"] = "취소 중...",
            ["FolderDialogTitle"] = "영상 저장 폴더를 선택하세요.",
            ["CookiesDialogTitle"] = "Netscape 형식 cookies.txt 선택",
            ["CookiesFilter"] = "Cookies 파일 (*.txt)|*.txt|모든 파일 (*.*)|*.*",
            ["DownloadInProgress"] = "다운로드 중",
            ["RemainingTime"] = "남은 시간",
            ["ToolsReady"] = "필수 도구 준비 완료",
            ["DownloadingYtDlp"] = "yt-dlp 다운로드 중...",
            ["DownloadingFfmpeg"] = "FFmpeg 다운로드 중... 파일 크기가 커서 시간이 걸릴 수 있습니다.",
            ["LanguageChanged"] = "언어가 한국어로 변경되었습니다."
        },
        ["en"] = new Dictionary<string, string>
        {
            ["WindowTitle"] = "4K Video Downloader",
            ["AppTitle"] = "4K Video Downloader",
            ["CopyrightNotice"] = "Use this only for videos you own or have permission to download.",
            ["Language"] = "Language",
            ["VideoUrl"] = "Video URL",
            ["Paste"] = "Paste",
            ["Inspect"] = "Check info",
            ["InfoPlaceholder"] = "Video title will appear after checking the information.",
            ["SaveFolder"] = "Save folder",
            ["Browse"] = "Browse",
            ["Quality"] = "Quality",
            ["OutputFormat"] = "Output format",
            ["Best4K"] = "Best quality (up to 4K)",
            ["Quality4K"] = "4K 2160p",
            ["QualityQHD"] = "QHD 1440p",
            ["QualityFHD"] = "FHD 1080p",
            ["QualityHD"] = "HD 720p",
            ["QualitySD"] = "SD 480p",
            ["Playlist"] = "Download entire playlist",
            ["Subtitles"] = "Download subtitles",
            ["AutoSubtitles"] = "Include auto-generated subtitles",
            ["Thumbnail"] = "Save thumbnail",
            ["Metadata"] = "Embed metadata",
            ["Select"] = "Select",
            ["Clear"] = "Clear",
            ["Ready"] = "Ready",
            ["DownloadStart"] = "Start download",
            ["Cancel"] = "Cancel",
            ["OpenFolder"] = "Open save folder",
            ["UpdateTools"] = "Update tools",
            ["ClearLog"] = "Clear log",
            ["Channel"] = "Channel",
            ["Duration"] = "Duration",
            ["Unknown"] = "Unknown",
            ["NoTitle"] = "No title",
            ["CheckingInfo"] = "Checking video information...",
            ["InfoCancelled"] = "Video information check was cancelled.",
            ["PreparingDownload"] = "Preparing download...",
            ["DownloadComplete"] = "Video download completed.",
            ["Completed"] = "Completed",
            ["UserCancelled"] = "Download cancelled by user.",
            ["Cancelled"] = "Cancelled",
            ["UpdatingTools"] = "Updating tools...",
            ["ToolsUpdated"] = "yt-dlp and FFmpeg were updated successfully.",
            ["InvalidUrl"] = "Enter a valid video URL.",
            ["UrlCheck"] = "URL check",
            ["SelectFolderWarning"] = "Select a save folder.",
            ["Confirm"] = "Confirm",
            ["ErrorOccurred"] = "An error occurred",
            ["ErrorPrefix"] = "Error",
            ["ErrorHelp"] = "Check the log and run 'Update tools' if necessary.",
            ["Cancelling"] = "Cancelling...",
            ["FolderDialogTitle"] = "Select a folder for downloaded videos.",
            ["CookiesDialogTitle"] = "Select a Netscape-format cookies.txt file",
            ["CookiesFilter"] = "Cookies files (*.txt)|*.txt|All files (*.*)|*.*",
            ["DownloadInProgress"] = "Downloading",
            ["RemainingTime"] = "Remaining",
            ["ToolsReady"] = "Required tools are ready",
            ["DownloadingYtDlp"] = "Downloading yt-dlp...",
            ["DownloadingFfmpeg"] = "Downloading FFmpeg... This may take a while.",
            ["LanguageChanged"] = "Language changed to English."
        }
    };

    public static string CurrentLanguage { get; private set; } = "ko";

    public static void SetLanguage(string language)
    {
        CurrentLanguage = Texts.ContainsKey(language) ? language : "ko";
    }

    public static string Get(string key)
    {
        if (Texts.TryGetValue(CurrentLanguage, out var language) &&
            language.TryGetValue(key, out var value))
            return value;

        return key;
    }
}
