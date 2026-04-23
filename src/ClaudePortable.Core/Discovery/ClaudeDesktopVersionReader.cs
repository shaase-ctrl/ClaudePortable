using System.Diagnostics;
using System.Runtime.Versioning;

namespace ClaudePortable.Core.Discovery;

[SupportedOSPlatform("windows")]
public static class ClaudeDesktopVersionReader
{
    public static string? TryRead(TimeSpan? timeout = null)
    {
        var deadline = timeout ?? TimeSpan.FromSeconds(5);
        try
        {
            var psi = new ProcessStartInfo("powershell.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            psi.ArgumentList.Add("-NoProfile");
            psi.ArgumentList.Add("-NonInteractive");
            psi.ArgumentList.Add("-Command");
            psi.ArgumentList.Add("Get-AppxPackage -Name Claude | Select-Object -ExpandProperty Version");

            using var proc = Process.Start(psi);
            if (proc is null)
            {
                return null;
            }
            if (!proc.WaitForExit(deadline))
            {
                try { proc.Kill(); } catch (InvalidOperationException) { }
                return null;
            }
            if (proc.ExitCode != 0)
            {
                return null;
            }
            var output = proc.StandardOutput.ReadToEnd().Trim();
            return string.IsNullOrEmpty(output) ? null : output;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        {
            return null;
        }
    }
}
