# Updating

How to get the latest version after you've already cloned and built the app once (see [Building](Building.md)). Your settings and the **Start with Windows** entry carry over automatically.

## The short version

1. **Exit the app**: right-click the tray icon → **Exit**. (**Hide** isn't enough; the app has to be fully closed.)
2. Open a terminal in the folder you cloned into (the one containing `README.md`) and run:

```bash
git pull
cd HardwareMonitor
dotnet build -c Release
```

3. Start it again. In **Command Prompt**:

```bash
start "" "HardwareMonitor\bin\Release\net9.0-windows\HardwareMonitor.exe"
```

In **PowerShell**:

```powershell
Start-Process .\HardwareMonitor\bin\Release\net9.0-windows\HardwareMonitor.exe
```

Or double-click the `.exe`, or use your pinned shortcut. A shortcut still works, because the new build replaces the old `.exe` at the same path.

## What each step does

| Step | Why |
|---|---|
| **Exit the app** | Windows locks a running `.exe` and the DLLs next to it. If the app is still running, the build fails with *"file is locked"* / *"being used by another process"*. Remember it may have been started automatically at sign-in. |
| `git pull` | Downloads the latest commits from GitHub and updates your copy of the source code. |
| `dotnet build -c Release` | Rebuilds the app into the same `bin\Release\net9.0-windows\` folder, overwriting the old version. New or updated NuGet packages are downloaded automatically. |
| Start it | Runs the new version. It asks for admin rights as usual. |

## What carries over

- **Your settings.** They live in `%AppData%\HardwareMonitor\settings.json`, outside the repo, so updating never touches them. Settings that a new version adds start at their default values.
- **Start with Windows.** The scheduled task points at the `.exe` path, which doesn't change, and it's refreshed every time the app starts anyway.
- **PawnIO.** It's installed separately, so you don't need to reinstall it. To update PawnIO itself: `winget upgrade namazso.PawnIO`.

## See what's new before updating

```bash
git fetch
git log --oneline HEAD..origin/main
```

`git fetch` downloads the new commits without changing your files. The `git log` line lists the commits you don't have yet. If it prints nothing, you're already up to date.

## If something goes wrong

### `git pull` says your local changes would be overwritten

You edited files in your copy. Either keep your changes and reapply them on top of the update:

```bash
git stash
git pull
git stash pop
```

…or throw your changes away and take the update as-is. **This permanently deletes your edits:**

```bash
git reset --hard
git pull
```

### The build fails with "file is locked" / "being used by another process"

The app is still running. Exit it from the tray icon and build again. If the tray icon isn't visible, it's behind the **^** arrow next to the clock.

Because the app runs as administrator, `taskkill /IM HardwareMonitor.exe` only works from a terminal that was opened **as administrator**. From a normal terminal it fails with *"Access is denied"*.

### The build fails with strange errors after updating

Clear the old build output and build again:

```bash
dotnet clean -c Release
dotnet build -c Release
```

If the error mentions an SDK or framework version, the update may need a newer .NET SDK. Check `<TargetFramework>` in `HardwareMonitor/HardwareMonitor/HardwareMonitor.csproj` and install the matching SDK (see [Building → Prerequisites](Building.md#prerequisites)).

### The new version misbehaves

Go back to the version you had before. Find it with `git log --oneline`, then:

```bash
git checkout <commit-id>
cd HardwareMonitor
dotnet build -c Release
```

When a fixed version is out, return to the latest with `git checkout main`, then `git pull` and build again.

## Updating a published copy

If you made a shareable copy with `dotnet publish` (see [Building → Publish](Building.md#publish-a-copy-to-share)), update the source as above, exit the running copy, then run **the same publish command again with the same `-o` folder**. It overwrites the old files in place.

If you copied that folder to another PC, copy the new folder over the old one there too, again with the app exited first.
