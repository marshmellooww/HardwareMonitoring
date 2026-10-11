# Building

Everything here works from a plain terminal (Windows Terminal, PowerShell or Command Prompt). Visual Studio or Rider are optional.

## Prerequisites

| What | Why | Get it |
|---|---|---|
| Windows 10 or 11, 64-bit | WPF and the sensor drivers are Windows-only | — |
| .NET 9 SDK | Compiles the app | `winget install Microsoft.DotNet.SDK.9` or [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/9.0) |
| Git | Cloning the repo | `winget install Git.Git` |
| PawnIO *(optional)* | CPU temperature | `winget install namazso.PawnIO` |

Check the SDK is installed:

```bash
dotnet --list-sdks
```

You should see a line starting with `9.`.

## Clone

```bash
git clone https://github.com/marshmellooww/HardwareMonitoring.git
cd HardwareMonitoring/HardwareMonitor
```

All commands below are run from this `HardwareMonitoring/HardwareMonitor` folder, the one containing `HardwareMonitor.sln`.

## Build

```bash
dotnet build -c Release
```

What this does:

- `dotnet build` restores NuGet packages (LibreHardwareMonitorLib and its dependencies) on the first run, then compiles the C# and XAML.
- `-c Release` builds the optimized configuration. Leave it out for a **Debug** build, which is unoptimized but easier to debug.

The result lands in:

```
HardwareMonitor/bin/Release/net9.0-windows/HardwareMonitor.exe
```

(or `bin/Debug/...` for a Debug build). Double-click the `.exe` and accept the admin prompt.

> **Close the running app before rebuilding.** Windows locks a running `.exe`, so the build fails to overwrite it.

## Running it

The app's manifest requires administrator rights, so it has to be started in a way that can show the Windows admin (UAC) prompt.

**Command Prompt:**

```bash
start "" "HardwareMonitor\bin\Release\net9.0-windows\HardwareMonitor.exe"
```

**PowerShell:**

```powershell
Start-Process .\HardwareMonitor\bin\Release\net9.0-windows\HardwareMonitor.exe
```

Both launch the `.exe` the same way a double-click in File Explorer does, so Windows shows the admin prompt.

### Why `dotnet run` fails from a normal terminal

```bash
dotnet run -c Release --project HardwareMonitor
```

From a normal terminal this fails with *"An error occurred trying to start process … The requested operation requires elevation"* (in Turkish Windows: *"İstenen işlem için yükseltme gerekiyor"*). `dotnet run` starts the app directly, a way that can't trigger the admin prompt, so Windows refuses. The build itself succeeded; only the launch failed.

It **does** work from a terminal that's already elevated: right-click Windows Terminal or Command Prompt → **Run as administrator**. (`--project` points at the app's folder, because the current folder holds the solution rather than the project.)

## Publish a copy to share

`dotnet build` output is meant for development. To put the app in a folder you can zip up and copy to another PC, publish it.

**Small (~15 MB), needs the .NET 9 Desktop Runtime on the target PC:**

```bash
dotnet publish HardwareMonitor -c Release -o publish
```

The other PC needs the [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0) (`winget install Microsoft.DotNet.DesktopRuntime.9`).

**Standalone (~175 MB), runs on any 64-bit Windows PC with nothing installed:**

```bash
dotnet publish HardwareMonitor -c Release -r win-x64 --self-contained true -o publish
```

Either way, copy the **whole `publish` folder** and run `HardwareMonitor.exe` from it.

> **Single-file publishing (`-p:PublishSingleFile=true`) isn't supported yet.** The tray icon is loaded from the `Assets` folder next to the `.exe`, and a single-file build doesn't include that folder, so the app fails at startup.

## Using an IDE instead

Open `HardwareMonitor/HardwareMonitor.sln` in Visual Studio 2022 (17.12+) or JetBrains Rider and run it. Because the app requests administrator rights, either start the IDE as administrator or accept the UAC prompt when the app launches.

## Tips

- **"Start with Windows" follows the last `.exe` you ran.** If you switch between Debug and Release builds, the most recently launched one is what opens at sign-in.
- **Clean build:** `dotnet clean` removes the build output if something seems stale.
