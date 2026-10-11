using System.Globalization;
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
    private readonly DispatcherTimer _timer;
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

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _timer.Tick += (_, _) => UpdateReadings();
        _timer.Start();
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

        ApplyLayout(_settings.IsHorizontal);
        ContentScale.ScaleX = ContentScale.ScaleY = _settings.Scale;
        Opacity = _settings.Opacity;
        Topmost = _settings.Topmost;

        VerticalLayoutItem.IsChecked = !_settings.IsHorizontal;
        HorizontalLayoutItem.IsChecked = _settings.IsHorizontal;
        CheckItemWithTag(SizeMenu, _settings.Scale);
        CheckItemWithTag(OpacityMenu, _settings.Opacity);
        TopmostItem.IsChecked = _settings.Topmost;
        LockPositionItem.IsChecked = _settings.IsPositionLocked;
    }

    private void ApplyLayout(bool horizontal)
    {
        StatsPanel.Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical;

        // Shared column widths line values up in a column; in a single row they
        // would just pad every stat out to the widest one.
        Grid.SetIsSharedSizeScope(StatsPanel, !horizontal);

        Thickness spacing = horizontal ? new Thickness(0, 0, 18, 0) : new Thickness(0, 0, 0, 4);
        UIElementCollection stats = StatsPanel.Children;
        for (int i = 0; i < stats.Count; i++)
            ((FrameworkElement)stats[i]).Margin = i < stats.Count - 1 ? spacing : new Thickness(0);
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

    private void LayoutMenuItem_Click(object sender, RoutedEventArgs e) =>
        UpdateSettings(s => s.IsHorizontal = sender == HorizontalLayoutItem);

    private void SizeMenuItem_Click(object sender, RoutedEventArgs e) =>
        UpdateSettings(s => s.Scale = ParseTag(sender));

    private void OpacityMenuItem_Click(object sender, RoutedEventArgs e) =>
        UpdateSettings(s => s.Opacity = ParseTag(sender));

    private void TopmostMenuItem_Click(object sender, RoutedEventArgs e) =>
        UpdateSettings(s => s.Topmost = !s.Topmost);

    private void LockPositionMenuItem_Click(object sender, RoutedEventArgs e) =>
        UpdateSettings(s => s.IsPositionLocked = !s.IsPositionLocked);

    private void ResetPositionMenuItem_Click(object sender, RoutedEventArgs e)
    {
        UpdateSettings(s =>
        {
            s.Left = null;
            s.Top = null;
        });
        PositionWindow();
    }

    // Only hides: the tray icon keeps running and its menu is where the app exits.
    private void HideMenuItem_Click(object sender, RoutedEventArgs e) => Hide();

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
