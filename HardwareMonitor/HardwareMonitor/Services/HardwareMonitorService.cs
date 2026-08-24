using System.IO;
using System.Runtime.InteropServices;
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
        };
        _computer.Open();
    }

    public void Refresh()
    {
        _computer.Accept(_visitor);

        // Requires LibreHardwareMonitor's kernel driver to read CPU MSR sensors
        // directly, which Windows blocks when Memory Integrity (Core Isolation)
        // is on. There's no reliable fallback: MSAcpi_ThermalZoneTemperature via
        // WMI often reports an unrelated ambient/chassis probe instead of the
        // CPU die, so a plausible-looking wrong number is worse than N/A here.
        CpuTemperature = FindTemperature(
            new[] { HardwareType.Cpu },
            s => s.Name.Contains("Package", StringComparison.OrdinalIgnoreCase)
                 || s.Name.Contains("Average", StringComparison.OrdinalIgnoreCase));

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
        // LibreHardwareMonitor's Memory sensors report commit charge (RAM +
        // page file), not physical RAM, so read physical RAM directly instead.
        var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (GlobalMemoryStatusEx(ref status))
        {
            RamTotalGb = status.ullTotalPhys / 1024f / 1024f / 1024f;
            RamUsedGb = (status.ullTotalPhys - status.ullAvailPhys) / 1024f / 1024f / 1024f;
        }
        else
        {
            RamTotalGb = null;
            RamUsedGb = null;
        }
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

    public void Dispose() => _computer.Close();

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}
