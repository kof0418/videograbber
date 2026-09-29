using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace VideoGrabber;
public record EngineEvent(string Kind, string Text, double Progress = 0);

public static class DownloadEngine
{
    public static List<string> BuildArguments(DownloadJob job, string browser, string cookieFile, string tools)
    {
        var args = new List<string> {
            "--ignore-config", "--no-plugin-dirs", "--plugin-dirs", ThreadsPlugin.Root,
            "--no-playlist", "--no-colors", "--newline", "--windows-filenames", "--trim-filenames", "180",
            "--no-overwrites", "--continue", "--retries", "3", "--fragment-retries", "3", "--socket-timeout", "30",
            "--ffmpeg-location", tools, "--js-runtimes", "deno:" + Path.Combine(tools, "deno.exe"),
            "--progress", "--progress-delta", "0.5", "--progress-template", "download:VGPROGRESS:%(progress._percent_str)s|%(progress._speed_str)s|%(progress._eta_str)s",
            "--print", "before_dl:VGTITLE:%(title)j", "--print", "after_move:VGFILE:%(filepath)j", "--no-simulate",
            "-P", job.Folder, "-o", "%(title).120B [%(id)s].%(ext)s"
        };
        if (job.Format == "MP3") args.AddRange(["-f", "bestaudio/best", "-x", "--audio-format", "mp3", "--audio-quality", "0"]);
        else
        {
            var height = job.Quality == "best" ? "" : $"[height<=?{job.Quality}]";
            args.AddRange(["-f", $"bv*{height}+ba/b{height}", "--merge-output-format", job.Format == "MP4" ? "mp4" : "mkv"]);
            if (job.Format == "MP4") args.AddRange(["-S", "vcodec:h264,acodec:aac", "--recode-video", "mp4"]);
        }
        if (job.Subtitles) args.AddRange(["--write-subs", "--write-auto-subs", "--sub-langs", "zh.*,en.*", "--sub-format", "srt/best", "--convert-subs", "srt"]);
        if (!string.IsNullOrWhiteSpace(cookieFile)) args.AddRange(["--cookies", cookieFile]);
        else if (browser != "none") args.AddRange(["--cookies-from-browser", browser]);
        args.Add("--");
        args.Add(job.Url);
        return args;
    }

    public static EngineEvent ParseLine(string line)
    {
        if (line.StartsWith("VGTITLE:")) return new("title", Decode(line[8..]));
        if (line.StartsWith("VGFILE:")) return new("file", Decode(line[7..]));
        if (line.StartsWith("VGPROGRESS:"))
        {
            var parts = line[11..].Split('|');
            if (parts.Length == 3 && double.TryParse(parts[0].Trim().TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                return new("progress", $"{parts[1].Trim()} · 剩餘 {parts[2].Trim()}", Math.Clamp(value, 0, 100));
        }
        return new("log", line);
    }
    private static string Decode(string json)
    { try { return JsonSerializer.Deserialize<string>(json) ?? ""; } catch (JsonException) { return json; } }

    public static async Task RunAsync(DownloadJob job, string browser, string cookieFile, IProgress<EngineEvent> progress, CancellationToken token)
    {
        if (!ToolInstaller.Ready) throw new IOException("請先安裝下載引擎。");
        ThreadsPlugin.EnsureInstalled();
        Directory.CreateDirectory(job.Folder);
        // yt-dlp may modify its cookie jar; protect the user-selected source with a temporary copy.
        string? cookieCopy = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(cookieFile))
            {
                cookieCopy = Path.Combine(Storage.Root, "cookies-" + Guid.NewGuid().ToString("N") + ".txt");
                File.Copy(cookieFile, cookieCopy);
            }
            var start = new ProcessStartInfo(Path.Combine(Storage.Tools, "yt-dlp.exe")) {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true,
                RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
            start.Environment["PYTHONIOENCODING"] = "utf-8";
            foreach (var arg in BuildArguments(job, browser, cookieCopy ?? "", Storage.Tools)) start.ArgumentList.Add(arg);
            using var process = new Process { StartInfo = start };
            token.ThrowIfCancellationRequested();
            process.Start();
            using var registration = token.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { } });
            var errors = new Queue<string>();
            async Task Read(StreamReader reader, bool error)
            {
                while (await reader.ReadLineAsync() is { } line)
                {
                    progress.Report(ParseLine(line));
                    if (error) { errors.Enqueue(line); if (errors.Count > 10) errors.Dequeue(); }
                }
            }
            await Task.WhenAll(Read(process.StandardOutput, false), Read(process.StandardError, true), process.WaitForExitAsync());
            token.ThrowIfCancellationRequested();
            if (process.ExitCode != 0) throw new IOException(string.Join(Environment.NewLine, errors.Prepend("下載失敗。請確認網址、登入權限或更新引擎；網站也可能暫時限制存取。")));
        }
        finally { if (cookieCopy != null) { try { File.Delete(cookieCopy); } catch (IOException) { } } }
    }
}
