using System.Configuration;
using System.Data;
using System.IO;
using System.Threading;
using System.Windows;

namespace HardwareMonitor;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private System.Windows.Forms.NotifyIcon? _trayIcon;

    private const string MutexName = @"Local\HardwareMonitor.SingleInstance";
    private const string ShowEventName = @"Local\HardwareMonitor.ShowWidget";

    private Mutex? _singleInstanceMutex;
    private EventWaitHandle? _showEvent;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Only one widget per user session: a second launch asks the running one to
        // show itself, then exits before creating any window.
        _singleInstanceMutex = new Mutex(true, MutexName, out bool isFirstInstance);
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        if (!isFirstInstance)
        {
            _showEvent.Set();
            _showEvent.Dispose();
            _showEvent = null;
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            Shutdown();
            return;
        }

        var showEvent = _showEvent;
        new Thread(() =>
        {
            try
            {
                while (showEvent.WaitOne())
                    Dispatcher.BeginInvoke(ShowWidget);
            }
            catch (ObjectDisposedException)
            {
                // App is exiting.
            }
        })
        { IsBackground = true }.Start();

        base.OnStartup(e);

        // The tray icon is the app's lifetime: the widget can be hidden and shown
        // again from here, and only "Exit" here actually quits.
        var menu = new System.Windows.Forms.ContextMenuStrip();
        var toggleItem = menu.Items.Add("Hide widget", null, (_, _) => ToggleWidget());
        var clickThroughItem = new System.Windows.Forms.ToolStripMenuItem("Click-through");
        clickThroughItem.Click += (_, _) =>
        {
            if (MainWindow is MainWindow widget)
                widget.IsClickThrough = !widget.IsClickThrough;
        };
        menu.Items.Add(clickThroughItem);
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Shutdown());
        menu.Opening += (_, _) =>
        {
            toggleItem.Text = MainWindow?.IsVisible == true ? "Hide widget" : "Show widget";
            clickThroughItem.Checked = (MainWindow as MainWindow)?.IsClickThrough == true;
        };

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

    public void ShowTrayTip(string title, string text) =>
        _trayIcon?.ShowBalloonTip(4000, title, text, System.Windows.Forms.ToolTipIcon.Info);

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
        _showEvent?.Dispose();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}