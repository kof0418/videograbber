using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace VideoGrabber;

public class DownloadJob : INotifyPropertyChanged
{
    public string Url { get; set; } = "";
    public string Folder { get; set; } = "";
    public string Quality { get; set; } = "1080";
    public string Format { get; set; } = "MP4";
    public bool Subtitles { get; set; }
    public DateTime Created { get; set; } = DateTime.Now;
    private string title = "等待解析影片…", status = "等待中", detail = "", filePath = "";
    private double progress;
    public string Title { get => title; set => Set(ref title, value); }
    public string Status { get => status; set => Set(ref status, value); }
    public string Detail { get => detail; set => Set(ref detail, value); }
    public string FilePath { get => filePath; set => Set(ref filePath, value); }
    public double Progress { get => progress; set => Set(ref progress, value); }
    [JsonIgnore] public string Platform => UrlRules.Platform(Url);
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    { field = value; PropertyChanged?.Invoke(this, new(name)); }
}

public class AppSettings
{
    public string Folder { get; set; } = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "VideoGrabber");
    public string Quality { get; set; } = "1080";
    public string Format { get; set; } = "MP4";
    public bool Subtitles { get; set; }
    public string Browser { get; set; } = "none";
    // Cookie 檔路徑與內容不寫入設定，僅保留於目前工作階段。
}

public static class UrlRules
{
    private static readonly string[] Hosts = ["youtube.com", "youtu.be", "facebook.com", "fb.watch", "fb.com", "instagram.com", "threads.net", "threads.com"];
    public static bool TryNormalize(string input, out string result)
    {
        result = "";
        if (!Uri.TryCreate(input.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo) || !uri.IsDefaultPort) return false;
        if (!Hosts.Any(h => uri.Host.Equals(h, StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith("." + h, StringComparison.OrdinalIgnoreCase))) return false;
        result = uri.AbsoluteUri;
        return true;
    }
    public static string Platform(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return "未知";
        var host = uri.Host.ToLowerInvariant();
        if (host.Contains("youtu")) return "YouTube";
        if (host.Contains("instagram")) return "Instagram";
        if (host.Contains("threads")) return "Threads";
        return "Facebook";
    }
}
