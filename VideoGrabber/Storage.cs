using System.IO;
using System.Text.Json;

namespace VideoGrabber;
public static class Storage
{
    public static string Root { get; } = Environment.GetEnvironmentVariable("VIDEOGRABBER_DATA_ROOT") is { Length: > 0 } custom
        ? Path.GetFullPath(custom) : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VideoGrabber");
    public static string Tools => Path.Combine(Root, "tools");
    public static string? LastWarning { get; private set; }
    public static T Load<T>(string name, T fallback)
    {
        var path = Path.Combine(Root, name);
        if (!File.Exists(path)) return fallback;
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(path)) ?? fallback; }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        { LastWarning = "設定或歷史無法讀取，已使用預設值：" + ex.Message; return fallback; }
    }
    public static void Save<T>(string name, T value)
    {
        Directory.CreateDirectory(Root);
        var path = Path.Combine(Root, name);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(path + ".tmp", path, true);
    }
}
