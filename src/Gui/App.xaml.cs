using System.Windows;

namespace ScanPhoneNetwork.Gui;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // --monitor 로 실행되면 트레이 최소화 + 자동 감시 시작
        bool monitor = e.Args.Any(a => a.Equals("--monitor", StringComparison.OrdinalIgnoreCase));
        var win = new MainWindow(startInMonitorMode: monitor);
        if (!monitor) win.Show();
        else win.StartMonitorHeadless();
    }
}
