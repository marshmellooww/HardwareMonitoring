using System.Management;
using LibreHardwareMonitor.Hardware;

namespace HardwareMonitor.Services;

/// <summary>
/// Thin wrapper around LibreHardwareMonitorLib exposing just the CPU and GPU
/// temperature readings this app needs.
/// </summary>
public sealed class HardwareMonitorService : IDisposable
{
    private static readonly HardwareType[] GpuHardwareTypes =
    {
        HardwareType.GpuNvidia, HardwareType.GpuAmd, HardwareType.GpuIntel,
    };

    private readonly Computer _computer;
    private readonly UpdateVisitor _visitor = new();

    public float? CpuTemperature { get; private set; }
    public float? GpuTemperature { get; private set; }

    public HardwareMonitorService()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
        };
        _computer.Open();
    }

    public void Refresh()
    {
        _computer.Accept(_visitor);

        // LibreHardwareMonitor needs its kernel driver to read CPU MSR sensors
        // directly. On machines where that driver is blocked (e.g. corporate
        // EDR policies), fall back to the ACPI thermal zone exposed via WMI.
        CpuTemperature = FindTemperature(
            new[] { HardwareType.Cpu },
            s => s.Name.Contains("Package", StringComparison.OrdinalIgnoreCase)
                 || s.Name.Contains("Average", StringComparison.OrdinalIgnoreCase))
            ?? GetAcpiThermalZoneTemperature();

        GpuTemperature = FindTemperature(
            GpuHardwareTypes,
            s => s.Name.Contains("Core", StringComparison.OrdinalIgnoreCase)
                 || s.Name.Contains("Hot Spot", StringComparison.OrdinalIgnoreCase));
    }

    private float? FindTemperature(IEnumerable<HardwareType> hardwareTypes, Func<ISensor, bool> preferred)
    {
        foreach (HardwareType type in hardwareTypes)
        {
            foreach (IHardware hardware in _computer.Hardware.Where(h => h.HardwareType == type))
            {
                List<ISensor> sensors = hardware.Sensors
                    .Where(s => s.SensorType == SensorType.Temperature && s.Value.HasValue)
                    .ToList();

                if (sensors.Count == 0)
                    continue;

                ISensor chosen = sensors.FirstOrDefault(preferred) ?? sensors[0];
                return chosen.Value;
            }
        }

        return null;
    }

    private static float? GetAcpiThermalZoneTemperature()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\WMI",
                "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");

            foreach (ManagementBaseObject obj in searcher.Get())
            {
                using (obj)
                {
                    // Value is in tenths of a Kelvin.
                    uint tenthsKelvin = Convert.ToUInt32(obj["CurrentTemperature"]);
                    float celsius = (tenthsKelvin / 10f) - 273.15f;
                    if (celsius is > 0 and < 110)
                        return celsius;
                }
            }
        }
        catch
        {
            // No ACPI thermal zone available on this system.
        }

        return null;
    }

    public void Dispose() => _computer.Close();
}
