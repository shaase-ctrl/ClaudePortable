using ClaudePortable.Scheduler.Scheduling;

namespace ClaudePortable.Tests.Scheduling;

public class ScheduledTaskClassifierTests
{
    [Theory]
    [InlineData("ClaudePortable-Daily", "Sascha", "", "", ManagedBy.ClaudePortable)]
    [InlineData("ClaudePortable", "anyone", "", "", ManagedBy.ClaudePortable)]
    [InlineData("ClaudePortable-Weekly", "x", "", "", ManagedBy.ClaudePortable)]
    [InlineData("SomeTask", "ClaudePortable installer", "", "", ManagedBy.ClaudePortable)]
    public void Classify_RecognisesClaudePortableManagedTasks(
        string name, string author, string exe, string args, ManagedBy expected)
    {
        var info = NewInfo(name, author, exe, args);
        Assert.Equal(expected, ScheduledTaskClassifier.Classify(info));
    }

    [Theory]
    [InlineData("Claude-Desktop-Backup", "Sascha Haase", "powershell.exe",
        @"-NoProfile -File ""C:\Users\X\OneDrive\02_Beruf\CoWork\Backup\2026-04-22_backup-claude-workstation.ps1""")]
    [InlineData("legacy-backup", "user", "powershell.exe",
        "-File backup-.claude.ps1")]
    [InlineData("AnthropicSync", "n/a",
        @"C:\Tools\sync.exe", "--source anthropic --dest cloud")]
    [InlineData("BackupTask", "ops", "robocopy.exe",
        @"%APPDATA%\Claude\ \\nas\backup")]
    public void Classify_RecognisesForeignRelevantTasks(string name, string author, string exe, string args)
    {
        var info = NewInfo(name, author, exe, args);
        Assert.Equal(ManagedBy.ForeignRelevant, ScheduledTaskClassifier.Classify(info));
    }

    [Theory]
    [InlineData("Adobe Acrobat Update Task", "Adobe Inc.",
        @"C:\Program Files (x86)\Common Files\Adobe\ARM\1.0\AdobeARM.exe", "")]
    [InlineData("Office Automatic Updates 2.0", "Microsoft Corporation",
        @"C:\Program Files\Common Files\OfficeC2RClient.exe", "/update user")]
    [InlineData("MSIAfterburner", "user",
        @"C:\Program Files (x86)\MSI Afterburner\MSIAfterburner.exe", "/s")]
    public void Classify_LeavesUnrelatedTasksAsForeignOther(string name, string author, string exe, string args)
    {
        var info = NewInfo(name, author, exe, args);
        Assert.Equal(ManagedBy.ForeignOther, ScheduledTaskClassifier.Classify(info));
    }

    [Fact]
    public void Classify_IsCaseInsensitive()
    {
        var info = NewInfo("LEGACY", "ops", "POWERSHELL.EXE", @"-File C:\users\x\.CLAUDE\backup.ps1");
        Assert.Equal(ManagedBy.ForeignRelevant, ScheduledTaskClassifier.Classify(info));
    }

    [Fact]
    public void Classify_SubfolderTaskNameTriggersClaudePortable()
    {
        var info = NewInfo("ClaudePortable-Daily", "user", @"C:\bin\claudeportable.exe", "backup");
        info = info with { Name = "ClaudePortable-Daily", FolderPath = @"\ClaudePortable" };
        Assert.Equal(ManagedBy.ClaudePortable, ScheduledTaskClassifier.Classify(info));
    }

    [Fact]
    public void Classify_MarkerOnlyInWorkingDirectory_IsForeignRelevant()
    {
        var info = new ScheduledTaskInfo(
            Name: "AdHoc",
            FolderPath: "\\",
            State: "Bereit",
            NextRunTime: null,
            LastRunTime: null,
            LastResult: null,
            Author: "user",
            Action: new ScheduledTaskAction("backup.exe", "now", @"C:\Users\X\.claude"),
            TriggerSummary: "manual",
            ManagedBy: ManagedBy.ForeignOther);
        Assert.Equal(ManagedBy.ForeignRelevant, ScheduledTaskClassifier.Classify(info));
    }

    private static ScheduledTaskInfo NewInfo(string name, string author, string exe, string args) =>
        new(
            Name: name,
            FolderPath: "\\",
            State: "Bereit",
            NextRunTime: null,
            LastRunTime: null,
            LastResult: null,
            Author: author,
            Action: new ScheduledTaskAction(exe, args, string.Empty),
            TriggerSummary: "test",
            ManagedBy: ManagedBy.ForeignOther);
}
