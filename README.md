# VideoGrabber

繁體中文 Windows 桌面影片下載工具，以 C# / .NET 10 / WPF 製作。

## 功能

- 支援 YouTube、Facebook、Instagram 與 Threads 公開影片網址，實際可用性依網站與存取權限而定。
- 批次加入網址、依序下載，支援取消、重試與暫停後續佇列。
- 提供最高可用、4K、1080p、720p、480p 畫質上限，以及 MP4、MKV、MP3 格式。
- 下載來源提供的中英文字幕，顯示下載進度、速度與剩餘時間。
- 自訂儲存位置、保存下載歷史，支援瀏覽器登入 Cookie 或 Cookie 檔。

## 初始設定

### 1. 安裝環境

使用 Windows 10/11 x64，安裝 Git 與 [.NET 10 SDK（Windows x64）](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)。請選擇 **SDK**，只安裝 Runtime 無法編譯。

安裝後重新開啟 PowerShell，執行 `dotnet --list-sdks`，確認列出 `10.0.xxx`。

### 2. 取得並啟動專案

將 `<儲存庫網址>` 換成此專案的 Git URL；若已 clone，直接進入專案資料夾。

```powershell
git clone <儲存庫網址> videograbber
cd videograbber
```

在專案根目錄依序執行，還原成功後再啟動：

```powershell
dotnet restore VideoGrabber/VideoGrabber.csproj --configfile NuGet.Config
dotnet run --project VideoGrabber -c Release
```

首次建置需網路，後續啟動只需執行第二行。

### 3. 安裝引擎並下載

1. 在「設定與引擎」按 **安裝／更新引擎**，等待安裝完成；初次需網路與數百 MB 空間。
2. 貼上影片網址（每行一筆），選擇畫質與格式，按 **加入下載**。
3. 完成後按 **開啟位置**。預設儲存於 `%USERPROFILE%\Downloads\VideoGrabber`。

### 4. 打包成 EXE

先關閉 VideoGrabber，在專案根目錄執行：

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build.ps1 -Publish
```

成功後產出：

- 執行檔：`artifacts\VideoGrabber-win-x64\VideoGrabber.exe`
- 壓縮檔：`artifacts\VideoGrabber-win-x64.zip`

雙擊 EXE 即可啟動。ZIP 可複製到其他 Windows x64 電腦，完整解壓縮後執行，不需另裝 .NET；首次使用仍需安裝下載引擎。首次 clone 不含這些產出檔案，修改程式後需重新打包。
