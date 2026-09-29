using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using VideoGrabber;

static class IntegrationChecks
{
    private sealed class Sink(Action<EngineEvent> callback) : IProgress<EngineEvent>
    { public void Report(EngineEvent value) => callback(value); }
    public static async Task Run()
    {
        if (!ToolInstaller.Ready) throw new Exception("Install test engine first.");
        var root = Path.Combine(Storage.Root, "integration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var fixture = Path.Combine(root, "fixture.mp4");
        var info = new ProcessStartInfo(Path.Combine(Storage.Tools, "ffmpeg.exe")) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-hide_banner", "-loglevel", "error", "-f", "lavfi", "-i", "color=c=blue:s=320x240:d=1", "-f", "lavfi", "-i", "sine=frequency=440:duration=1", "-c:v", "libx264", "-c:a", "aac", "-shortest", fixture }) info.ArgumentList.Add(arg);
        using (var process = Process.Start(info)!)
        { var error = await process.StandardError.ReadToEndAsync(); await process.WaitForExitAsync(); if (process.ExitCode != 0) throw new Exception(error); }
        var bytes = await File.ReadAllBytesAsync(fixture);
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var stop = new CancellationTokenSource();
        var slowRequest = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = Task.Run(async () =>
        {
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    using var client = await listener.AcceptTcpClientAsync(stop.Token);
                    await using var stream = client.GetStream();
                    using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
                    var request = await reader.ReadLineAsync(stop.Token) ?? "";
                    while (await reader.ReadLineAsync(stop.Token) is { Length: > 0 }) { }
                    if (request.Contains("slow.mp4")) { slowRequest.TrySetResult(); await Task.Delay(Timeout.Infinite, stop.Token); }
                    var missing = request.Contains("missing.mp4");
                    var headers = $"HTTP/1.1 {(missing ? "404 Not Found" : "200 OK")}\r\nContent-Type: video/mp4\r\nContent-Length: {(missing ? 0 : bytes.Length)}\r\nConnection: close\r\n\r\n";
                    await stream.WriteAsync(Encoding.ASCII.GetBytes(headers), stop.Token);
                    if (!missing && !request.StartsWith("HEAD")) await stream.WriteAsync(bytes, stop.Token);
                }
            }
            catch (OperationCanceledException) { }
        });
        try
        {
            foreach (var format in new[] { "MP4", "MP3" })
            {
                string output = "", title = "";
                var job = new DownloadJob { Url = $"http://127.0.0.1:{port}/fixture.mp4", Folder = Path.Combine(root, format), Format = format };
                await DownloadEngine.RunAsync(job, "none", "", new Sink(e => { if (e.Kind == "file") output = e.Text; if (e.Kind == "title") title = e.Text; }), CancellationToken.None);
                if (!File.Exists(output) || new FileInfo(output).Length == 0 || string.IsNullOrEmpty(title) || !output.EndsWith("." + format, StringComparison.OrdinalIgnoreCase)) throw new Exception("Missing output/title for " + format);
                Console.WriteLine("PASS: real engine download/convert " + format + " => " + output);
            }
            var bad = new DownloadJob { Url = $"http://127.0.0.1:{port}/missing.mp4", Folder = root };
            try { await DownloadEngine.RunAsync(bad, "none", "", new Sink(_ => { }), CancellationToken.None); throw new Exception("Expected failure"); }
            catch (IOException ex) when (ex.Message.Contains("404")) { Console.WriteLine("PASS: HTTP failure surfaced"); }
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            try { await DownloadEngine.RunAsync(bad, "none", "", new Sink(_ => { }), cancel.Token); throw new Exception("Expected cancellation"); }
            catch (OperationCanceledException) { Console.WriteLine("PASS: cancellation handled"); }
            using var activeCancel = new CancellationTokenSource();
            bad.Url = $"http://127.0.0.1:{port}/slow.mp4";
            var running = DownloadEngine.RunAsync(bad, "none", "", new Sink(_ => { }), activeCancel.Token);
            await slowRequest.Task.WaitAsync(TimeSpan.FromSeconds(20));
            activeCancel.Cancel();
            try { await running.WaitAsync(TimeSpan.FromSeconds(10)); throw new Exception("Expected running cancellation"); }
            catch (OperationCanceledException) { Console.WriteLine("PASS: active download process terminated on cancel"); }
        }
        finally { stop.Cancel(); listener.Stop(); await server; }
    }
}
