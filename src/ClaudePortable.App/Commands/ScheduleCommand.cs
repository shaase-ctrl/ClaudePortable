using System.CommandLine;
using System.Globalization;
using System.Runtime.Versioning;
using ClaudePortable.Scheduler.Scheduling;

namespace ClaudePortable.App.Commands;

[SupportedOSPlatform("windows")]
public static class ScheduleCommand
{
    public static Command Build()
    {
        var cmd = new Command("schedule", "Install, inspect, or remove a Windows Task Scheduler entry that runs daily backups.");
        cmd.AddCommand(BuildInstall());
        cmd.AddCommand(BuildShow());
        cmd.AddCommand(BuildRemove());
        cmd.AddCommand(BuildEmit());
        return cmd;
    }

    private static Command BuildInstall()
    {
        var folderOption = new Option<DirectoryInfo>(new[] { "--folder", "-f" }, "Destination folder for scheduled backups.") { IsRequired = true };
        var timeOption = new Option<string>(new[] { "--at" }, () => "23:00", "Daily local time in HH:mm (24h).");
        var nameOption = new Option<string>(new[] { "--name" }, () => "ClaudePortable-Daily", "Task Scheduler task name.");
        var noInstallOption = new Option<bool>(new[] { "--no-install" }, () => false, "Write XML only; do not invoke schtasks.exe.");
        var install = new Command("install", "Create or replace the scheduled task.")
        {
            folderOption, timeOption, nameOption, noInstallOption,
        };

        install.SetHandler(async (folder, atRaw, taskName, noInstall) =>
        {
            if (!TimeOnly.TryParseExact(atRaw, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var at))
            {
                Console.Error.WriteLine($"error: could not parse --at '{atRaw}'. Expected HH:mm.");
                Environment.ExitCode = 1;
                return;
            }

            var exe = Environment.ProcessPath
                ?? throw new InvalidOperationException("Could not determine current executable path.");
            var spec = new ScheduleSpec(
                TaskName: taskName,
                ExecutablePath: exe,
                Arguments: new[] { "backup", "--to", folder.FullName },
                DailyStart: at,
                Description: $"ClaudePortable daily backup to {folder.FullName}");

            var xml = TaskSchedulerEmitter.ToXml(spec, DateTimeOffset.UtcNow);
            var xmlPath = Path.Combine(
                Environment.ExpandEnvironmentVariables("%LOCALAPPDATA%"),
                "ClaudePortable",
                $"{taskName}.xml");
            Directory.CreateDirectory(Path.GetDirectoryName(xmlPath)!);
            await File.WriteAllTextAsync(xmlPath, xml).ConfigureAwait(false);
            Console.WriteLine($"wrote: {xmlPath}");

            if (noInstall)
            {
                Console.WriteLine("--no-install set; skipping schtasks.exe.");
                return;
            }

            var installer = new TaskSchedulerInstaller();
            var exit = await installer.InstallAsync(taskName, xmlPath).ConfigureAwait(false);
            if (exit != 0)
            {
                Console.Error.WriteLine($"error: schtasks.exe /Create exited with code {exit}.");
                Environment.ExitCode = 3;
                return;
            }
            Console.WriteLine($"installed scheduled task '{taskName}' running daily at {at:HH:mm} local.");
        }, folderOption, timeOption, nameOption, noInstallOption);
        return install;
    }

    private static Command BuildShow()
    {
        var nameOption = new Option<string>(new[] { "--name" }, () => "ClaudePortable-Daily", "Task Scheduler task name.");
        var show = new Command("show", "Query the scheduled task via schtasks.exe /Query.")
        {
            nameOption,
        };
        show.SetHandler(async taskName =>
        {
            var installer = new TaskSchedulerInstaller();
            var (exit, output) = await installer.QueryAsync(taskName).ConfigureAwait(false);
            Console.Write(output);
            if (exit != 0)
            {
                Environment.ExitCode = 2;
            }
        }, nameOption);
        return show;
    }

    private static Command BuildRemove()
    {
        var nameOption = new Option<string>(new[] { "--name" }, () => "ClaudePortable-Daily", "Task Scheduler task name.");
        var remove = new Command("remove", "Delete the scheduled task via schtasks.exe /Delete /F.")
        {
            nameOption,
        };
        remove.SetHandler(async taskName =>
        {
            var installer = new TaskSchedulerInstaller();
            var exit = await installer.DeleteAsync(taskName).ConfigureAwait(false);
            if (exit != 0)
            {
                Console.Error.WriteLine($"error: schtasks.exe /Delete exited with code {exit}.");
                Environment.ExitCode = 3;
                return;
            }
            Console.WriteLine($"deleted scheduled task '{taskName}'.");
        }, nameOption);
        return remove;
    }

    private static Command BuildEmit()
    {
        var folderOption = new Option<DirectoryInfo>(new[] { "--folder", "-f" }, "Destination folder for scheduled backups.") { IsRequired = true };
        var timeOption = new Option<string>(new[] { "--at" }, () => "23:00", "Daily local time in HH:mm (24h).");
        var nameOption = new Option<string>(new[] { "--name" }, () => "ClaudePortable-Daily", "Task Scheduler task name.");
        var emit = new Command("emit", "Emit the Task Scheduler XML to stdout without installing.")
        {
            folderOption, timeOption, nameOption,
        };
        emit.SetHandler((folder, atRaw, taskName) =>
        {
            if (!TimeOnly.TryParseExact(atRaw, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var at))
            {
                Console.Error.WriteLine($"error: could not parse --at '{atRaw}'. Expected HH:mm.");
                Environment.ExitCode = 1;
                return;
            }
            var exe = Environment.ProcessPath ?? "claudeportable.exe";
            var spec = new ScheduleSpec(
                TaskName: taskName,
                ExecutablePath: exe,
                Arguments: new[] { "backup", "--to", folder.FullName },
                DailyStart: at,
                Description: $"ClaudePortable daily backup to {folder.FullName}");
            Console.WriteLine(TaskSchedulerEmitter.ToXml(spec, DateTimeOffset.UtcNow));
        }, folderOption, timeOption, nameOption);
        return emit;
    }
}
