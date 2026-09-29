using System.IO;
using System.Windows;
using System.Windows.Controls;
using VideoGrabber;

static class QueueRegression
{
    public static void Run()
    {
        Environment.SetEnvironmentVariable("VIDEOGRABBER_DATA_ROOT", Path.GetFullPath(Path.Combine(".tools", "queue-test-" + Guid.NewGuid().ToString("N"))));
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var app = new App(); app.InitializeComponent();
                var window = new MainWindow();
                T Control<T>(string name) where T : FrameworkElement => (T)window.FindName(name);
                void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS: " + name); }
                Check(!ToolInstaller.Ready, "fresh profile has no engine");
                Control<TextBox>("FolderBox").Text = Path.Combine(Storage.Root, "downloads");
                var url = "https://www.facebook.com/share/r/19aE8HYGp3/";
                Control<TextBox>("UrlsBox").Text = url;
                Control<Button>("AddButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(window.Jobs.Count == 1 && window.Jobs[0].Url == url, "Facebook share URL enqueued without engine");
                Check(window.Jobs[0].Status == "等待中" && window.Jobs[0].Detail.Contains("引擎"), "job waits for installation instead of failing");
                Check(Control<Border>("SetupBanner").Visibility == Visibility.Visible && Control<Button>("SetupInstallButton").IsEnabled, "inline installation action visible");
                Check(Control<TextBox>("UrlsBox").Text == "", "accepted URL cleared from input");
                Check(Storage.Load("history.json", new List<DownloadJob>()).Count == 1, "queued job persisted");
                Control<TextBox>("UrlsBox").Text = url;
                Control<Button>("AddButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(window.Jobs.Count == 1, "waiting duplicates suppressed");
                Control<Button>("PauseButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Control<Button>("PauseButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(window.Jobs[0].Status == "等待中", "resume cannot start a missing engine");
                Control<TextBox>("UrlsBox").Text = "https://example.com/invalid";
                Control<Button>("AddButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(window.Jobs.Count == 1 && Control<TextBox>("UrlsBox").Text.Contains("example.com"), "invalid URL retained and not queued");
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure != null) throw failure;
    }
}
