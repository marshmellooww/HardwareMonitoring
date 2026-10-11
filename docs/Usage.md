# Usage

## The widget

On first launch, the widget appears in the top-right corner of your main screen. Drag it anywhere with the left mouse button and it stays there next time.

Each row is one reading:

| Row | Shows | Green | Orange | Red |
|---|---|---|---|---|
| CPU | Processor temperature (core average or package) | ≤ 60 °C | ≤ 80 °C | above 80 °C |
| GPU | Core temperature | ≤ 60 °C | ≤ 80 °C | above 80 °C |
| RAM | Used / total physical memory | ≤ 60% used | ≤ 85% used | above 85% |
| VRAM | Used / total graphics memory | ≤ 60% used | ≤ 85% used | above 85% |
| DISK C, DISK D, … | Free / total space | ≥ 20% free | ≥ 10% free | below 10% free |

A gray **N/A** means the reading isn't available on this PC. A gray **No driver** on the CPU row means PawnIO isn't installed (see [Troubleshooting](Troubleshooting.md#cpu-shows-no-driver)).

There's one **DISK** row per internal drive, found automatically. Plug in or remove a drive and the rows update without a restart. USB sticks and optical drives aren't shown. Hover over a disk row to see its Windows drive name.

## Right-click menu

| Option | What it does |
|---|---|
| **Sensors ▸** | Tick which rows to show. You can't untick the last one, because an empty widget couldn't be right-clicked anymore. |
| **Refresh rate ▸** | How often readings update: 1–5 seconds. Higher values use less CPU. |
| **Layout ▸** | **Vertical** (a column) or **Horizontal** (one row). |
| **Size ▸** | Extra small (70%), Small (85%), Normal, Large (125%). |
| **Opacity ▸** | 100%, 85%, 70% or 50%. |
| **Always on top** | Keep the widget above other windows. |
| **Lock position** | Stop the widget from being dragged by accident. |
| **Click-through** | Clicks pass through the widget to whatever is behind it. Turn it off from the tray icon (see below). |
| **Start with Windows** | Open automatically when you sign in. On by default. |
| **Reset position** | Move the widget back to the top-right corner. |
| **Task Manager** | Opens Windows Task Manager (with admin rights). |
| **Hide** \| **Exit** | **Hide** tucks the widget away and keeps the app in the tray. **Exit** quits completely. |

When the widget changes width (for example when switching layout), it grows **away from the nearest screen edge**, so a widget docked on the right stays on screen.

## Tray icon

The app lives in the notification area (the **^** arrow next to the clock). Drag the icon onto the taskbar if you want it always visible.

| Action | Result |
|---|---|
| Left-click | Shows the widget if hidden, or brings it to the front |
| Right-click → **Hide widget / Show widget** | Toggles the widget |
| Right-click → **Click-through** | Toggles click-through (this is how you turn it **off**) |
| Right-click → **Exit** | Quits the app |

## The settings file

Every choice is saved immediately to:

```
%AppData%\HardwareMonitor\settings.json
```

(typically `C:\Users\<you>\AppData\Roaming\HardwareMonitor\settings.json`). Example:

```json
{
  "IsHorizontal": false,
  "Scale": 0.85,
  "Opacity": 1,
  "Topmost": true,
  "IsPositionLocked": false,
  "IsClickThrough": false,
  "StartWithWindows": true,
  "RefreshSeconds": 1,
  "HiddenStats": [ "DISK D" ],
  "Left": 1750,
  "Top": 23
}
```

| Field | Meaning |
|---|---|
| `IsHorizontal` | `true` for the horizontal layout |
| `Scale` | Size multiplier (clamped to 0.5–2) |
| `Opacity` | Widget opacity (clamped to 0.2–1) |
| `Topmost` | Always on top |
| `IsPositionLocked` | Dragging disabled |
| `IsClickThrough` | Click-through mode |
| `StartWithWindows` | Launch at sign-in |
| `RefreshSeconds` | Seconds between readings (clamped to 1–5) |
| `HiddenStats` | Rows turned off: `CPU`, `GPU`, `RAM`, `VRAM`, `DISK C`, … |
| `Left`, `Top` | Saved position. Remove both (or use **Reset position**) to return to the default corner. |

You can edit the file by hand **while the app is closed**. Out-of-range values are pulled back into range, and a broken or missing file just means defaults. **Deleting the file resets everything.**
