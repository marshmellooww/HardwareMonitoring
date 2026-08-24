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

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1.5),
        };
        _timer.Tick += (_, _) => UpdateReadings();
        _timer.Start();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        PositionWindow();
        UpdateReadings();
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
        SetTemperature(CpuTempText, _hardwareMonitor.CpuTemperature);
        SetTemperature(GpuTempText, _hardwareMonitor.GpuTemperature);
    }

    private static void SetTemperature(TextBlock block, float? value)
    {
        if (!value.HasValue)
        {
            block.Text = "N/A";
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
