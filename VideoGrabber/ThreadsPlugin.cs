using System.IO;
using System.Reflection;

namespace VideoGrabber;
public static class ThreadsPlugin
{
    public static string Root => Path.Combine(Storage.Root, "plugins");
    public static void EnsureInstalled()
    {
        var package = Path.Combine(Root, "threads");
        Write("ThreadsExtractor", Path.Combine(package, "yt_dlp_plugins", "extractor", "threads.py"));
        Write("ThreadsLicense", Path.Combine(package, "LICENSE"));
    }
    private static void Write(string resource, string path)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)
            ?? throw new IOException("缺少 Threads 解析器資源。");
        using var reader = new StreamReader(stream);
        var text = reader.ReadToEnd();
        if (File.Exists(path) && File.ReadAllText(path) == text) return;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path + ".tmp", text);
        File.Move(path + ".tmp", path, true);
    }
}
