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

        // The tray icon is the app's lifetime: the widget can be hidden and shown
        // again from here, and only "Exit" here actually quits.
        var menu = new System.Windows.Forms.ContextMenuStrip();
        var toggleItem = menu.Items.Add("Hide widget", null, (_, _) => ToggleWidget());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Shutdown());
        menu.Opening += (_, _) => toggleItem.Text = MainWindow?.IsVisible == true ? "Hide widget" : "Show widget";

        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            // Ask for the tray's own size so Windows picks the matching frame from
            // the .ico instead of shrinking the default 32px one (which looks blurry).
            Icon = new System.Drawing.Icon(
                Path.Combine(AppContext.BaseDirectory, "Assets", "tray.ico"),
                System.Windows.Forms.SystemInformation.SmallIconSize),
            Text = "Hardware Monitor",
            Visible = true,
            ContextMenuStrip = menu,
        };

        // Shows the widget if it was hidden, or brings it to the front when
        // "Always on top" is off.
        _trayIcon.MouseClick += (_, args) =>
        {
            if (args.Button == System.Windows.Forms.MouseButtons.Left)
                ShowWidget();
        };
    }

    private void ShowWidget()
    {
        MainWindow?.Show();
        MainWindow?.Activate();
    }

    private void ToggleWidget()
    {
        if (MainWindow?.IsVisible == true)
            MainWindow.Hide();
        else
            ShowWidget();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}