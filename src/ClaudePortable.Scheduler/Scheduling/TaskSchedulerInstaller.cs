using System.Diagnostics;
using System.Runtime.Versioning;

namespace ClaudePortable.Scheduler.Scheduling;

[SupportedOSPlatform("windows")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Instance methods for DI and mock-ability.")]
public sealed class TaskSchedulerInstaller
{
    public async Task<int> InstallAsync(string taskName, string xmlPath, CancellationToken cancellationToken = default)
    {
        return await RunSchtasksAsync(new[] { "/Create", "/TN", taskName, "/XML", xmlPath, "/F" }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> DeleteAsync(string taskName, CancellationToken cancellationToken = default)
    {
        return await RunSchtasksAsync(new[] { "/Delete", "/TN", taskName, "/F" }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<(int ExitCode, string Output)> QueryAsync(string taskName, CancellationToken cancellationToken = default)
    {
        var psi = BuildStartInfo(new[] { "/Query", "/TN", taskName, "/FO", "LIST", "/V" });
        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start schtasks.exe.");
        var stdout = await proc.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await proc.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return (proc.ExitCode, stdout);
    }

    private static async Task<int> RunSchtasksAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var psi = BuildStartInfo(args);
        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start schtasks.exe.");
        _ = await proc.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        _ = await proc.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await proc.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return proc.ExitCode;
    }

    private static ProcessStartInfo BuildStartInfo(IReadOnlyList<string> args)
    {
        var psi = new ProcessStartInfo("schtasks.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var a in args)
        {
            psi.ArgumentList.Add(a);
        }
        return psi;
    }
}
