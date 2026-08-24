using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;

namespace HardwareMonitor;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private System.Windows.Forms.NotifyIcon? _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Exit", null, (_, _) => Shutdown());

        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = new System.Drawing.Icon(Path.Combine(AppContext.BaseDirectory, "Assets", "tray.ico")),
            Text = "Hardware Monitor",
            Visible = true,
            ContextMenuStrip = menu,
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}