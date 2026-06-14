using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WinForms = System.Windows.Forms;

namespace ScanPhoneNetwork.Gui;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<HostRow> _rows = new();
    private readonly MonitorService _monitor;
    private CancellationTokenSource? _scanCts;
    private ScanReport? _lastReport;
    private WinForms.NotifyIcon? _tray;

    private static string AllowListPath =>
        Path.Combine(AppContext.BaseDirectory, "allowlist.json");

    public MainWindow(bool startInMonitorMode = false)
    {
        InitializeComponent();
        Grid.ItemsSource = _rows;

        // 외부 oui.csv 가 exe 옆에 있으면 자동 로드(식별률 향상)
        var ouiPath = Path.Combine(AppContext.BaseDirectory, "oui.csv");
        if (File.Exists(ouiPath)) OuiDatabase.LoadExternalCsv(ouiPath);

        _monitor = new MonitorService(AllowListPath);
        _monitor.ScanCompleted += r => Dispatcher.Invoke(() => ShowReport(r));
        _monitor.NewSuspicious += h => Dispatcher.Invoke(() => AlertNewDevice(h));
        _monitor.StatusChanged += s => Dispatcher.Invoke(() => StatusText.Text = s);

        AutoStartCheck.IsChecked = AutoStart.IsEnabled();
        SetupTray();

        if (startInMonitorMode) MonitorCheck.IsChecked = true;
    }

    /// <summary>--monitor 로 시작될 때: 창 숨기고 트레이에서 감시.</summary>
    public void StartMonitorHeadless()
    {
        Hide();
        StartMonitor();
        _tray!.ShowBalloonTip(3000, "업무망 감시 시작",
            "백그라운드에서 업무망을 감시합니다.", WinForms.ToolTipIcon.Info);
    }

    // ---------- 1회 스캔 ----------

    private async void OnScanClick(object sender, RoutedEventArgs e)
    {
        _rows.Clear();
        _scanCts = new CancellationTokenSource();
        SetScanning(true);

        var progress = new Progress<ScanProgress>(p =>
        {
            StatusText.Text = $"{p.Phase} ({p.Done}/{p.Total})";
            Progress.Value = p.Total > 0 ? p.Done * 100.0 / p.Total : 0;
        });

        try
        {
            string? cidr = string.IsNullOrWhiteSpace(TargetBox.Text) ? null : TargetBox.Text.Trim();
            var report = await new Scanner().RunAsync(cidr, progress, _scanCts.Token);
            ShowReport(report);
            StatusText.Text = $"완료 · {report.TargetRange}";
        }
        catch (OperationCanceledException) { StatusText.Text = "중지됨"; }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
            StatusText.Text = "오류";
        }
        finally { SetScanning(false); }
    }

    private void OnStopClick(object sender, RoutedEventArgs e) => _scanCts?.Cancel();

    private void ShowReport(ScanReport report)
    {
        _lastReport = report;
        _rows.Clear();
        foreach (var h in report.Hosts) _rows.Add(new HostRow(h));
        Progress.Value = 100;
        int sus = report.Suspicious.Count();
        SummaryText.Text = sus > 0 ? $"⚠ 의심 장비 {sus}대" : "의심 장비 없음";
        CsvButton.IsEnabled = report.Hosts.Count > 0;
    }

    private void SetScanning(bool on)
    {
        ScanButton.IsEnabled = !on;
        StopButton.IsEnabled = on;
    }

    // ---------- CSV 내보내기 ----------

    private void OnExportClick(object sender, RoutedEventArgs e)
    {
        if (_lastReport is null) return;
        var dlg = new SaveFileDialog
        {
            Filter = "CSV 파일 (*.csv)|*.csv",
            FileName = $"업무망점검_{DateTime.Now:yyyyMMdd_HHmm}.csv",
        };
        if (dlg.ShowDialog() == true)
        {
            CsvExporter.Save(_lastReport, dlg.FileName);
            StatusText.Text = "저장 완료: " + dlg.FileName;
        }
    }

    // ---------- 감시 모드 ----------

    private void OnMonitorToggle(object sender, RoutedEventArgs e)
    {
        if (MonitorCheck.IsChecked == true) StartMonitor();
        else _monitor.Stop();
    }

    private void StartMonitor()
    {
        _monitor.TargetCidr = string.IsNullOrWhiteSpace(TargetBox.Text) ? null : TargetBox.Text.Trim();
        if (int.TryParse(IntervalBox.Text, out int min) && min > 0)
            _monitor.Interval = TimeSpan.FromMinutes(min);
        _monitor.Start();
        StatusText.Text = "감시 시작...";
    }

    private void AlertNewDevice(DiscoveredHost h)
    {
        string msg = $"새 의심 장비 발견\nIP: {h.Ip}\nMAC: {h.Mac ?? "-"}\n" +
                     $"종류: {CsvExporter.CategoryKo(h.Category)} ({h.Confidence}%)\n" +
                     $"제조사: {h.Vendor ?? "-"}";
        _tray?.ShowBalloonTip(8000, "⚠ 업무망 무단 장비 경고", msg, WinForms.ToolTipIcon.Warning);
        System.Media.SystemSounds.Exclamation.Play();
    }

    // ---------- 승인(allowlist) ----------

    private void OnApproveClick(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is HostRow row)
        {
            if (row.Source.Mac is null)
            {
                MessageBox.Show("MAC 을 알 수 없는 장비는 승인할 수 없습니다.", "승인 불가");
                return;
            }
            _monitor.Approve(row.Source);
            StatusText.Text = $"승인됨: {row.Mac}";
        }
    }

    // ---------- 자동 시작 ----------

    private void OnAutoStartToggle(object sender, RoutedEventArgs e)
    {
        if (AutoStartCheck.IsChecked == true) AutoStart.Enable();
        else AutoStart.Disable();
    }

    // ---------- 트레이 ----------

    private void SetupTray()
    {
        _tray = new WinForms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Shield,
            Visible = true,
            Text = "업무망 점검기",
        };
        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("창 열기", null, (_, _) => { Show(); WindowState = WindowState.Normal; Activate(); });
        menu.Items.Add("종료", null, (_, _) => { _tray!.Visible = false; Application.Current.Shutdown(); });
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => { Show(); WindowState = WindowState.Normal; Activate(); };
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        // 최소화하면 트레이로 숨김(감시 모드일 때 방해 없이 동작)
        if (WindowState == WindowState.Minimized) Hide();
    }

    protected override void OnClosed(EventArgs e)
    {
        _tray?.Dispose();
        base.OnClosed(e);
    }
}
