using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace VideoGrabber;
public partial class MainWindow : Window
{
    public ObservableCollection<DownloadJob> Jobs { get; } = [];
    private readonly AppSettings settings;
    private string cookieFile = "";
    private bool pumping, paused, closing;
    private CancellationTokenSource? activeCancellation, installCancellation;
    private Task? pumpTask, installTask;
    private DownloadJob? active;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        settings = Storage.Load("settings.json", new AppSettings());
        FolderBox.Text = settings.Folder;
        QualityBox.SelectedValue = settings.Quality;
        FormatBox.SelectedValue = settings.Format;
        BrowserBox.SelectedValue = settings.Browser;
        SubtitlesBox.IsChecked = settings.Subtitles;
        foreach (var job in Storage.Load("history.json", new List<DownloadJob>()).Take(500))
        {
            if (job.Status is "等待中" or "下載中" or "處理中") { job.Status = "已中斷"; job.Detail = "上次工作未完成，可重新下載。"; }
            Jobs.Add(job);
        }
        EngineText.Text = ToolInstaller.Ready ? "引擎已就緒，可以開始下載。" : "尚未安裝，請先按「安裝／更新引擎」。";
        StatusText.Text = Storage.LastWarning ?? (ToolInstaller.Ready ? "準備就緒 · 貼上網址即可加入下載" : "準備就緒 · 初次使用請先安裝引擎");
        UpdateCount();
        RefreshEngineState();
    }

    private void RefreshEngineState()
    {
        bool installing = installCancellation != null;
        bool ready = ToolInstaller.Ready;
        SetupBanner.Visibility = !ready || installing ? Visibility.Visible : Visibility.Collapsed;
        SetupInstallButton.IsEnabled = !installing && !pumping && !closing;
        SetupInstallButton.Content = installing ? "正在安裝…" : "安裝引擎並繼續";
        SetupText.Text = installing ? "正在安裝下載引擎，完成後自動處理佇列。"
            : "首次使用需安裝下載引擎。網址可先加入佇列，安裝完成後自動下載。";
        if (!ready || installing)
            foreach (var job in Jobs.Where(j => j.Status == "等待中"))
                job.Detail = installing ? "等待引擎安裝完成…" : "等待安裝下載引擎，請按上方「安裝引擎並繼續」。";
    }

    private void Notice(string message) => StatusText.Text = message;
    private void UpdateCount() => CountText.Text = $"{Jobs.Count} 筆工作 · {Jobs.Count(j => j.Status == "完成")} 筆完成";
    private void Persist()
    {
        try { Storage.Save("settings.json", settings); Storage.Save("history.json", Jobs.TakeLast(500).ToList()); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Notice("無法儲存設定／歷史：" + ex.Message); }
    }
    private bool ReadSettings()
    {
        try
        {
            var folder = FolderBox.Text.Trim();
            if (!Path.IsPathFullyQualified(folder)) throw new IOException("請選擇完整的下載資料夾路徑。");
            Directory.CreateDirectory(folder);
            settings.Folder = Path.GetFullPath(folder);
            settings.Quality = QualityBox.SelectedValue?.ToString() ?? "1080";
            settings.Format = FormatBox.SelectedValue?.ToString() ?? "MP4";
            settings.Browser = BrowserBox.SelectedValue?.ToString() ?? "none";
            settings.Subtitles = SubtitlesBox.IsChecked == true;
            return true;
        }
        catch (Exception ex) { Notice("設定無法套用：" + ex.Message); return false; }
    }
    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (!ReadSettings()) return;
        var lines = UrlsBox.Text.Split(['\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        int added = 0, duplicate = 0;
        var invalid = new List<string>();
        foreach (var line in lines)
        {
            if (!UrlRules.TryNormalize(line, out var url)) { invalid.Add(line); continue; }
            if (Jobs.Any(j => j.Url == url && j.Status is "等待中" or "下載中" or "處理中")) { duplicate++; continue; }
            Jobs.Add(new DownloadJob { Url = url, Folder = settings.Folder, Quality = settings.Quality, Format = settings.Format, Subtitles = settings.Subtitles });
            added++;
        }
        UrlsBox.Text = string.Join(Environment.NewLine, invalid);
        Notice($"已加入 {added} 筆，略過 {duplicate} 筆重複。" + (invalid.Count > 0 ? $" {invalid.Count} 筆網址無效；請使用支援平台的 HTTPS 網址。" : ""));
        UpdateCount(); Persist(); StartPump();
    }
    private void StartPump()
    {
        RefreshEngineState();
        if (!ToolInstaller.Ready || installCancellation != null) return;
        if (!pumping && !closing && !paused) pumpTask = PumpAsync();
    }
    private async Task PumpAsync()
    {
        pumping = true;
        InstallButton.IsEnabled = false;
        try
        {
            while (!paused && !closing && Jobs.FirstOrDefault(j => j.Status == "等待中") is { } job)
            {
                active = job;
                using var cancellation = new CancellationTokenSource();
                activeCancellation = cancellation;
                job.Status = "下載中"; job.Detail = "正在解析與連線…";
                Persist();
                var progress = new Progress<EngineEvent>(update =>
                {
                    if (job.Status is not ("下載中" or "處理中")) return;
                    switch (update.Kind)
                    {
                        case "title": job.Title = update.Text; break;
                        case "file": job.FilePath = update.Text; break;
                        case "progress": job.Progress = update.Progress; job.Detail = update.Text; job.Status = update.Progress >= 100 ? "處理中" : "下載中"; break;
                        case "log": if (!string.IsNullOrWhiteSpace(update.Text)) job.Detail = update.Text; break;
                    }
                });
                try
                {
                    await DownloadEngine.RunAsync(job, settings.Browser, cookieFile, progress, cancellation.Token);
                    // Let queued progress callbacks publish the final output path before marking complete.
                    await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
                    if (string.IsNullOrWhiteSpace(job.FilePath) || !File.Exists(job.FilePath)) throw new IOException("引擎結束但找不到輸出檔案，請查看來源是否含有影片。");
                    job.Status = "完成"; job.Progress = 100; job.Detail = job.FilePath;
                }
                catch (OperationCanceledException) { job.Status = closing ? "已中斷" : "已取消"; job.Detail = "已停止；保留部分檔案以供重試續傳。"; }
                catch (Exception ex) { job.Status = "失敗"; job.Detail = ex.Message; }
                finally { activeCancellation = null; active = null; Persist(); UpdateCount(); }
            }
        }
        finally { pumping = false; InstallButton.IsEnabled = !closing; }
    }
    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (JobsList.SelectedItem is not DownloadJob job) { Notice("請先選取工作。"); return; }
        if (ReferenceEquals(job, active)) activeCancellation?.Cancel();
        else if (job.Status == "等待中") { job.Status = "已取消"; job.Detail = "尚未開始下載。"; Persist(); }
    }
    private void Pause_Click(object sender, RoutedEventArgs e)
    { paused = !paused; PauseButton.Content = paused ? "繼續佇列" : "暫停佇列"; Notice(paused ? "目前工作繼續，後續工作已暫停。" : "佇列已繼續。"); if (!paused) StartPump(); }
    private void Retry_Click(object sender, RoutedEventArgs e)
    {
        if (JobsList.SelectedItem is not DownloadJob job) { Notice("請先選取工作。"); return; }
        if (job.Status is "下載中" or "處理中" or "等待中") return;
        job.Status = "等待中"; job.Progress = 0; job.FilePath = ""; job.Detail = "等待重新下載…";
        Persist(); StartPump();
    }
    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        foreach (var job in Jobs.Where(j => j.Status is not ("等待中" or "下載中" or "處理中")).ToList()) Jobs.Remove(job);
        UpdateCount(); Persist(); Notice("已移除結束的工作紀錄，下載檔案保留於原位置。");
    }
    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        if (pumping || installCancellation != null) return;
        using var cancellation = new CancellationTokenSource();
        installCancellation = cancellation;
        RefreshEngineState();
        InstallButton.IsEnabled = false; CancelInstallButton.IsEnabled = true;
        try
        {
            installTask = ToolInstaller.InstallAsync(new Progress<string>(s => { EngineText.Text = s; SetupText.Text = s; }), cancellation.Token);
            await installTask;
            Notice(paused ? "引擎安裝完成，按「繼續佇列」開始下載。" : "引擎安裝完成，已加入的工作將自動開始下載。");
        }
        catch (OperationCanceledException) { EngineText.Text = "安裝已取消；既有引擎仍可使用。"; Notice("安裝已取消，佇列已保留。尚未安裝引擎時，可再按「安裝引擎並繼續」。"); }
        catch (Exception ex) { EngineText.Text = "安裝失敗：" + ex.Message + "\n請確認網路連線後重試。"; Notice(EngineText.Text + " 佇列已保留。"); }
        finally { installCancellation = null; InstallButton.IsEnabled = true; CancelInstallButton.IsEnabled = false; RefreshEngineState(); if (!closing) StartPump(); }
    }
    private void CancelInstall_Click(object sender, RoutedEventArgs e) => installCancellation?.Cancel();
    private void Folder_Click(object sender, RoutedEventArgs e)
    { var dialog = new OpenFolderDialog { Title = "選擇下載資料夾" }; if (dialog.ShowDialog(this) == true) FolderBox.Text = dialog.FolderName; }
    private void Cookie_Click(object sender, RoutedEventArgs e)
    { var dialog = new OpenFileDialog { Title = "選擇 Netscape Cookie 檔", Filter = "Cookie 文字檔|*.txt|所有檔案|*.*" }; if (dialog.ShowDialog(this) == true) { cookieFile = dialog.FileName; CookieLabel.Text = Path.GetFileName(cookieFile); } }
    private void RemoveCookie_Click(object sender, RoutedEventArgs e) { cookieFile = ""; CookieLabel.Text = "未選擇（僅本次執行使用）"; }
    private void Save_Click(object sender, RoutedEventArgs e) { if (ReadSettings()) { Notice("設定已儲存。"); Persist(); } }
    private void Paste_Click(object sender, RoutedEventArgs e)
    { try { if (Clipboard.ContainsText()) UrlsBox.Text = Clipboard.GetText(); } catch (Exception ex) { Notice("無法讀取剪貼簿：" + ex.Message); } }
    private void OpenFolder_Click(object sender, RoutedEventArgs e) { if (ReadSettings()) OpenPath(settings.Folder, false); }
    private void OpenFile_Click(object sender, RoutedEventArgs e)
    { if (JobsList.SelectedItem is DownloadJob job) OpenPath(File.Exists(job.FilePath) ? job.FilePath : job.Folder, File.Exists(job.FilePath)); else Notice("請先選取工作。"); }
    private void OpenPath(string path, bool select)
    {
        try
        {
            if (!File.Exists(path) && !Directory.Exists(path)) { Notice("檔案或資料夾已不存在。"); return; }
            var info = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
            if (select) info.ArgumentList.Add("/select,");
            info.ArgumentList.Add(path);
            Process.Start(info);
        }
        catch (Exception ex) { Notice("無法開啟位置：" + ex.Message); }
    }
    private void Details_Click(object sender, RoutedEventArgs e)
    { if (JobsList.SelectedItem is DownloadJob job) MessageBox.Show(this, $"{job.Title}\n{job.Url}\n\n{job.Status}\n{job.Detail}", "工作詳情", MessageBoxButton.OK, MessageBoxImage.Information); }
    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (closing) return;
        e.Cancel = true; closing = true; IsEnabled = false;
        activeCancellation?.Cancel(); installCancellation?.Cancel();
        try { if (pumpTask != null) await pumpTask; if (installTask != null) await installTask; } catch (Exception) { /* Failure already displayed by owning operation. */ }
        foreach (var job in Jobs.Where(j => j.Status == "等待中")) { job.Status = "已中斷"; job.Detail = "程式已關閉，可重新下載。"; }
        ReadSettings(); Persist(); Close();
    }
}
