using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using VideoGrabber;

static class CloseRegression
{
    public static void Run()
    {
        Environment.SetEnvironmentVariable("VIDEOGRABBER_DATA_ROOT", Path.Combine(Path.GetTempPath(), "VideoGrabber-close-" + Guid.NewGuid().ToString("N")));
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            var app = new App(); app.InitializeComponent();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            dispatcher.UnhandledException += (_, e) => { failure = e.Exception; e.Handled = true; dispatcher.InvokeShutdown(); };
            dispatcher.BeginInvoke(new Action(async () =>
            {
                try
                {
                    foreach (var scenario in new[] { "idle", "completed", "active" })
                    {
                        var window = new MainWindow { ShowActivated = false, WindowState = WindowState.Minimized };
                        ((TextBox)window.FindName("FolderBox")).Text = Path.Combine(Storage.Root, "downloads");
                        window.Jobs.Clear();
                        window.Jobs.Add(new DownloadJob { Url = "https://youtu.be/test", Status = "等待中" });
                        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                        window.Closed += (_, _) => closed.TrySetResult();
                        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                        using var cancellation = new CancellationTokenSource();
                        void Set(string name, object value) => typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, value);
                        if (scenario == "completed") Set("pumpTask", Task.CompletedTask);
                        if (scenario == "active") { Set("pumpTask", pending.Task); Set("activeCancellation", cancellation); }
                        window.Show();
                        var watch = Stopwatch.StartNew();
                        window.Close();
                        if (scenario == "active")
                        {
                            await Task.Delay(100);
                            if (!cancellation.IsCancellationRequested || closed.Task.IsCompleted) throw new Exception("Close must cancel and await active work.");
                            window.Close();
                            if (closed.Task.IsCompleted) throw new Exception("Repeated close bypassed cleanup.");
                            pending.SetResult();
                        }
                        await closed.Task.WaitAsync(TimeSpan.FromSeconds(3));
                        if (!File.Exists(Path.Combine(Storage.Root, "settings.json"))) throw new Exception("Settings not saved.");
                        if (Storage.Load("history.json", new List<DownloadJob>()).Single().Status != "已中斷") throw new Exception("Queued job not persisted as interrupted.");
                        Console.WriteLine($"PASS: {scenario} close completed cleanly in {watch.ElapsedMilliseconds} ms.");
                    }
                }
                catch (Exception ex) { failure = ex; }
                finally { dispatcher.InvokeShutdown(); }
            }));
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure != null) throw new Exception("Close regression failed", failure);
    }
}
