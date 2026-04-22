using System.CommandLine;
using System.Runtime.Versioning;
using ClaudePortable.Scheduler.Retention;
using ClaudePortable.Targets;

namespace ClaudePortable.App.Commands;

[SupportedOSPlatform("windows")]
public static class RotateCommand
{
    public static Command Build()
    {
        var inOption = new Option<DirectoryInfo>(
            aliases: new[] { "--in", "-i" },
            description: "Folder to rotate.")
        {
            IsRequired = true,
        };
        var dailyOption = new Option<int>(new[] { "--daily" }, () => 7, "Daily backups to keep.");
        var weeklyOption = new Option<int>(new[] { "--weekly" }, () => 3, "Weekly backups to keep.");
        var monthlyOption = new Option<int>(new[] { "--monthly" }, () => 2, "Monthly backups to keep.");

        var cmd = new Command("rotate", "Apply retention rotation (promote daily->weekly, weekly->monthly; prune older).")
        {
            inOption, dailyOption, weeklyOption, monthlyOption,
        };

        cmd.SetHandler((folder, daily, weekly, monthly) =>
        {
            var target = new FolderTarget(folder.FullName);
            var policy = new RetentionPolicy(daily, weekly, monthly, DayOfWeek.Sunday);
            var manager = new RetentionManager(policy);
            var report = manager.Rotate(target);
            Console.WriteLine($"promoted: {report.Promoted.Count}");
            foreach (var item in report.Promoted)
            {
                Console.WriteLine($"  {item}");
            }
            Console.WriteLine($"pruned: {report.Pruned.Count}");
            foreach (var item in report.Pruned)
            {
                Console.WriteLine($"  {item}");
            }
            Console.WriteLine($"daily={report.DailyAfter} weekly={report.WeeklyAfter} monthly={report.MonthlyAfter}");
        }, inOption, dailyOption, weeklyOption, monthlyOption);

        return cmd;
    }
}
