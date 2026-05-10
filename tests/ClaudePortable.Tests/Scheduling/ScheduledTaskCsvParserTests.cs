using ClaudePortable.Scheduler.Scheduling;

namespace ClaudePortable.Tests.Scheduling;

public class ScheduledTaskCsvParserTests
{
    [Fact]
    public void Parse_GermanFixture_DiscoversAllExpectedTasks()
    {
        var csv = ReadFixture("schtasks-csv-sample-de.csv");
        var tasks = ScheduledTaskCsvParser.Parse(csv);

        Assert.Equal(5, tasks.Count);
        var names = tasks.Select(t => t.Name).ToHashSet();
        Assert.Contains("Claude-Desktop-Backup", names);
        Assert.Contains("ClaudePortable-Daily", names);
        Assert.Contains("Adobe Acrobat Update Task", names);
        Assert.Contains("Office Automatic Updates 2.0", names);
        Assert.Contains("OldClaudePortable-Test", names);
    }

    [Fact]
    public void Parse_LegacyBackupTask_ParsesPowerShellInvocation()
    {
        var csv = ReadFixture("schtasks-csv-sample-de.csv");
        var task = ScheduledTaskCsvParser.Parse(csv).Single(t => t.Name == "Claude-Desktop-Backup");

        Assert.Equal("\\", task.FolderPath);
        Assert.Equal("Bereit", task.State);
        Assert.Equal("Sascha Haase", task.Author);
        Assert.Equal("powershell.exe", task.Action.Executable);
        Assert.Contains("-File", task.Action.Arguments, StringComparison.Ordinal);
        Assert.Contains("2026-04-22_backup-claude-workstation.ps1", task.Action.Arguments, StringComparison.Ordinal);
        Assert.Contains("CoWork\\Backup", task.Action.Arguments, StringComparison.Ordinal);
        Assert.Equal(0, task.LastResult);
    }

    [Fact]
    public void Parse_QuotedExecutableWithSpaces_StripsQuotesAndKeepsArgs()
    {
        var csv = ReadFixture("schtasks-csv-sample-de.csv");
        var task = ScheduledTaskCsvParser.Parse(csv).Single(t => t.Name == "Adobe Acrobat Update Task");

        Assert.Equal(@"C:\Program Files (x86)\Common Files\Adobe\ARM\1.0\AdobeARM.exe", task.Action.Executable);
        Assert.Equal(string.Empty, task.Action.Arguments);
    }

    [Fact]
    public void Parse_UnquotedExecutable_SplitsAtFirstSpace()
    {
        var csv = ReadFixture("schtasks-csv-sample-de.csv");
        var task = ScheduledTaskCsvParser.Parse(csv).Single(t => t.Name == "Office Automatic Updates 2.0");

        Assert.Equal(@"C:\Program", task.Action.Executable);
        Assert.Contains("OfficeC2RClient.exe", task.Action.Arguments, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_SubfolderTaskName_SeparatesFolderAndName()
    {
        var csv = ReadFixture("schtasks-csv-sample-de.csv");
        var task = ScheduledTaskCsvParser.Parse(csv).Single(t => t.Name == "Office Automatic Updates 2.0");

        Assert.Equal(@"\Microsoft\Office", task.FolderPath);
    }

    [Fact]
    public void Parse_TaskMarkedDeaktiviert_KeepsStateFromStatusColumn()
    {
        var csv = ReadFixture("schtasks-csv-sample-de.csv");
        var task = ScheduledTaskCsvParser.Parse(csv).Single(t => t.Name == "OldClaudePortable-Test");

        Assert.Equal("Deaktiviert", task.State);
        Assert.Null(task.LastResult);
        Assert.Null(task.LastRunTime);
    }

    [Fact]
    public void Parse_AssignsManagedByClassification()
    {
        var csv = ReadFixture("schtasks-csv-sample-de.csv");
        var tasks = ScheduledTaskCsvParser.Parse(csv);

        Assert.Equal(ManagedBy.ClaudePortable, tasks.Single(t => t.Name == "ClaudePortable-Daily").ManagedBy);
        Assert.Equal(ManagedBy.ForeignRelevant, tasks.Single(t => t.Name == "Claude-Desktop-Backup").ManagedBy);
        Assert.Equal(ManagedBy.ForeignOther, tasks.Single(t => t.Name == "Adobe Acrobat Update Task").ManagedBy);
    }

    [Fact]
    public void Parse_EmptyCsv_ReturnsEmpty()
    {
        Assert.Empty(ScheduledTaskCsvParser.Parse(string.Empty));
    }

    [Fact]
    public void Parse_HeaderOnly_ReturnsEmpty()
    {
        var headerOnly = "\"Hostname\",\"Aufgabenname\",\"Naechste Laufzeit\"\n";
        Assert.Empty(ScheduledTaskCsvParser.Parse(headerOnly));
    }

    [Fact]
    public void SplitExecutableAndArgs_HandlesQuotedPathWithArgs()
    {
        var (exe, args) = ScheduledTaskCsvParser.SplitExecutableAndArgs(@"""C:\Program Files\Git\git-bash.exe"" --hide --no-needs-console");

        Assert.Equal(@"C:\Program Files\Git\git-bash.exe", exe);
        Assert.Equal("--hide --no-needs-console", args);
    }

    [Fact]
    public void SplitExecutableAndArgs_EmptyInput_ReturnsEmpty()
    {
        var (exe, args) = ScheduledTaskCsvParser.SplitExecutableAndArgs(string.Empty);
        Assert.Equal(string.Empty, exe);
        Assert.Equal(string.Empty, args);
    }

    private static string ReadFixture(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Scheduling", "Fixtures", name);
        return File.ReadAllText(path);
    }
}
