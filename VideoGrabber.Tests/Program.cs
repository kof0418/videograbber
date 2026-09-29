using VideoGrabber;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

if (args.Contains("--integration")) { await IntegrationChecks.Run(); return; }
if (args.Contains("--queue-regression")) { QueueRegression.Run(); return; }
if (args.Contains("--close-regression")) { CloseRegression.Run(); return; }

if (args.Contains("--install-engine"))
{
    await ToolInstaller.InstallAsync(new Progress<string>(Console.WriteLine), CancellationToken.None);
    Console.WriteLine("ENGINE_READY=" + ToolInstaller.Ready);
    return;
}
if (args.Contains("--render"))
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            var app = new App(); app.InitializeComponent();
            var window = new MainWindow();
            var content = (FrameworkElement)window.Content;
            content.Measure(new Size(1180, 820)); content.Arrange(new Rect(0, 0, 1180, 820)); content.UpdateLayout();
            var tabs = ((Grid)content).Children.OfType<TabControl>().Single();
            Directory.CreateDirectory("artifacts");
            for (var i = 0; i < tabs.Items.Count; i++)
            {
                tabs.SelectedIndex = i; content.UpdateLayout();
                var bitmap = new RenderTargetBitmap(1180, 820, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(content);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream = File.Create($"artifacts/ui-{i}.png"); encoder.Save(stream);
            }
            Console.WriteLine("WPF views rendered successfully.");
        }
        catch (Exception ex) { failure = ex; }
    });
    thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
    if (failure != null) throw failure;
    return;
}

int checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); checks++; Console.WriteLine("PASS: " + name); }
foreach (var url in new[] { "https://www.youtube.com/watch?v=test", "https://youtu.be/test", "https://m.facebook.com/watch/?v=123", "https://fb.watch/test/", "https://www.instagram.com/reel/test/", "https://www.threads.net/@user/post/123", "https://www.threads.com/@user/post/123" })
    Check(UrlRules.TryNormalize(url, out _), "Accepted supported host " + url);
foreach (var url in new[] { "file:///C:/secret", "http://youtube.com/test", "https://youtube.com.evil.test/x", "https://evilyoutube.com/x", "https://youtube.com@evil.test/x", "--exec calc", "https://localhost/x", "https://youtube.com:8443/x", "https://user:pass@youtube.com/x" })
    Check(!UrlRules.TryNormalize(url, out _), "Rejected invalid host or scheme " + url);
var job = new DownloadJob { Url = "https://youtube.com/watch?v=x&test=1", Folder = @"C:\影片 資料夾", Quality = "1080", Format = "MP4" };
var argsList = DownloadEngine.BuildArguments(job, "none", "", @"C:\Tools With Spaces");
Check(argsList[^2] == "--" && argsList[^1] == job.Url, "URL isolated after option terminator");
Check(argsList.Contains("bv*[height<=?1080]+ba/b[height<=?1080]"), "Resolution cap applies to fallback; unknown height allowed");
Check(argsList.Contains("--ignore-config") && argsList.Contains("--no-playlist"), "Ignore external config and playlists");
Check(argsList.Contains("--no-plugin-dirs") && argsList.Contains(ThreadsPlugin.Root), "Only explicit bundled plugin directory loaded");
ThreadsPlugin.EnsureInstalled();
Check(File.ReadAllText(Path.Combine(ThreadsPlugin.Root, "threads", "yt_dlp_plugins", "extractor", "threads.py")).Contains("class ThreadsIE"), "Threads extractor embedded and deployed");
var start = new ProcessStartInfo("yt-dlp.exe");
foreach (var arg in argsList) start.ArgumentList.Add(arg);
Check(start.ArgumentList.Contains(job.Folder), "Unicode folder with spaces remains a single argument");
job.Format = "MP3";
argsList = DownloadEngine.BuildArguments(job, "firefox", @"C:\private cookies.txt", "tools");
Check(argsList.Contains("--audio-format") && argsList.Contains("mp3") && !argsList.Contains("--recode-video"), "Audio extraction parameters");
Check(argsList.Contains("--cookies") && !argsList.Contains("--cookies-from-browser"), "Cookie file takes precedence");
job.Format = "MKV"; job.Quality = "best"; job.Subtitles = true;
argsList = DownloadEngine.BuildArguments(job, "firefox", "", "tools");
Check(argsList.Contains("bv*+ba/b") && argsList.Contains("mkv"), "Best quality MKV selection");
Check(argsList.Contains("--write-auto-subs") && argsList.Contains("zh.*,en.*"), "Subtitle options");
var progress = DownloadEngine.ParseLine("VGPROGRESS:  42.3%| 2.5MiB/s|00:12");
Check(progress.Kind == "progress" && progress.Progress == 42.3 && progress.Text.Contains("00:12"), "Machine progress parsing");
Check(DownloadEngine.ParseLine("VGPROGRESS:NA|NA|NA").Kind == "log", "Unknown progress handled");
Check(DownloadEngine.ParseLine("VGTITLE:\"中文 \\" + "\"測試\\\"\"").Text == "中文 \"測試\"", "JSON title decoding");
Check(DownloadEngine.ParseLine("VGFILE:\"C:\\\\影片\\\\test.mp4\"").Text == @"C:\影片\test.mp4", "JSON Windows path decoding");
Console.WriteLine($"All {checks} checks passed.");
