# Troubleshooting

## CPU shows "No driver"

The PawnIO driver, which is needed to read CPU temperature, isn't installed. Install it, then restart Hardware Monitor:

```bash
winget install namazso.PawnIO
```

Or download it from [pawnio.eu](https://pawnio.eu). If the installer asks for a reboot, reboot first.

## CPU shows "N/A" even with PawnIO installed

- Make sure the app is running **as administrator**. It asks automatically; if you declined the prompt, it won't start at all.
- Some CPUs or virtual machines don't expose temperature sensors. Other tools based on LibreHardwareMonitor will show N/A there too.

## GPU, VRAM or a disk shows "N/A"

That reading isn't available right now. For example, a GPU without a supported driver doesn't report temperature. A disk row disappears entirely if the drive is removed or not ready.

## The widget doesn't show up over my game

The widget can only appear over games in **Borderless** or **Windowed** mode. In exclusive **Fullscreen**, the game owns the whole screen, and no overlay app can draw on top of it except the GPU vendor's own. Switch the game's display mode to Borderless.

Also check **Always on top** is ticked in the right-click menu.

## I can't right-click the widget anymore

**Click-through** is probably on. Right-click the **tray icon** and untick **Click-through**.

## The widget disappeared

It's probably hidden or behind other windows. Left-click the tray icon to show it and bring it to the front. If it's off-screen (for example after unplugging a monitor), right-click the tray icon to show it, then use the widget's **Reset position**. Saved positions that aren't on any connected screen are ignored at startup anyway.

## It doesn't start when I sign in

- Check **Start with Windows** is ticked in the right-click menu.
- Open Task Scheduler (`Win+R` → `taskschd.msc`) and look for a task named **HardwareMonitor** in the library root. Its action should point at the `.exe` you actually use. The task is updated every time you launch the app, so starting the right `.exe` once fixes a stale path.
- If you moved or deleted the build folder, launch the app from its new location once.

## Two widgets appear

The app doesn't yet prevent a second copy from starting. If it's already running from startup and you launch the `.exe` again, you get two. Exit one from its tray icon.

## The build fails with "file is locked" / "being used by another process"

The app is running from the folder you're building into. Exit it (tray icon → **Exit**), then build again.

## The old icon still shows on the .exe

Windows caches file icons. It refreshes on its own after a while, or after signing out or restarting.

## Reset everything

Exit the app, delete `%AppData%\HardwareMonitor\settings.json`, and start it again. To also remove the startup entry, untick **Start with Windows** first, or delete the **HardwareMonitor** task in Task Scheduler.

## Brief stutters every refresh

Each refresh asks the GPU driver for its sensors, which takes roughly 90 ms on an NVIDIA card. This runs on a background thread, so the widget itself stays smooth. If you notice system-wide hitches in games, raise **Refresh rate** to 2–5 seconds.
