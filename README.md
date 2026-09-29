# VideoGrabber

以 **C# / .NET 10 / WPF** 製作的繁體中文 Windows 桌面影片下載工具。將 YouTube、Facebook、Instagram、Threads 的 HTTPS 影片網址交由 yt-dlp 解析及下載。

## 直接執行

### 1.0.1 修正

修正尚未安裝引擎時無法加入網址的問題。網址現在可先加入佇列，下載頁提供「安裝引擎並繼續」按鈕與安裝進度；安裝成功後自動處理佇列，失敗或取消會保留工作。新版位於 `artifacts/VideoGrabber-1.0.1-win-x64/VideoGrabber.exe`。請先關閉舊版再開啟新版。

佇列回歸測試：`dotnet run --project VideoGrabber.Tests -c Release -- --queue-regression`，包含使用者提供的 Facebook 分享網址與未安裝引擎情境。

1. 開啟 `artifacts/VideoGrabber-win-x64/VideoGrabber.exe`，或解壓縮 `artifacts/VideoGrabber-win-x64.zip` 後執行。
2. 在「設定與引擎」按 **安裝／更新引擎**。會從官方 GitHub 下載 yt-dlp、FFmpeg、Deno，初次需網路與數百 MB 空間。
3. 貼上影片網址（每行一筆），選擇畫質、格式，按 **加入下載**。
4. 選取已完成工作並按 **開啟位置**。

目標：Windows 10/11 x64。自包含發行版不需另裝 .NET；尚未進行 Windows 10 實機測試。程式未簽署。

## 功能

- 批次加入、進行中網址去重、序列下載。
- 最高可用／4K／1080p／720p／480p 畫質上限。
- MP4（優先 H.264/AAC，必要時轉檔）、MKV（不強制重新編碼）、MP3。
- 中英文原始／自動字幕，有提供時另存 SRT。
- 進度、速度、剩餘時間、合併／轉檔狀態與錯誤詳情。
- 取消、暫停後續佇列、重試與引擎支援的 .part 續傳。
- 完整檔案不覆寫，檔名包含來源 ID。
- 自訂儲存位置、設定保存、最近 500 筆歷史；關閉時停止子程序。
- 使用已登入的瀏覽器或 Netscape Cookie 檔存取有權限的內容。
- 引擎安裝可取消；所有下載完成才替換現有版本，失敗保留舊版。
- 限定平台 HTTPS 網址；程序使用 ArgumentList，不經命令殼層，忽略外部 yt-dlp 設定。
- 單一程式執行個體，避免更新或歷史互相覆蓋。

## 平台與限制

YouTube、Facebook、Instagram 使用 yt-dlp 內建解析器；Threads 使用內嵌的 [yt-dlp-threads](https://github.com/tribixbite/yt-dlp-threads) 公開貼文擴充（固定版本，見 `VideoGrabber/Plugins/threads/SOURCE.md`）。**不代表每個網址皆可下載**。網站改版、反爬蟲、登入、地區或年齡限制都可能造成失敗。Threads 僅處理公開影片，不能保證私密貼文與 Cookie 登入下載；其頁面結構變更時需更新應用程式。其他平台可先更新引擎，必要時使用自己的登入 Cookie。不破解 DRM，請只下載擁有或已獲授權的內容。

- 使用單部影片／Reel／貼文網址，不展開整個頻道或播放清單。
- 多影片貼文可能輸出數個檔案，清單顯示最後一個。
- 畫質是上限，不會將影片升頻；來源未提供解析度時允許下載未知畫質。MP3 模式不使用畫質上限。
- 100% 表示目前串流已下載；仍可能下載下一個串流、合併或轉檔。
- MP4 是容器格式，不保證所有來源均為 H.264。
- 相同影片、資料夾及副檔名重試時不覆寫；改存另一個資料夾可保留不同畫質。
- 暫停佇列不會暫停目前影片；請選取工作並取消以停止目前下載。
- Cookie 檔路徑不持久保存；內容暫時複製到資料目錄，工作結束刪除。異常斷電可能留下 `cookies-*.txt`，可手動刪除。登入選項於每筆工作開始時套用。
- Chrome／Edge 加密或檔案鎖可能使 Cookie 讀取失敗；可使用 Firefox 或自己的 Cookie 檔。
- 沒有雲端服務、遙測或背景剪貼簿監控。僅按「貼上」時讀取剪貼簿。

官方文件：[yt-dlp](https://github.com/yt-dlp/yt-dlp)、[支援平台](https://github.com/yt-dlp/yt-dlp/blob/master/supportedsites.md)、[JavaScript runtime](https://github.com/yt-dlp/yt-dlp/wiki/EJS)。

## 原始碼開發

安裝 .NET 10 SDK，開啟 `VideoGrabber.slnx`，或執行：

```powershell
dotnet restore VideoGrabber.Tests/VideoGrabber.Tests.csproj --configfile NuGet.Config
dotnet build VideoGrabber.Tests/VideoGrabber.Tests.csproj -c Release --no-restore
dotnet run --project VideoGrabber.Tests -c Release --no-build
dotnet run --project VideoGrabber
```

使用本專案內的 SDK 時，將 `dotnet` 替換成 `.\.tools\dotnet\dotnet.exe`。

產出自包含 Windows x64 版本與 ZIP：

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build.ps1 -Publish
```

不需第三方 NuGet 套件。測試為可執行的測試專案，不依賴額外框架。

## 資料與架構

- 影片：`%USERPROFILE%\Downloads\VideoGrabber`
- 設定、歷史、引擎：`%LOCALAPPDATA%\VideoGrabber`。測試可設定 `VIDEOGRABBER_DATA_ROOT` 環境變數改用獨立目錄。
- `MainWindow`：介面、序列佇列、取消、關閉生命週期。
- `DownloadEngine`：程序參數、非同步 stdout/stderr、取消整棵程序樹。
- `ToolInstaller`：官方 GitHub releases、串流下載、上游提供 digest 時驗證 SHA-256、整組替換。
- `Storage`：JSON 暫存寫入後替換；`Models`：工作狀態、網址驗證。

視圖渲染：`dotnet run --project VideoGrabber.Tests -c Release -- --render`。
安裝整合驗證（會下載官方檔案）：`dotnet run --project VideoGrabber.Tests -c Release -- --install-engine`。
本機影片整合驗證（需先安裝引擎）：`dotnet run --project VideoGrabber.Tests -c Release -- --integration`。

本次驗證已完成 Release 編譯（0 警告／0 錯誤）、30 項核心檢查、3 個 WPF 視圖渲染、官方引擎安裝，以及本機自產影片的 MP4 下載、MP3 轉檔、HTTP 失敗與取消測試。另已用 Threads 擴充上游的公開測試貼文驗證解析器可讀取影片資訊（未下載該影片）。整合測試不存取個人瀏覽器或帳號。

測試涵蓋平台網址、惡意主機拒絕、畫質 fallback、Unicode 路徑、Cookie 優先序、音訊與字幕參數、進度及 JSON 檔名解析。四平台的登入與下載成功率需以有效網址、帳號另行驗證。

第三方授權見 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。
