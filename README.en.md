# YouTube4KDownloader v1.3.5

[한국어](README.md) · [Downloads / GitHub Releases](https://github.com/wjddn8480-spec/YouTube4KDownloader/releases)

A Windows YouTube video and audio downloader featuring standard MP4, Premiere Pro-compatible MP4, multiple sections entered by time, and selectable GPU encoding.

> This document describes the **latest 1.3.5 release**. Section downloads use fast cutting; video preview and frame-accurate cutting are not available.

## What's New in 1.3.5

- Automatic installation and updates for the Deno JavaScript runtime used by current YouTube `n` challenges
- Automatic use of yt-dlp's official EJS challenge solver components
- Firefox login cookies now apply consistently to inspection, queue preflight, full downloads and section downloads
- Existing installations refresh yt-dlp when Deno is first installed
- Deno added to automatic tool updates

## Features

- Downloads up to 4K when available from the source
- Standard MP4, Premiere-compatible MP4, MP3 and M4A output
- Multiple-URL queue and full-playlist downloads
- Manual start/end entry and separate output for up to 100 sections
- CPU, AMD AMF, NVIDIA NVENC and Intel QSV encoder selection
- Queue progress, download speed and estimated time remaining
- ETA including merging/conversion, informed by previous processing timings
- Subtitles, automatic captions, thumbnails and embedded metadata
- Automatic clipboard URL input, queue insertion and optional automatic start
- Download history, redownload from history, completion notification and folder opening
- Automatic application and tool updates
- Members-only downloads through an authorized Firefox login
- Automatic Windows language detection and 20 selectable languages

## Requirements and Installation

- Windows 10/11 x64
- Internet access and sufficient space for downloads and conversion
- Compatible GPU and driver when using hardware encoding

The distributed executable includes the .NET runtime; a separate runtime installation is not required to run it.

1. Download `YouTube4KDownloader.exe` from [Releases](https://github.com/wjddn8480-spec/YouTube4KDownloader/releases). In the full-source ZIP, use `release-assets/YouTube4KDownloader.exe`.
2. Save it in a writable folder and launch it.
3. Select an output folder and format. Required yt-dlp, FFmpeg, FFprobe and Deno tools are downloaded during tool preparation.

**Replacing another 1.3.5 revision:** fully close the existing application and manually replace its EXE. Automatic updates target higher versions and will not apply another build with the same version.

## Basic Downloads

1. Paste video URLs into the URL field. Separate multiple URLs with spaces or new lines.
2. Choose the output folder, maximum resolution and output format.
3. Add the URLs to the queue and start downloading.
4. Check each item's progress, speed, remaining time and result in the queue.

Enable automatic start to begin after adding URLs. Clipboard automation options can be enabled or disabled separately.

## Output Formats

| Format | Processing | Intended use |
| --- | --- | --- |
| MP4 (Standard) | Downloads MP4 video and M4A audio, then merges/remuxes into MP4 without the app's separate video re-encoding step. | General playback and preserving source codecs |
| MP4 (Premiere Pro compatible) | Converts the downloaded source to H.264 video and AAC audio. | Editing in Premiere Pro |
| MP3 | Extracts and converts audio. | MP3 audio files |
| M4A | Extracts audio and converts when necessary. | M4A audio files |

**Standard MP4** is the default. The app remembers subsequent format selections. Settings that have not received the earlier default migration may switch to standard MP4 once.

Standard MP4 does not guarantee H.264: the source may contain AV1 or other codecs. Select Premiere-compatible output when editing compatibility matters. If suitable MP4 streams are unavailable at the requested resolution, standard mode may choose a lower resolution or fail. The selected resolution is a maximum; the app does not upscale beyond the source.

## Section Downloads

### Save One Section

1. Enter an individual video URL and enable **Download section**.
2. Enter start and end times.
3. If the section list is empty, starting the download saves the single range in the time fields.

| Input | Meaning |
| --- | --- |
| `90` | 90 seconds |
| `01:30` | 1 minute, 30 seconds |
| `00:01:30` | 1 minute, 30 seconds |
| `00:01:30.500` | 1 minute, 30.5 seconds |

End must be later than start. The app also checks the range against the source duration when available.

### Save Multiple Sections

1. Enter start/end times and click **+ Download section**.
2. Change the times and add more ranges.
3. Select an item to remove it, or clear the entire list.
4. Start downloading. Every section becomes a separate queue item and output file.

**An existing section list takes precedence over the current time fields.** Up to 100 sections are supported; exact duplicates are ignored. The same list applies to each URL. Success/failure and progress are tracked separately for each section.

### Scope and Limitations

- Only fast cutting is supported. Keyframes may cause slight differences at the boundaries.
- Sections work with standard MP4, Premiere-compatible MP4, MP3 and M4A. Premiere mode still converts section output to H.264/AAC.
- Full playlists, ongoing live streams and subtitle saving are unsupported in section mode; chapters are omitted.
- Output filenames include the range, processing mode and format to distinguish sections from full downloads.
- Section settings and lists reset when the application restarts.
- FFmpeg is required. Depending on the server and stream type, more data than the selected range may need to be transferred.

## GPU Encoding

Select **MP4 (Premiere Pro compatible)**, then choose an encoder.

| Selection | Encoder |
| --- | --- |
| CPU — default | `libx264` |
| AMD AMF | `h264_amf` |
| NVIDIA NVENC | `h264_nvenc` |
| Intel QSV | `h264_qsv` |

The selection is saved. It does not apply to standard MP4, MP3 or M4A. GPU encoding does not increase the network download speed itself.

Before using a GPU encoder, the app runs a short test encode to check basic availability. If unavailable, it reports an error; select CPU manually to retry. Passing this check does not guarantee success for every actual resolution or input format. Quality and output size can differ between encoders.

## Progress and ETA

- Progress, speed and estimated remaining time appear in the queue and status area.
- Downloads show transfer speed. Post-processing shows a measured multiplier such as `2.00x` when available.
- `≈` indicates an estimate. It accounts for the current transfer and pending merging/conversion, **not completion of the entire queue**.
- Progress and ETA may adjust between transfer stages. An item reaches 100% only after successful completion of processing.
- Successful processing timings are retained by output format, selected resolution and processing category, with separate GPU encoder profiles.
- Initial estimates use default assumptions; later matching jobs use saved timings. Cancelled/failed jobs and jobs without measurements are not learned.
- Sudden speed changes are smoothed. Stalls or overruns may show “Estimating” when a useful time cannot be determined.

Video complexity, actual codec/FPS, system load and storage can affect accuracy. ETA is not a guaranteed completion time.

## Subtitles, Thumbnails and Browser Login

Full-video downloads offer subtitles, automatic captions, thumbnails and metadata options. Subtitle availability depends on the source; the app targets Korean and English language variants.

For content requiring login, browser login or a `cookies.txt` file can be used within the access granted to your account. The current browser selector offers Firefox and Brave. For members-only videos, sign in to YouTube in Firefox with an account that has access, then select `Firefox` in the app. Starting with 1.3.5, those cookies apply to inspection, queue preflight, full downloads and section downloads.

If Firefox cookie extraction fails, fully close Firefox and retry. You can alternatively export and select a Netscape-format `cookies.txt` file. Cookie files may contain login information; do not share them or commit them to a repository.

## Application and Tool Updates

### Application

When enabled, automatic updates check GitHub Releases at startup and approximately every six hours while running. You can also check manually using the application update button. Installation waits for active work and the queue to finish.

### Tools

| Tool | Role |
| --- | --- |
| yt-dlp | Extracts video information and downloads media |
| FFmpeg | Merges video/audio, processes sections and converts media |
| FFprobe | Inspects media information and duration |
| Deno | Runs current YouTube JavaScript challenges |
| yt-dlp EJS | Official YouTube player challenge solver scripts |

Automatic tool updates check while idle after 24 hours have elapsed since the last successful check. Failed checks are retried after approximately one hour. The tool update button can also run a manual update. On an existing installation without Deno, the app prepares Deno and a current yt-dlp together.

## Languages

Korean, English, Japanese, Simplified Chinese, Traditional Chinese, Spanish, German, French, Portuguese, Russian, Italian, Dutch, Polish, Turkish, Ukrainian, Vietnamese, Indonesian, Thai, Arabic and Hindi — 20 languages in total.

Choose automatic Windows language detection or select a language manually. Technical logs and detailed errors from external tools or the operating system may retain their original text. Existing history descriptions retain the language used when saved.

## Settings and History

Default location: `%LOCALAPPDATA%\YouTube4KDownloader`

| File | Contents |
| --- | --- |
| `settings.json` | Output format, encoder, folder, language, automation preferences and other settings |
| `history.json` | Download history |
| `timing-history.json` | Processing measurements used for ETA estimates |

To reset learned timings, close the app and delete `timing-history.json`. The download queue and section list are not restored after restart.

## Building from Source

Install the .NET 8 SDK on Windows, then run `build_release.bat` from the source root.

```bat
build_release.bat
```

Output: `release-assets/YouTube4KDownloader.exe`

Alternatively, publish directly from the source root:

```powershell
dotnet publish .\YouTube4KDownloader\YouTube4KDownloader.csproj -c Release -r win-x64 --self-contained true -o .\release-assets
```

## Publishing a GitHub Release

| Item | Value |
| --- | --- |
| Application version | `1.3.5` |
| EXE file version | `1.3.5.0` |
| Release tag | `1.3.5` or `v1.3.5` |
| Update asset name | `YouTube4KDownloader.exe` |

Attach the published EXE to a stable release and mark it Latest. GitHub's automatically generated Source code ZIP is not an application update. The current EXE update mechanism does not require a separate update ZIP or checksum sidecar.

The release tag and embedded EXE version must match. Renaming a tag does not change the EXE version. Republishing a revised 1.3.5 build will still require users already running 1.3.5 to replace the executable manually.

## Troubleshooting

| Symptom | What to check |
| --- | --- |
| Download fails | Update tools; check URL access, logs and login settings |
| `n challenge solving failed` | Use 1.3.5 or later, then update tools to install current yt-dlp and Deno |
| `The page needs to be reloaded` | Update tools, then check the Firefox login and cookies again |
| Firefox cookie extraction fails | Fully close Firefox or use a `cookies.txt` file |
| Members-only video is unavailable | Confirm the Firefox account has membership access and Firefox is selected in Browser login |
| Requested resolution unavailable | Check the source resolution and available MP4 streams |
| Playback/editing problems in Premiere | Download again using Premiere-compatible MP4 |
| GPU encoder error | Update drivers/tools and retry, or select CPU |
| Section boundaries differ slightly | Fast cutting is subject to keyframe boundaries |
| Entered times are not used | An existing section list takes precedence; edit or clear it |
| ETA changes | Estimates depend on workload and processing stage |
| Same-version update is not offered | Close the app and replace the EXE manually |
| `EXE version does not match the release tag.` | Check that the release tag matches the attached EXE's embedded version |

## Validation Scope

Version 1.3.5 passed a Windows x64 single-file build and ZIP integrity validation. It also passed 10 tool-management checks, 65 existing functional checks, validation of all 20 language resources and 18 application-update checks. Windows UI behavior, physical AMD/NVIDIA/Intel GPU execution and a real members-only download with a Firefox account were not directly tested in this environment. See `VALIDATION.md` in the source package for details.

## Usage Terms

Use this application for content you own or have permission to download. Consult the included `LICENSE.txt` for source usage terms. External tools, including yt-dlp, FFmpeg and Deno, retain their respective licenses.
