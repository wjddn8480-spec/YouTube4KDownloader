YouTube4KDownloader v1.3.1

Windows용 YouTube 영상 다운로더
다운로드한 영상을 Adobe Premiere Pro에서 바로 편집하기 좋은 H.264 + AAC MP4 형식으로 자동 변환합니다.






소개

YouTube4KDownloader는 Windows에서 사용할 수 있는 YouTube 영상 다운로드 프로그램입니다.

원하는 해상도로 영상을 다운로드한 뒤 FFmpeg를 이용해 자동으로 Premiere Pro 호환 MP4(H.264/AVC + AAC) 형식으로 변환합니다.

YouTube에서 VP9 또는 AV1 형식으로 제공되는 영상도 최종적으로 H.264 MP4로 변환되기 때문에, 별도의 변환 작업 없이 Adobe Premiere Pro에서 바로 불러와 편집할 수 있습니다.

주요 기능

YouTube 영상 다운로드

여러 URL 대기열 등록

대기열 순차 자동 다운로드

다운로드 진행률 표시

다운로드 기록 확인

한국어 / 영어 UI

Windows 표시 언어 자동 감지

클립보드 URL 자동 입력

MP4 / MP3 / M4A 지원

480p / 720p / 1080p / 1440p / 2160p(4K) 지원

60fps 영상 지원

VP9 / AV1 → H.264 자동 변환

Premiere Pro 호환 MP4 자동 인코딩

yt-dlp 기반 다운로드

FFmpeg 기반 병합 및 인코딩

Premiere Pro Ready

영상 다운로드가 완료되면 다음 형식으로 자동 인코딩됩니다.

항목

설정

컨테이너

MP4

영상 코덱

H.264 / AVC

인코더

libx264

픽셀 포맷

yuv420p

프로파일

High

화질

CRF 18

오디오 코덱

AAC

오디오 비트레이트

320 kbps

오디오 샘플레이트

48 kHz

최적화

faststart

해상도

선택한 해상도 유지

다운로드 흐름:

YouTube URL
   ↓
yt-dlp 다운로드
   ↓
영상 + 오디오 병합
   ↓
FFmpeg 자동 인코딩
   ↓
H.264 + AAC MP4
   ↓
Adobe Premiere Pro

지원 형식

MP4

영상과 오디오를 다운로드한 뒤 Premiere Pro 호환 H.264 + AAC MP4로 자동 변환합니다.

MP3

오디오만 MP3 형식으로 다운로드합니다.

M4A

오디오만 M4A 형식으로 다운로드합니다.

지원 해상도

480p

720p

1080p

1440p

2160p / 4K

가능한 경우 원본 프레임레이트를 유지하며 60fps 영상도 지원합니다.

스크린샷

프로그램 스크린샷을 추가하려면 저장소에 이미지를 업로드한 뒤 아래 형식으로 추가하세요.

![YouTube4KDownloader](docs/screenshot.png)

사용 방법

프로그램을 실행합니다.

YouTube URL을 입력합니다.

원하는 해상도와 형식을 선택합니다.

대기열 추가를 클릭합니다.

여러 영상을 다운로드하려면 URL을 계속 추가합니다.

다운로드를 시작합니다.

영상 다운로드 완료 후 Premiere Pro 호환 인코딩이 자동으로 진행됩니다.

생성된 MP4 파일을 Premiere Pro에서 불러옵니다.

로그에는 다음과 비슷한 메시지가 표시됩니다.

Premiere Pro 호환 H.264/AAC 인코딩 중...
Premiere Pro 호환 파일 생성 완료

요구 사항

실행

Windows 10 / 11 64-bit

인터넷 연결

yt-dlp

FFmpeg

소스 빌드

.NET 8 SDK

Visual Studio 2022 또는 dotnet CLI

Windows 10 / 11

빌드 방법

저장소를 클론합니다.

git clone https://github.com/YOUR_USERNAME/YouTube4KDownloader.git
cd YouTube4KDownloader

패키지를 복원하고 빌드합니다.

dotnet restore
dotnet build -c Release

Windows x64 self-contained 버전을 만들려면:

dotnet publish -c Release -r win-x64 --self-contained true

프로젝트에 build_release.bat가 포함되어 있다면 해당 파일을 실행할 수도 있습니다.

폴더 구조 예시

YouTube4KDownloader/
├─ YouTube4KDownloader.csproj
├─ MainWindow.xaml
├─ MainWindow.xaml.cs
├─ YtDlpService.cs
├─ Models/
├─ Services/
├─ Resources/
├─ yt-dlp.exe
├─ ffmpeg.exe
├─ build_release.bat
├─ README.md
└─ README_EN.md

업데이트

YouTube 변경으로 다운로드가 실패하는 경우 yt-dlp.exe를 최신 버전으로 업데이트한 뒤 다시 시도해 주세요.

공식 yt-dlp 프로젝트:

https://github.com/yt-dlp/yt-dlp

FFmpeg:

https://ffmpeg.org/

알려진 사항

4K / 60fps 인코딩 시간

4K 또는 60fps 영상은 H.264로 다시 인코딩하기 때문에 다운로드 후 추가 시간이 필요할 수 있습니다.

CPU 사용률

libx264 소프트웨어 인코딩을 사용하므로 인코딩 중 CPU 사용률이 높아질 수 있습니다.

파일 크기

CRF 18은 화질을 우선하는 설정이므로 원본에 따라 최종 파일 크기가 커질 수 있습니다.

문제 제보

버그나 기능 요청은 GitHub Issues를 이용해 주세요.

Issue 작성 시 다음 내용을 함께 적어주시면 문제 확인에 도움이 됩니다.

프로그램 버전

