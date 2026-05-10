using System.Diagnostics;
using System.Runtime.Versioning;

namespace ClaudePortable.Scheduler.Scheduling;

[SupportedOSPlatform("windows")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Instance methods for DI and mock-ability.")]
public sealed class TaskSchedulerInstaller
{
    private static readonly string[] EnumerateArgs = { "/Query", "/FO", "CSV", "/V" };

    private readonly Func<IReadOnlyList<string>, CancellationToken, Task<SchtasksResult>> _runSchtasks;

    public TaskSchedulerInstaller()
        : this(DefaultRunSchtasksAsync)
    {
    }

    internal TaskSchedulerInstaller(Func<IReadOnlyList<string>, CancellationToken, Task<SchtasksResult>> runSchtasks)
    {
        _runSchtasks = runSchtasks;
    }

    public async Task<int> InstallAsync(string taskName, string xmlPath, CancellationToken cancellationToken = default)
    {
        var r = await _runSchtasks(new[] { "/Create", "/TN", taskName, "/XML", xmlPath, "/F" }, cancellationToken).ConfigureAwait(false);
        return r.ExitCode;
    }

    public async Task<int> DeleteAsync(string taskName, CancellationToken cancellationToken = default)
    {
        var r = await _runSchtasks(new[] { "/Delete", "/TN", taskName, "/F" }, cancellationToken).ConfigureAwait(false);
        return r.ExitCode;
    }

    public async Task<(int ExitCode, string Output)> QueryAsync(string taskName, CancellationToken cancellationToken = default)
    {
        var r = await _runSchtasks(new[] { "/Query", "/TN", taskName, "/FO", "LIST", "/V" }, cancellationToken).ConfigureAwait(false);
        return (r.ExitCode, r.Stdout);
    }

    public async Task<int> DisableAsync(string taskName, CancellationToken cancellationToken = default)
    {
        var r = await _runSchtasks(new[] { "/Change", "/TN", taskName, "/Disable" }, cancellationToken).ConfigureAwait(false);
        return r.ExitCode;
    }

    public async Task<int> EnableAsync(string taskName, CancellationToken cancellationToken = default)
    {
        var r = await _runSchtasks(new[] { "/Change", "/TN", taskName, "/Enable" }, cancellationToken).ConfigureAwait(false);
        return r.ExitCode;
    }

    public async Task<int> RunNowAsync(string taskName, CancellationToken cancellationToken = default)
    {
        var r = await _runSchtasks(new[] { "/Run", "/TN", taskName }, cancellationToken).ConfigureAwait(false);
        return r.ExitCode;
    }

    public async Task<(int ExitCode, string Xml)> GetTaskXmlAsync(string taskName, CancellationToken cancellationToken = default)
    {
        var r = await _runSchtasks(new[] { "/Query", "/TN", taskName, "/XML" }, cancellationToken).ConfigureAwait(false);
        return (r.ExitCode, r.Stdout);
    }

    public async Task<IReadOnlyList<ScheduledTaskInfo>> EnumerateAsync(CancellationToken cancellationToken = default)
    {
        var r = await _runSchtasks(EnumerateArgs, cancellationToken).ConfigureAwait(false);
        if (r.ExitCode != 0 || string.IsNullOrWhiteSpace(r.Stdout))
        {
            return Array.Empty<ScheduledTaskInfo>();
        }
        return ScheduledTaskCsvParser.Parse(r.Stdout);
    }

    private static async Task<SchtasksResult> DefaultRunSchtasksAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var psi = BuildStartInfo(args);
        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start schtasks.exe.");
        var stdout = await proc.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        var stderr = await proc.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await proc.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return new SchtasksResult(proc.ExitCode, stdout, stderr);
    }

    private static ProcessStartInfo BuildStartInfo(IReadOnlyList<string> args)
    {
        var psi = new ProcessStartInfo("schtasks.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
        };
        foreach (var a in args)
        {
            psi.ArgumentList.Add(a);
        }
        return psi;
    }
}

public readonly record struct SchtasksResult(int ExitCode, string Stdout, string Stderr);
