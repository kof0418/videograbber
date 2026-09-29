using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace VideoGrabber;
public static class ToolInstaller
{
    private static readonly HttpClient Client = CreateClient();
    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("VideoGrabber/1.0");
        return client;
    }
    public static bool Ready => new[] { "yt-dlp.exe", "ffmpeg.exe", "ffprobe.exe", "deno.exe" }.All(n => File.Exists(Path.Combine(Storage.Tools, n)));

    public static async Task InstallAsync(IProgress<string> progress, CancellationToken token)
    {
        Directory.CreateDirectory(Storage.Root);
        var stage = Path.Combine(Storage.Root, "install-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        try
        {
            await Fetch("yt-dlp/yt-dlp", "yt-dlp.exe", stage, progress, token);
            await Fetch("yt-dlp/FFmpeg-Builds", "ffmpeg-master-latest-win64-gpl.zip", stage, progress, token);
            await Fetch("denoland/deno", "deno-x86_64-pc-windows-msvc.zip", stage, progress, token);
            token.ThrowIfCancellationRequested();
            // Only replace the complete tool set after every download/extraction succeeds.
            var bundle = Path.Combine(stage, "bundle");
            Directory.CreateDirectory(bundle);
            foreach (var name in new[] { "yt-dlp.exe", "ffmpeg.exe", "ffprobe.exe", "deno.exe" })
            {
                var source = Directory.EnumerateFiles(stage, name, SearchOption.AllDirectories).FirstOrDefault()
                    ?? throw new IOException("下載套件缺少 " + name);
                File.Move(source, Path.Combine(bundle, name));
            }
            var previous = Path.Combine(stage, "previous");
            if (Directory.Exists(Storage.Tools)) Directory.Move(Storage.Tools, previous);
            try { Directory.Move(bundle, Storage.Tools); }
            catch { if (Directory.Exists(previous)) Directory.Move(previous, Storage.Tools); throw; }
            progress.Report("下載引擎、FFmpeg 與 Deno 已就緒。");
        }
        finally { try { Directory.Delete(stage, true); } catch (IOException) { } }
    }

    private static async Task Fetch(string repo, string assetName, string stage, IProgress<string> progress, CancellationToken token)
    {
        progress.Report("取得最新版本：" + repo);
        using var response = await Client.GetAsync($"https://api.github.com/repos/{repo}/releases/latest", token);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        var asset = doc.RootElement.GetProperty("assets").EnumerateArray().FirstOrDefault(a => a.GetProperty("name").GetString() == assetName);
        if (asset.ValueKind == JsonValueKind.Undefined) throw new IOException("官方發行版本找不到 " + assetName);
        var url = asset.GetProperty("browser_download_url").GetString()!;
        if (!url.StartsWith("https://github.com/", StringComparison.Ordinal)) throw new IOException("非預期的下載來源。");
        var target = Path.Combine(stage, assetName);
        using var download = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
        download.EnsureSuccessStatusCode();
        var total = download.Content.Headers.ContentLength;
        await using (var input = await download.Content.ReadAsStreamAsync(token))
        await using (var output = File.Create(target))
        {
            var buffer = new byte[131072];
            long done = 0;
            var last = DateTime.MinValue;
            int count;
            while ((count = await input.ReadAsync(buffer, token)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, count), token);
                done += count;
                if ((DateTime.UtcNow - last).TotalMilliseconds < 300) continue;
                last = DateTime.UtcNow;
                progress.Report($"下載 {assetName} · {done / 1048576.0:F1} MB" + (total > 0 ? $" / {total / 1048576.0:F1} MB" : ""));
            }
        }
        if (asset.TryGetProperty("digest", out var digest) && digest.GetString() is { } expected && expected.StartsWith("sha256:"))
        {
            await using var file = File.OpenRead(target);
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(file, token));
            if (!actual.Equals(expected[7..], StringComparison.OrdinalIgnoreCase)) throw new IOException("下載檔案 SHA-256 驗證失敗：" + assetName);
        }
        if (target.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            progress.Report("解壓縮 " + assetName);
            await Task.Run(() => ZipFile.ExtractToDirectory(target, Path.Combine(stage, Path.GetFileNameWithoutExtension(assetName))), token);
        }
    }
}
