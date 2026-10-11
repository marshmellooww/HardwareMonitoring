# Hardware Monitor

A small, always-on-top overlay for Windows that shows your PC's vital signs at a glance:

- **CPU** and **GPU** temperature
- **RAM** and **VRAM** usage
- Free space on **every internal drive** (C:, D:, …)

Readings are color-coded: **green** is fine, **orange** is getting warm/full, **red** needs attention. Everything is configured from the widget's right-click menu, and your choices are remembered.

Built with WPF on .NET 9, using [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) to read the sensors.

## Features

- **Vertical or horizontal layout**: a compact column, or a single row across the top of your screen
- **Pick your sensors**: show only what you care about (e.g. just CPU and GPU)
- **Five sizes** (Extra small to Large) and adjustable opacity
- **Refresh rate** from 1 to 5 seconds
- **Click-through mode**: the widget stays visible while clicks pass through to the game or app underneath
- **Start with Windows**, with admin rights and no UAC prompt at sign-in
- **Tray icon** to show/hide the widget, toggle click-through, and exit
- **Remembers everything**: position, layout, size, sensors and more
- Quick shortcut to **Task Manager**

## Quick start

You need **Windows 10/11 (64-bit)** and the **[.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)**. No IDE is required.

```bash
git clone https://github.com/marshmellooww/HardwareMonitoring.git
cd HardwareMonitoring/HardwareMonitor
dotnet build -c Release
```

Then start it. In **Command Prompt**:

```bash
start "" "HardwareMonitor\bin\Release\net9.0-windows\HardwareMonitor.exe"
```

In **PowerShell**:

```powershell
Start-Process .\HardwareMonitor\bin\Release\net9.0-windows\HardwareMonitor.exe
```

Or just double-click that `.exe` in File Explorer. Windows will ask for administrator permission. That's expected (see [Why admin?](#why-does-it-need-administrator-rights)).

> `dotnet run` only works from a terminal opened **as administrator**. From a normal terminal it fails with *"The requested operation requires elevation"*, because it can't show the admin prompt. See [Building](docs/Building.md#running-it).

### For CPU temperature: install PawnIO

CPU temperature needs the free **PawnIO** driver. Without it, the CPU row shows **"No driver"** and everything else still works.

```bash
winget install namazso.PawnIO
```

Or download it from [pawnio.eu](https://pawnio.eu). Restart Hardware Monitor afterwards.

## Using it

| Action | How |
|---|---|
| Move the widget | Drag it with the left mouse button |
| Open the options | Right-click the widget |
| Hide the widget | Right-click → **Hide** (the app keeps running in the tray) |
| Bring it back | Left-click the tray icon |
| Turn off click-through | Right-click the tray icon → **Click-through** |
| Quit completely | Right-click → **Exit**, or tray icon → **Exit** |

See **[docs/Usage.md](docs/Usage.md)** for every menu option and the settings file.

## Documentation

| Page | What's in it |
|---|---|
| [Building](docs/Building.md) | Prerequisites, building and running from the command line, publishing a shareable copy |
| [Usage](docs/Usage.md) | Every menu option, color thresholds, the settings file |
| [How it works](docs/How-It-Works.md) | Architecture, how sensors are read, settings, startup task, click-through |
| [Troubleshooting](docs/Troubleshooting.md) | "No driver", missing widget in games, startup issues, resetting settings |

## Why does it need administrator rights?

Reading CPU temperature means talking to the processor through a kernel driver (PawnIO), and Windows only allows that from an elevated process. The app's manifest therefore asks for admin rights every time it starts. When **Start with Windows** is on, a scheduled task launches it elevated at sign-in, so you aren't prompted then.

## Project layout

```
HardwareMonitoring/
├── README.md
├── docs/                         Extended documentation (the "wiki")
└── HardwareMonitor/
    ├── HardwareMonitor.sln
    └── HardwareMonitor/          The WPF app
        ├── App.xaml(.cs)         Startup and tray icon
        ├── MainWindow.xaml(.cs)  The widget and its right-click menu
        ├── AppSettings.cs        Saved preferences (settings.json)
        ├── Services/
        │   ├── HardwareMonitorService.cs   Reads sensors (LibreHardwareMonitor)
        │   ├── UpdateVisitor.cs            Refreshes every hardware node
        │   └── StartupTask.cs              "Start with Windows" scheduled task
        ├── Assets/tray.ico       App and tray icon
        └── app.manifest          Requests administrator rights
```
