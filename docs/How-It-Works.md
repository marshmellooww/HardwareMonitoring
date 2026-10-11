# How it works

A tour of the code for anyone who wants to change it. All paths are relative to `HardwareMonitor/HardwareMonitor/`.

## Big picture

```
 App.xaml.cs ─────────── tray icon (show/hide, click-through, exit)
     │
     └─ MainWindow ───── the widget: rows, right-click menu, dragging, layout
          │   ▲
          │   │ readings (every 1–5 s, read on a background thread)
          ▼   │
     HardwareMonitorService ── LibreHardwareMonitor ── PawnIO driver (CPU)
          │                                       └── GPU vendor APIs
          ├── Windows memory API (RAM)
          └── DriveInfo (disks)

 AppSettings ──── %AppData%\HardwareMonitor\settings.json
 StartupTask ──── Task Scheduler task "HardwareMonitor" (Start with Windows)
```

## Startup and lifetime: `App.xaml(.cs)`

`App.xaml` opens `MainWindow` on startup. `App.OnStartup` creates the **tray icon** (a WinForms `NotifyIcon`, since WPF has none) and its menu.

The tray icon defines the app's lifetime. **Hide** on the widget only hides the window, and the app keeps running until **Exit** is chosen. The icon is loaded at `SystemInformation.SmallIconSize`, so Windows uses the matching 16/20/24 px frame from `tray.ico` instead of shrinking a larger one, which looks blurry.

## Reading sensors: `Services/HardwareMonitorService.cs`

| Reading | Source |
|---|---|
| CPU temperature | LibreHardwareMonitor CPU sensors. Prefers "Package"/"Average" sensors. |
| GPU temperature | LibreHardwareMonitor GPU sensors (NVIDIA, AMD or Intel). Prefers "Core"/"Hot Spot". |
| VRAM | LibreHardwareMonitor's "GPU Memory Used/Total" sensors |
| RAM | `GlobalMemoryStatusEx` (Windows API) directly. LibreHardwareMonitor's memory sensors include the page file, which isn't physical RAM. |
| Disks | `DriveInfo.GetDrives()`, filtered to fixed (internal) drives that are ready. Re-scanned every refresh. |

`Refresh()` asks LibreHardwareMonitor to update every hardware node (through `UpdateVisitor`), then reads the values out into simple properties (`CpuTemperature`, `Disks`, …) that the UI displays.

### CPU temperature and PawnIO

Modern CPUs report temperature through model-specific registers (MSRs), which only kernel code can read. The LibreHardwareMonitor version used here does that through **PawnIO**, a signed driver that works with Windows' Memory Integrity security feature. Older versions used WinRing0, which Windows now blocks.

- No PawnIO installed → every CPU temperature sensor exists but has no value → the widget shows **No driver**. `HardwareMonitorService.IsPawnIoInstalled` reports this.
- PawnIO only accepts calls from administrators, which is why `app.manifest` sets `requireAdministrator`.

There's deliberately **no fallback** to the WMI `MSAcpi_ThermalZoneTemperature` value. On many boards it reports a motherboard/ambient probe, not the CPU, and a plausible-looking wrong number is worse than "N/A".

### Why reading happens on a background thread

A full refresh takes roughly 90–100 ms, almost all of it in the NVIDIA GPU query. Running that on the UI thread froze the widget for that long on every tick, which you noticed most while dragging.

`MainWindow.Timer_Tick` therefore runs `Refresh()` with `Task.Run`, awaits it, and only then updates the text on the UI thread (`ShowReadings`). A tick is skipped if the previous read hasn't finished, so reads never pile up. On exit, the app waits for an in-progress read before closing the sensors.

The first read happens synchronously in `Window_Loaded`, before the window is shown, so the widget opens at its real size and position.

## The widget: `MainWindow.xaml(.cs)`

- **Rows** are small two-column `Grid`s (label, value) inside `StatsPanel`. In vertical layout, the columns use `SharedSizeGroup` so all values line up. In horizontal layout, the shared sizing is switched off so each stat only takes the width it needs (`ApplyLayout`).
- **Disk rows** are created in code by `SyncDiskRows`, one per drive, together with a matching **Sensors** menu entry. They're rebuilt only when the set of drive letters changes.
- **Hiding sensors**: each row and menu entry share a `Tag` (`"CPU"`, `"DISK C"`, …). `AppSettings.HiddenStats` holds the hidden tags.
- **Styling**: the dark context menu, its separators and the Hide/Exit button row are custom `ControlTemplate`s in `Window.Resources`. Icons come from the *Segoe Fluent Icons* font (falling back to *Segoe MDL2 Assets*).
- **Size** is a `ScaleTransform` on the root border. **Opacity** is the window's opacity.
- **Edge anchoring**: `Window_SizeChanged` moves the window when its width changes, keeping the edge nearest the screen side in place.

### Click-through

WPF windows with `AllowsTransparency="True"` are already Win32 *layered* windows. Adding the `WS_EX_TRANSPARENT` extended style (`ApplyClickThrough`) makes Windows deliver mouse input to whatever is underneath. The widget can't receive a right-click in this mode, so the tray menu is the way to turn it off.

## Settings: `AppSettings.cs`

A plain class serialized with `System.Text.Json`:

1. `AppSettings.Load()` runs when the window is created. A missing or corrupt file gives defaults.
2. `MainWindow.ApplySettings()` applies every value: layout, scale, opacity, timer interval, menu checkmarks and so on.
3. Every menu click goes through `UpdateSettings(...)`, which changes the value, re-applies everything and calls `Save()`.
4. Dragging saves the new position. On exit the final position is saved again, in case width changes shifted it.

To add a setting: add a property to `AppSettings`, apply it in `ApplySettings`, and add a menu item that calls `UpdateSettings`.

## Start with Windows: `Services/StartupTask.cs`

The usual `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` registry key doesn't work here: Windows silently skips programs that need elevation. Instead, the app registers a **Task Scheduler** task named `HardwareMonitor`:

- Trigger: when *you* sign in, after a 5-second delay so the taskbar is ready
- Runs with highest privileges, so it starts elevated without a UAC prompt
- No execution time limit, and no stopping on battery (Windows' defaults would kill it after 72 hours)

The task is created from an XML definition with `schtasks.exe /Create /XML`, off the UI thread. It's re-registered on every launch, so it always points at the `.exe` that ran last. Unticking **Start with Windows** deletes it. You can inspect it in Task Scheduler (`taskschd.msc`).

## The icon: `Assets/tray.ico`

The icon is a multi-resolution `.ico` (16, 20, 24, 32, 40, 48, 64, 128 and 256 px). Each size was drawn separately and snapped to whole pixels rather than downscaled, which keeps the small tray sizes sharp. It's used both as the tray icon and as the `.exe` icon (`ApplicationIcon` in the `.csproj`).
