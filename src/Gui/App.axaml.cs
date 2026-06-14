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
            // --monitor 로 시작하면 창을 숨기고 트레이에서 감시
            if (monitor)
            {
                win.ShowInTaskbar = false;
                win.WindowState = Avalonia.Controls.WindowState.Minimized;
            }
        }
        base.OnFrameworkInitializationCompleted();
    }
}
