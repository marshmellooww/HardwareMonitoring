using System.IO;
using System.Management;
using LibreHardwareMonitor.Hardware;

namespace HardwareMonitor.Services;

/// <summary>
/// Thin wrapper around LibreHardwareMonitorLib exposing CPU/GPU temperature,
/// RAM/VRAM usage, and system-drive free space.
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
    public float? RamUsedGb { get; private set; }
    public float? RamTotalGb { get; private set; }
    public float? VramUsedGb { get; private set; }
    public float? VramTotalGb { get; private set; }
    public double? DiskFreeGb { get; private set; }
    public double? DiskTotalGb { get; private set; }

    public HardwareMonitorService()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
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

        UpdateMemory();
        UpdateVram();
        UpdateDisk();
    }

    private void UpdateMemory()
    {
        IHardware? memory = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Memory);

        float? used = memory?.Sensors.FirstOrDefault(
            s => s.SensorType == SensorType.Data && s.Name == "Memory Used")?.Value;
        float? available = memory?.Sensors.FirstOrDefault(
            s => s.SensorType == SensorType.Data && s.Name == "Memory Available")?.Value;

        RamUsedGb = used;
        RamTotalGb = used.HasValue && available.HasValue ? used + available : null;
    }

    private void UpdateVram()
    {
        foreach (HardwareType type in GpuHardwareTypes)
        {
            IHardware? gpu = _computer.Hardware.FirstOrDefault(h => h.HardwareType == type);
            if (gpu == null)
                continue;

            float? usedMb = gpu.Sensors.FirstOrDefault(
                s => s.SensorType == SensorType.SmallData && s.Name == "GPU Memory Used")?.Value;
            float? totalMb = gpu.Sensors.FirstOrDefault(
                s => s.SensorType == SensorType.SmallData && s.Name == "GPU Memory Total")?.Value;

            if (usedMb.HasValue && totalMb.HasValue)
            {
                VramUsedGb = usedMb.Value / 1024f;
                VramTotalGb = totalMb.Value / 1024f;
                return;
            }
        }

        VramUsedGb = null;
        VramTotalGb = null;
    }

    private void UpdateDisk()
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\");
            if (drive.IsReady)
            {
                DiskFreeGb = drive.AvailableFreeSpace / 1024d / 1024d / 1024d;
                DiskTotalGb = drive.TotalSize / 1024d / 1024d / 1024d;
                return;
            }
        }
        catch (IOException)
        {
            // Drive not accessible right now; report unknown below.
        }

        DiskFreeGb = null;
        DiskTotalGb = null;
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
