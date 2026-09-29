using System.Windows;
namespace VideoGrabber;
public partial class App : Application
{
    private Mutex? instance;
    protected override void OnStartup(StartupEventArgs e)
    {
        instance = new Mutex(true, "Local\\VideoGrabber.Desktop", out var first);
        if (!first) { MessageBox.Show("VideoGrabber 已在執行中，請切換至已開啟的視窗。"); Shutdown(); return; }
        base.OnStartup(e);
    }
    protected override void OnExit(ExitEventArgs e) { instance?.Dispose(); base.OnExit(e); }
}