Windows 버전

오류 메시지

재현 방법

관련 로그

기여

Pull Request와 기능 개선 제안을 환영합니다.

저장소를 Fork 합니다.

새로운 브랜치를 만듭니다.

변경 사항을 커밋합니다.

Fork 저장소에 Push 합니다.

Pull Request를 생성합니다.

사용 및 저작권 주의

이 프로그램은 사용자가 다운로드 권한을 가진 콘텐츠를 저장하는 용도로 사용해야 합니다.

YouTube 이용약관, 콘텐츠 저작권 및 관련 법률을 준수해 주세요.

사용된 오픈소스

yt-dlp

FFmpeg

.NET

각 구성 요소는 해당 프로젝트의 라이선스를 따릅니다.

버전

YouTube4KDownloader v1.3.1 — Premiere Pro Ready Edition


YouTube4KDownloader v1.3.1

A YouTube video downloader for Windows
Automatically converts downloaded videos into Adobe Premiere Pro-friendly H.264 + AAC MP4 files.






About

YouTube4KDownloader is a Windows application for downloading YouTube videos.

After downloading a video at the selected resolution, FFmpeg automatically converts it to a Premiere Pro-friendly MP4 using H.264/AVC video and AAC audio.

Videos delivered by YouTube in VP9 or AV1 are converted to H.264 MP4, making the final files easier to import directly into Adobe Premiere Pro without a separate conversion step.

Features

Download YouTube videos

Multi-URL download queue

Automatic sequential downloads

Download progress display

Download history

Korean / English UI

Automatic Windows display-language detection

Automatic clipboard URL input

MP4 / MP3 / M4A support

480p / 720p / 1080p / 1440p / 2160p (4K)

60 FPS support

Automatic VP9 / AV1 to H.264 conversion

Automatic Premiere Pro-compatible MP4 encoding

yt-dlp-based downloading

FFmpeg-based merging and encoding

Premiere Pro Ready

Completed video downloads are automatically encoded using the following settings:

Item

Setting

Container

MP4

Video codec

H.264 / AVC

Encoder

libx264

Pixel format

yuv420p

Profile

High

Quality

CRF 18

Audio codec

AAC

Audio bitrate

320 kbps

Audio sample rate

48 kHz

Optimization

faststart

Resolution

Keeps the selected resolution

Download workflow:

YouTube URL
   ↓
Download with yt-dlp
   ↓
Merge video + audio
   ↓
Automatic FFmpeg encoding
   ↓
H.264 + AAC MP4
   ↓
Adobe Premiere Pro

Supported Formats

MP4

Downloads video and audio, then automatically converts the result to a Premiere Pro-friendly H.264 + AAC MP4 file.

MP3

Downloads audio only as MP3.

M4A

Downloads audio only as M4A.

Supported Resolutions

480p

720p

1080p

1440p

2160p / 4K

When possible, the original frame rate is preserved, including 60 FPS sources.

Screenshots

To add a screenshot, upload an image to the repository and use:

![YouTube4KDownloader](docs/screenshot.png)

How to Use

Launch the application.

Enter a YouTube URL.

Select the desired resolution and format.

Click Add to Queue.

Add more URLs if you want to download multiple videos.

Start the download.

After a video finishes downloading, Premiere Pro-compatible encoding starts automatically.

Import the completed MP4 file directly into Premiere Pro.

The log will display messages similar to:

Encoding Premiere Pro-compatible H.264/AAC...
Premiere Pro-compatible file created

Requirements

Running

Windows 10 / 11 64-bit

Internet connection

yt-dlp

FFmpeg

Building from Source

.NET 8 SDK

Visual Studio 2022 or the dotnet CLI

Windows 10 / 11

Build

Clone the repository:

git clone https://github.com/YOUR_USERNAME/YouTube4KDownloader.git
cd YouTube4KDownloader

Restore dependencies and build:

dotnet restore
dotnet build -c Release

To create a self-contained Windows x64 build:

dotnet publish -c Release -r win-x64 --self-contained true

If the project includes build_release.bat, you can use that script as well.

Example Project Structure

YouTube4KDownloader/
├─ YouTube4KDownloader.csproj
├─ MainWindow.xaml
├─ MainWindow.xaml.cs
├─ YtDlpService.cs
├─ Models/
├─ Services/
├─ Resources/
├─ yt-dlp.exe
├─ ffmpeg.exe
├─ build_release.bat
├─ README.md
└─ README_KO.md

Updates

If downloads stop working after changes on YouTube, update yt-dlp.exe and try again.

Official yt-dlp project:

https://github.com/yt-dlp/yt-dlp

FFmpeg:

https://ffmpeg.org/

Known Notes

4K / 60 FPS Encoding Time

4K and 60 FPS videos may require additional processing time because they are re-encoded to H.264 after downloading.

CPU Usage

The current Premiere Pro conversion workflow uses the libx264 software encoder, so CPU usage may be high while encoding.

File Size

CRF 18 prioritizes visual quality, so the final MP4 may be larger depending on the source video.

Bug Reports

Please use GitHub Issues for bugs and feature requests.

When reporting a problem, please include:

Application version

Windows version

Error message

Steps to reproduce

Relevant logs

Contributing

Pull requests and feature suggestions are welcome.

Fork the repository.

Create a new branch.

Commit your changes.

Push the branch to your fork.

Open a Pull Request.

Usage and Copyright Notice

Use this application only for content that you have permission to download.

Please comply with YouTube's Terms of Service, copyright rules, and applicable laws.

Open-Source Components

yt-dlp

FFmpeg

.NET

Each component is subject to its own license.

Version

YouTube4KDownloader v1.3.1 — Premiere Pro Ready Edition
