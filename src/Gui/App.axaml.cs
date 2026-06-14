using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace ScanPhoneNetwork.Gui;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            bool monitor = desktop.Args?.Any(a => a.Equals("--monitor", StringComparison.OrdinalIgnoreCase)) == true;
            var win = new MainWindow(startInMonitorMode: monitor);
            desktop.MainWindow = win;
            if (monitor)
            {
                // --monitor: 트레이에서 조용히 감시
                win.ShowInTaskbar = false;
                win.WindowState = Avalonia.Controls.WindowState.Minimized;
            }
            else
            {
                // 일반 실행: 창을 명시적으로 표시
                win.Show();
                win.Activate();
            }
        }
        base.OnFrameworkInitializationCompleted();
    }
}
