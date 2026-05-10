using System.CommandLine;
using System.Globalization;
using System.Runtime.Versioning;
using System.Text.Json;
using ClaudePortable.Scheduler.Scheduling;

namespace ClaudePortable.App.Commands;

[SupportedOSPlatform("windows")]
public static class ScheduleCommand
{
    public static Command Build()
    {
        var cmd = new Command("schedule", "Install, inspect, list, or remove Windows Task Scheduler entries.");
        cmd.AddCommand(BuildInstall());
        cmd.AddCommand(BuildShow());
        cmd.AddCommand(BuildRemove());
        cmd.AddCommand(BuildEmit());
        cmd.AddCommand(BuildList());
        cmd.AddCommand(BuildDisable());
        cmd.AddCommand(BuildEnable());
        cmd.AddCommand(BuildRun());
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

    private static Command BuildList()
    {
        var allOption = new Option<bool>(new[] { "--all" }, () => false, "Include foreign tasks unrelated to Claude (default: hide them).");
        var managedOption = new Option<bool>(new[] { "--managed" }, () => false, "Only list ClaudePortable-managed tasks.");
        var relevantOption = new Option<bool>(new[] { "--relevant" }, () => false, "Only list ClaudePortable + Claude-related tasks (default).");
        var jsonOption = new Option<bool>(new[] { "--json" }, () => false, "Emit JSON instead of an aligned table.");
        var list = new Command("list", "List Windows scheduled tasks and flag Claude relevance.")
        {
            allOption, managedOption, relevantOption, jsonOption,
        };
        list.SetHandler(async (all, managed, relevant, json) =>
        {
            var installer = new TaskSchedulerInstaller();
            var tasks = await installer.EnumerateAsync().ConfigureAwait(false);

            IEnumerable<ScheduledTaskInfo> filtered = tasks;
            if (managed)
            {
                filtered = filtered.Where(t => t.ManagedBy == ManagedBy.ClaudePortable);
            }
            else if (all)
            {
                // no filter
            }
            else
            {
                filtered = filtered.Where(t => t.ManagedBy is ManagedBy.ClaudePortable or ManagedBy.ForeignRelevant);
            }

            _ = relevant;

            var ordered = filtered
                .OrderBy(t => t.ManagedBy)
                .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (json)
            {
                var payload = new
                {
                    tasks = ordered.Select(t => new
                    {
                        name = t.Name,
                        fullName = t.FullName,
                        folderPath = t.FolderPath,
                        managedBy = t.ManagedBy.ToString(),
                        state = t.State,
                        author = t.Author,
                        nextRun = t.NextRunTime?.ToString("o", CultureInfo.InvariantCulture),
                        lastRun = t.LastRunTime?.ToString("o", CultureInfo.InvariantCulture),
                        lastResult = t.LastResult,
                        action = new
                        {
                            executable = t.Action.Executable,
                            arguments = t.Action.Arguments,
                            workingDirectory = t.Action.WorkingDirectory,
                        },
                        trigger = t.TriggerSummary,
                    }),
                };
                Console.WriteLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
                return;
            }

            if (ordered.Count == 0)
            {
                Console.WriteLine("(no tasks matched the filter)");
                return;
            }

            var headers = new[] { "NAME", "MANAGED-BY", "STATE", "NEXT RUN", "ACTION" };
            var rows = ordered.Select(t => new[]
            {
                t.FullName,
                t.ManagedBy.ToString(),
                t.State,
                t.NextRunTime?.LocalDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "-",
                Truncate(string.IsNullOrEmpty(t.Action.Arguments) ? t.Action.Executable : $"{t.Action.Executable} {t.Action.Arguments}", 80),
            }).ToList();

            var widths = headers
                .Select((h, i) => Math.Max(h.Length, rows.Count == 0 ? 0 : rows.Max(r => r[i]?.Length ?? 0)))
                .ToArray();

            Console.WriteLine(string.Join("  ", headers.Select((h, i) => h.PadRight(widths[i]))));
            Console.WriteLine(string.Join("  ", widths.Select(w => new string('-', w))));
            foreach (var row in rows)
            {
                Console.WriteLine(string.Join("  ", row.Select((c, i) => (c ?? string.Empty).PadRight(widths[i]))));
            }
        }, allOption, managedOption, relevantOption, jsonOption);
        return list;
    }

    private static Command BuildDisable()
    {
        var nameArg = new Argument<string>("name", "Full task name (e.g. \\ClaudePortable-Daily).");
        var disable = new Command("disable", "Disable a scheduled task via schtasks.exe /Change /Disable.")
        {
            nameArg,
        };
        disable.SetHandler(async taskName =>
        {
            var installer = new TaskSchedulerInstaller();
            var exit = await installer.DisableAsync(taskName).ConfigureAwait(false);
            if (exit != 0)
            {
                Console.Error.WriteLine($"error: schtasks.exe /Change /Disable exited with code {exit}.");
                Environment.ExitCode = 3;
                return;
            }
            Console.WriteLine($"disabled '{taskName}'.");
        }, nameArg);
        return disable;
    }

    private static Command BuildEnable()
    {
        var nameArg = new Argument<string>("name", "Full task name (e.g. \\ClaudePortable-Daily).");
        var enable = new Command("enable", "Enable a scheduled task via schtasks.exe /Change /Enable.")
        {
            nameArg,
        };
        enable.SetHandler(async taskName =>
        {
            var installer = new TaskSchedulerInstaller();
            var exit = await installer.EnableAsync(taskName).ConfigureAwait(false);
            if (exit != 0)
            {
                Console.Error.WriteLine($"error: schtasks.exe /Change /Enable exited with code {exit}.");
                Environment.ExitCode = 3;
                return;
            }
            Console.WriteLine($"enabled '{taskName}'.");
        }, nameArg);
        return enable;
    }

    private static Command BuildRun()
    {
        var nameArg = new Argument<string>("name", "Full task name (e.g. \\ClaudePortable-Daily).");
        var run = new Command("run", "Trigger a scheduled task immediately via schtasks.exe /Run.")
        {
            nameArg,
        };
        run.SetHandler(async taskName =>
        {
            var installer = new TaskSchedulerInstaller();
            var exit = await installer.RunNowAsync(taskName).ConfigureAwait(false);
            if (exit != 0)
            {
                Console.Error.WriteLine($"error: schtasks.exe /Run exited with code {exit}.");
                Environment.ExitCode = 3;
                return;
            }
            Console.WriteLine($"triggered '{taskName}'.");
        }, nameArg);
        return run;
    }

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s[..(max - 3)] + "...";

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
