using ClaudePortable.Scheduler.Scheduling;

namespace ClaudePortable.Tests.Scheduling;

public class TaskSchedulerInstallerCommandShapeTests
{
    [Fact]
    public async Task InstallAsync_PassesCreateTnXmlForce()
    {
        var recorded = new List<IReadOnlyList<string>>();
        var installer = new TaskSchedulerInstaller(Recorder(recorded, new SchtasksResult(0, string.Empty, string.Empty)));

        await installer.InstallAsync("ClaudePortable-Daily", @"C:\xml\task.xml");

        var args = Assert.Single(recorded);
        Assert.Equal(new[] { "/Create", "/TN", "ClaudePortable-Daily", "/XML", @"C:\xml\task.xml", "/F" }, args);
    }

    [Fact]
    public async Task DeleteAsync_PassesDeleteTnForce()
    {
        var recorded = new List<IReadOnlyList<string>>();
        var installer = new TaskSchedulerInstaller(Recorder(recorded, new SchtasksResult(0, string.Empty, string.Empty)));

        await installer.DeleteAsync("ClaudePortable-Daily");

        var args = Assert.Single(recorded);
        Assert.Equal(new[] { "/Delete", "/TN", "ClaudePortable-Daily", "/F" }, args);
    }

    [Fact]
    public async Task DisableAsync_PassesChangeTnDisable()
    {
        var recorded = new List<IReadOnlyList<string>>();
        var installer = new TaskSchedulerInstaller(Recorder(recorded, new SchtasksResult(0, string.Empty, string.Empty)));

        await installer.DisableAsync(@"\Claude-Desktop-Backup");

        var args = Assert.Single(recorded);
        Assert.Equal(new[] { "/Change", "/TN", @"\Claude-Desktop-Backup", "/Disable" }, args);
    }

    [Fact]
    public async Task EnableAsync_PassesChangeTnEnable()
    {
        var recorded = new List<IReadOnlyList<string>>();
        var installer = new TaskSchedulerInstaller(Recorder(recorded, new SchtasksResult(0, string.Empty, string.Empty)));

        await installer.EnableAsync("ClaudePortable-Daily");

        var args = Assert.Single(recorded);
        Assert.Equal(new[] { "/Change", "/TN", "ClaudePortable-Daily", "/Enable" }, args);
    }

    [Fact]
    public async Task RunNowAsync_PassesRunTn()
    {
        var recorded = new List<IReadOnlyList<string>>();
        var installer = new TaskSchedulerInstaller(Recorder(recorded, new SchtasksResult(0, string.Empty, string.Empty)));

        await installer.RunNowAsync("ClaudePortable-Daily");

        var args = Assert.Single(recorded);
        Assert.Equal(new[] { "/Run", "/TN", "ClaudePortable-Daily" }, args);
    }

    [Fact]
    public async Task GetTaskXmlAsync_PassesQueryTnXml_AndReturnsStdout()
    {
        var recorded = new List<IReadOnlyList<string>>();
        var installer = new TaskSchedulerInstaller(Recorder(recorded, new SchtasksResult(0, "<Task />", string.Empty)));

        var (exit, xml) = await installer.GetTaskXmlAsync("ClaudePortable-Daily");

        var args = Assert.Single(recorded);
        Assert.Equal(new[] { "/Query", "/TN", "ClaudePortable-Daily", "/XML" }, args);
        Assert.Equal(0, exit);
        Assert.Equal("<Task />", xml);
    }

    [Fact]
    public async Task EnumerateAsync_PassesQueryCsvVerbose_AndParsesRows()
    {
        var csv = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Scheduling", "Fixtures", "schtasks-csv-sample-de.csv"));
        var recorded = new List<IReadOnlyList<string>>();
        var installer = new TaskSchedulerInstaller(Recorder(recorded, new SchtasksResult(0, csv, string.Empty)));

        var tasks = await installer.EnumerateAsync();

        var args = Assert.Single(recorded);
        Assert.Equal(new[] { "/Query", "/FO", "CSV", "/V" }, args);
        Assert.Equal(5, tasks.Count);
    }

    [Fact]
    public async Task EnumerateAsync_NonZeroExitOrEmptyOutput_ReturnsEmpty()
    {
        var installer = new TaskSchedulerInstaller((_, _) => Task.FromResult(new SchtasksResult(1, string.Empty, "error")));
        var tasks = await installer.EnumerateAsync();
        Assert.Empty(tasks);
    }

    [Fact]
    public async Task InstallAsync_PropagatesExitCodeFromRunner()
    {
        var installer = new TaskSchedulerInstaller((_, _) => Task.FromResult(new SchtasksResult(42, string.Empty, "err")));
        var exit = await installer.InstallAsync("x", "y");
        Assert.Equal(42, exit);
    }

    private static Func<IReadOnlyList<string>, CancellationToken, Task<SchtasksResult>> Recorder(
        List<IReadOnlyList<string>> recorded,
        SchtasksResult result) =>
        (args, _) =>
        {
            recorded.Add(args.ToList());
            return Task.FromResult(result);
        };
}
