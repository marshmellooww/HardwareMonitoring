using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using HardwareMonitor.Services;

namespace HardwareMonitor;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private const double EdgeMargin = 16;

    private static readonly Brush NormalBrush = Brushes.LimeGreen;
    private static readonly Brush WarmBrush = Brushes.Orange;
    private static readonly Brush HotBrush = Brushes.OrangeRed;
    private static readonly Brush UnknownBrush = Brushes.Gray;

    private readonly HardwareMonitorService _hardwareMonitor = new();
    private readonly AppSettings _settings = AppSettings.Load();
    private readonly DispatcherTimer _timer = new();
    private bool _isPositioned;

    public MainWindow()
    {
        InitializeComponent();
        ApplySettings();

        if (!_hardwareMonitor.IsPawnIoInstalled)
        {
            CpuTempText.ToolTip = "CPU temperature needs the PawnIO driver.\n"
                                  + "Install it from https://pawnio.eu (or run: winget install namazso.PawnIO),\n"
                                  + "then restart Hardware Monitor.";
        }

        // Interval comes from the Refresh rate setting (see ApplySettings).
        _timer.Tick += (_, _) => UpdateReadings();
        _timer.Start();

        // Runs schtasks.exe, so keep it off the UI thread.
        bool startWithWindows = _settings.StartWithWindows;
        Task.Run(() => StartupTask.Apply(startWithWindows));
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Populate real readings before anchoring, so the window is sized
        // and positioned for its actual content rather than the "--" placeholders.
        UpdateReadings();
        UpdateLayout();
        if (!RestoreSavedPosition())
            PositionWindow();
        _isPositioned = true;
    }

    private void PositionWindow()
    {
        Rect workArea = SystemParameters.WorkArea;
        Left = workArea.Right - ActualWidth - EdgeMargin;
        Top = workArea.Top + EdgeMargin;
    }

    private bool RestoreSavedPosition()
    {
        if (_settings.Left is not double left || _settings.Top is not double top)
            return false;

        // Ignore a position left over from a monitor that's no longer connected.
        var virtualScreen = new Rect(
            SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        if (!virtualScreen.Contains(new Point(left + ActualWidth / 2, top + ActualHeight / 2)))
            return false;

        Left = left;
        Top = top;
        return true;
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Keep the edge nearest the screen side fixed, so a widget docked on the
        // right grows leftwards when its layout, size or readings change width.
        if (!_isPositioned || !e.WidthChanged)
            return;

        Rect workArea = GetCurrentWorkArea();
        double previousCenter = Left + e.PreviousSize.Width / 2;
        if (previousCenter > workArea.Left + workArea.Width / 2)
            Left -= e.NewSize.Width - e.PreviousSize.Width;
    }

    private Rect GetCurrentWorkArea()
    {
        var screen = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle);
        Matrix fromDevice = PresentationSource.FromVisual(this)?.CompositionTarget.TransformFromDevice ?? Matrix.Identity;
        System.Drawing.Rectangle area = screen.WorkingArea;
        return new Rect(
            fromDevice.Transform(new Point(area.Left, area.Top)),
            fromDevice.Transform(new Point(area.Right, area.Bottom)));
    }

    private void ApplySettings()
    {
        _settings.Scale = Math.Clamp(_settings.Scale, 0.5, 2);
        _settings.Opacity = Math.Clamp(_settings.Opacity, 0.2, 1);
        _settings.RefreshSeconds = Math.Clamp(_settings.RefreshSeconds, 1, 5);

        ApplyLayout(_settings.IsHorizontal);
        ContentScale.ScaleX = ContentScale.ScaleY = _settings.Scale;
        Opacity = _settings.Opacity;
        Topmost = _settings.Topmost;
        _timer.Interval = TimeSpan.FromSeconds(_settings.RefreshSeconds);

        VerticalLayoutItem.IsChecked = !_settings.IsHorizontal;
        HorizontalLayoutItem.IsChecked = _settings.IsHorizontal;
        CheckItemWithTag(SizeMenu, _settings.Scale);
        CheckItemWithTag(OpacityMenu, _settings.Opacity);
        CheckItemWithTag(RefreshMenu, _settings.RefreshSeconds);
        TopmostItem.IsChecked = _settings.Topmost;
        LockPositionItem.IsChecked = _settings.IsPositionLocked;
        ClickThroughItem.IsChecked = _settings.IsClickThrough;
        StartWithWindowsItem.IsChecked = _settings.StartWithWindows;
        ApplyClickThrough();
    }

    private void ApplyLayout(bool horizontal)
    {
        StatsPanel.Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical;

        // Shared column widths line values up in a column; in a single row they
        // would just pad every stat out to the widest one.
        Grid.SetIsSharedSizeScope(StatsPanel, !horizontal);

        List<Grid> stats = StatsPanel.Children.OfType<Grid>().ToList();

        // An empty widget can't be right-clicked to bring stats back, so never hide them all.
        if (stats.All(s => _settings.HiddenStats.Contains((string)s.Tag)))
            _settings.HiddenStats.Clear();

        foreach (Grid stat in stats)
        {
            bool shown = !_settings.HiddenStats.Contains((string)stat.Tag);
            stat.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;

            // Hidden rows keep their last width in a shared size group, which
            // would leave the widget as wide as a stat that isn't shown anymore.
            stat.ColumnDefinitions[0].SharedSizeGroup = shown ? "Label" : null;
            stat.ColumnDefinitions[1].SharedSizeGroup = shown ? "Value" : null;
        }

        List<Grid> visible = stats.Where(s => s.Visibility == Visibility.Visible).ToList();
        Thickness spacing = horizontal ? new Thickness(0, 0, 18, 0) : new Thickness(0, 0, 0, 4);
        for (int i = 0; i < visible.Count; i++)
            visible[i].Margin = i < visible.Count - 1 ? spacing : new Thickness(0);

        // Check the shown sensors, and lock the last one so it can't be unchecked.
        foreach (MenuItem item in SensorsMenu.Items.OfType<MenuItem>())
        {
            item.IsChecked = !_settings.HiddenStats.Contains((string)item.Tag);
            item.IsEnabled = !(item.IsChecked && visible.Count == 1);
        }
    }

    private static void CheckItemWithTag(MenuItem menu, double value)
    {
        foreach (MenuItem item in menu.Items.OfType<MenuItem>())
            item.IsChecked = Math.Abs(ParseTag(item) - value) < 0.001;
    }

    private static double ParseTag(object menuItem) =>
        double.Parse((string)((MenuItem)menuItem).Tag, CultureInfo.InvariantCulture);

    private void UpdateSettings(Action<AppSettings> change)
    {
        change(_settings);
        ApplySettings();
        _settings.Save();
    }

    private void UpdateReadings()
    {
        _hardwareMonitor.Refresh();
        SetTemperature(CpuTempText, _hardwareMonitor.CpuTemperature,
            _hardwareMonitor.IsPawnIoInstalled ? "N/A" : "No driver");
        SetTemperature(GpuTempText, _hardwareMonitor.GpuTemperature);
        SetUsage(RamText, _hardwareMonitor.RamUsedGb, _hardwareMonitor.RamTotalGb, "GB");
        SetUsage(VramText, _hardwareMonitor.VramUsedGb, _hardwareMonitor.VramTotalGb, "GB");
        SetDisk(DiskText, _hardwareMonitor.DiskFreeGb, _hardwareMonitor.DiskTotalGb, "GB");
    }

    private static void SetTemperature(TextBlock block, float? value, string unknownText = "N/A")
    {
        if (!value.HasValue)
        {
            block.Text = unknownText;
            block.Foreground = UnknownBrush;
            return;
        }

        block.Text = $"{value.Value:0}°C";
        block.Foreground = value.Value switch
        {
            <= 60 => NormalBrush,
            <= 80 => WarmBrush,
            _ => HotBrush,
        };
    }

    private static void SetUsage(TextBlock block, float? used, float? total, string unit)
    {
        if (!used.HasValue || !total.HasValue || total.Value <= 0)
        {
            block.Text = "N/A";
            block.Foreground = UnknownBrush;
            return;
        }

        block.Text = $"{used.Value:0.#}/{total.Value:0.#} {unit}";
        double ratio = used.Value / total.Value;
        block.Foreground = ratio switch
        {
            <= 0.6 => NormalBrush,
            <= 0.85 => WarmBrush,
            _ => HotBrush,
        };
    }

    private static void SetDisk(TextBlock block, double? free, double? total, string unit)
    {
        if (!free.HasValue || !total.HasValue || total.Value <= 0)
        {
            block.Text = "N/A";
            block.Foreground = UnknownBrush;
            return;
        }

        block.Text = $"{free.Value:0}/{total.Value:0} {unit}";
        double freeRatio = free.Value / total.Value;
        block.Foreground = freeRatio switch
        {
            >= 0.2 => NormalBrush,
            >= 0.1 => WarmBrush,
            _ => HotBrush,
        };
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed || _settings.IsPositionLocked)
            return;

        DragMove();
        _settings.Left = Left;
        _settings.Top = Top;
        _settings.Save();
    }

    private void SensorMenuItem_Click(object sender, RoutedEventArgs e)
    {
        string tag = (string)((MenuItem)sender).Tag;
        UpdateSettings(s =>
        {
            if (!s.HiddenStats.Remove(tag))
                s.HiddenStats.Add(tag);
        });
    }

    private void LayoutMenuItem_Click(object sender, RoutedEventArgs e) =>
        UpdateSettings(s => s.IsHorizontal = sender == HorizontalLayoutItem);

    private void SizeMenuItem_Click(object sender, RoutedEventArgs e) =>
        UpdateSettings(s => s.Scale = ParseTag(sender));

    private void RefreshMenuItem_Click(object sender, RoutedEventArgs e) =>
        UpdateSettings(s => s.RefreshSeconds = (int)ParseTag(sender));

    private void OpacityMenuItem_Click(object sender, RoutedEventArgs e) =>
        UpdateSettings(s => s.Opacity = ParseTag(sender));

    private void TopmostMenuItem_Click(object sender, RoutedEventArgs e) =>
        UpdateSettings(s => s.Topmost = !s.Topmost);

    private void LockPositionMenuItem_Click(object sender, RoutedEventArgs e) =>
        UpdateSettings(s => s.IsPositionLocked = !s.IsPositionLocked);

    private void StartWithWindowsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        UpdateSettings(s => s.StartWithWindows = !s.StartWithWindows);
        bool startWithWindows = _settings.StartWithWindows;
        Task.Run(() => StartupTask.Apply(startWithWindows));
    }

    // Once on, the widget can't be clicked anymore, so it's turned off from the tray.
    private void ClickThroughMenuItem_Click(object sender, RoutedEventArgs e)
    {
        IsClickThrough = true;
        ((App)Application.Current).ShowTrayTip(
            "Click-through is on",
            "Clicks now go to whatever is behind the widget. Right-click the tray icon to turn it off.");
    }

    public bool IsClickThrough
    {
        get => _settings.IsClickThrough;
        set => UpdateSettings(s => s.IsClickThrough = value);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ApplyClickThrough();
    }

    private void ApplyClickThrough()
    {
        // WPF windows with AllowsTransparency are already layered; adding
        // WS_EX_TRANSPARENT makes Windows route mouse input to the window below.
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
            return; // Not created yet; OnSourceInitialized applies it.

        int style = GetWindowLong(hwnd, GwlExStyle);
        int updated = _settings.IsClickThrough ? style | WsExTransparent : style & ~WsExTransparent;
        if (updated != style)
            SetWindowLong(hwnd, GwlExStyle, updated);
    }

    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x20;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private void ResetPositionMenuItem_Click(object sender, RoutedEventArgs e)
    {
        UpdateSettings(s =>
        {
            s.Left = null;
            s.Top = null;
        });
        PositionWindow();
    }

    // Only hides: the tray icon keeps running, so it can be shown again from there.
    // Buttons inside a menu don't close it on their own, hence closing it here.
    private void HideMenuItem_Click(object sender, RoutedEventArgs e)
    {
        RootBorder.ContextMenu.IsOpen = false;
        Hide();
    }

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        _hardwareMonitor.Dispose();

        // Width changes may have shifted a user-placed widget; remember where it ended up.
        if (_settings.Left.HasValue)
        {
            _settings.Left = Left;
            _settings.Top = Top;
            _settings.Save();
        }

        base.OnClosed(e);
    }
}
