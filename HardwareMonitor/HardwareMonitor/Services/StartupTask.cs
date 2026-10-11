using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Principal;
using System.Text;

namespace HardwareMonitor.Services;

/// <summary>
/// Starts the app when the user signs in, via a Task Scheduler task.
/// </summary>
/// <remarks>
/// The usual HKCU\...\Run key doesn't work here: the app requires elevation
/// (see app.manifest), and Windows silently skips elevated apps listed there.
/// A logon task with "run with highest privileges" starts it elevated without
/// a UAC prompt.
/// </remarks>
internal static class StartupTask
{
    private const string TaskName = "HardwareMonitor";
    private static readonly object Gate = new();

    /// <summary>Creates or removes the logon task to match <paramref name="enabled"/>.</summary>
    public static void Apply(bool enabled)
    {
        lock (Gate)
        {
            if (enabled)
                Register();
            else if (Exists())
                RunSchtasks($"/Delete /TN \"{TaskName}\" /F");
        }
    }

    private static bool Exists() => RunSchtasks($"/Query /TN \"{TaskName}\"") == 0;

    private static void Register()
    {
        string? exePath = Environment.ProcessPath;
        if (exePath == null)
            return;

        // Re-registered on every launch so the task follows the exe if it moves
        // (e.g. switching between Debug and Release builds).
        string xmlPath = Path.Combine(Path.GetTempPath(), $"HardwareMonitor-task-{Guid.NewGuid():N}.xml");
        try
        {
            File.WriteAllText(xmlPath, BuildTaskXml(exePath), Encoding.Unicode);
            RunSchtasks($"/Create /TN \"{TaskName}\" /XML \"{xmlPath}\" /F");
        }
        catch (IOException)
        {
            // Couldn't write the temp file; startup registration is best-effort.
        }
        finally
        {
            File.Delete(xmlPath);
        }
    }

    private static string BuildTaskXml(string exePath)
    {
        string user = SecurityElement.Escape(WindowsIdentity.GetCurrent().Name);
        string command = SecurityElement.Escape(exePath);
        string workingDirectory = SecurityElement.Escape(Path.GetDirectoryName(exePath) ?? "");

        // Unlike schtasks' command-line defaults, this has no 72-hour run limit
        // and doesn't stop on battery power.
        return $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo>
                <Description>Starts Hardware Monitor when you sign in.</Description>
              </RegistrationInfo>
              <Triggers>
                <LogonTrigger>
                  <Enabled>true</Enabled>
                  <UserId>{user}</UserId>
                  <Delay>PT5S</Delay>
                </LogonTrigger>
              </Triggers>
              <Principals>
                <Principal id="Author">
                  <UserId>{user}</UserId>
                  <LogonType>InteractiveToken</LogonType>
                  <RunLevel>HighestAvailable</RunLevel>
                </Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <Priority>7</Priority>
              </Settings>
              <Actions Context="Author">
                <Exec>
                  <Command>{command}</Command>
                  <WorkingDirectory>{workingDirectory}</WorkingDirectory>
                </Exec>
              </Actions>
            </Task>
            """;
    }

    private static int RunSchtasks(string arguments)
    {
        var startInfo = new ProcessStartInfo("schtasks.exe", arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using Process? process = Process.Start(startInfo);
        if (process == null)
            return -1;

        process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        return process.WaitForExit(10_000) ? process.ExitCode : -1;
    }
}
