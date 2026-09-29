# 第三方元件

下載引擎由使用者按下「安裝／更新引擎」後另外下載，不包含在原始碼或應用程式發行包中。

| 元件 | 用途 | 官方來源與授權 |
| --- | --- | --- |
| .NET / WPF | Windows 桌面執行環境 | https://github.com/dotnet/wpf （MIT；另含第三方元件） |
| yt-dlp | 網站解析及下載 | https://github.com/yt-dlp/yt-dlp （原始碼 Unlicense；執行檔依上游說明含其他授權元件） |
| FFmpeg | 合併與轉檔 | https://github.com/yt-dlp/FFmpeg-Builds （選用 GPL build，授權與原始碼見發行頁） |
| Deno | JavaScript runtime | https://github.com/denoland/deno （MIT；另含第三方元件） |
| yt-dlp-threads | 內嵌的 Threads 公開貼文解析器 | https://github.com/tribixbite/yt-dlp-threads （Unlicense；固定版本與本機修改見 `VideoGrabber/Plugins/threads/SOURCE.md`，授權內嵌並在使用時解壓） |

若另行散布這些元件，請遵守上游授權的聲明、原始碼與其他要求。
