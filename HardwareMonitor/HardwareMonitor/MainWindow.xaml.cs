using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
    private readonly DispatcherTimer _timer;

    public MainWindow()
    {
        InitializeComponent();

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
        PositionWindow();
    }

    private void PositionWindow()
    {
        Rect workArea = SystemParameters.WorkArea;
        Left = workArea.Right - ActualWidth - EdgeMargin;
        Top = workArea.Top + EdgeMargin;
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
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        _hardwareMonitor.Dispose();
        base.OnClosed(e);
    }
}
