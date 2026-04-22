using System.Xml;
using ClaudePortable.Scheduler.Scheduling;

namespace ClaudePortable.Tests;

public class TaskSchedulerEmitterTests
{
    [Fact]
    public void ToXml_ProducesValidWindowsTaskSchema()
    {
        var spec = new ScheduleSpec(
            TaskName: "ClaudePortable-Daily",
            ExecutablePath: @"C:\Program Files\ClaudePortable\claudeportable.exe",
            Arguments: new[] { "backup", "--to", @"C:\Backups" },
            DailyStart: new TimeOnly(23, 0),
            Description: "test");
        var xml = TaskSchedulerEmitter.ToXml(spec, new DateTimeOffset(2026, 4, 22, 12, 0, 0, TimeSpan.Zero));

        var doc = new XmlDocument();
        doc.LoadXml(xml);
        Assert.Equal("Task", doc.DocumentElement!.LocalName);
        Assert.Equal("1.4", doc.DocumentElement!.GetAttribute("version"));

        var ns = new XmlNamespaceManager(doc.NameTable);
        ns.AddNamespace("t", "http://schemas.microsoft.com/windows/2004/02/mit/task");

        Assert.NotNull(doc.SelectSingleNode("/t:Task/t:Triggers/t:CalendarTrigger/t:ScheduleByDay", ns));
        var command = doc.SelectSingleNode("/t:Task/t:Actions/t:Exec/t:Command", ns);
        Assert.Equal(@"C:\Program Files\ClaudePortable\claudeportable.exe", command!.InnerText);
        var argsNode = doc.SelectSingleNode("/t:Task/t:Actions/t:Exec/t:Arguments", ns);
        Assert.Contains("backup", argsNode!.InnerText, StringComparison.Ordinal);
        Assert.Contains(@"C:\Backups", argsNode!.InnerText, StringComparison.Ordinal);
    }

    [Fact]
    public void JoinArguments_QuotesPathsWithSpaces()
    {
        var joined = TaskSchedulerEmitter.JoinArguments(new[] { "backup", "--to", @"C:\Program Files\my backups" });
        Assert.Contains("\"C:\\Program Files\\my backups\"", joined, StringComparison.Ordinal);
    }
}
