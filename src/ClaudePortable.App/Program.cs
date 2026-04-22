using System.CommandLine;
using ClaudePortable.App.Commands;
using UiApp = ClaudePortable.App.Ui.App;

namespace ClaudePortable.App;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length == 0 || args.Contains("--gui"))
        {
            return UiApp.RunGui();
        }

        return MainCliAsync(args).GetAwaiter().GetResult();
    }

    private static async Task<int> MainCliAsync(string[] args)
    {
        var root = new RootCommand("ClaudePortable - backup and restore Claude Desktop / Claude Code state.");
        root.AddCommand(BackupCommand.Build());
        root.AddCommand(RestoreCommand.Build());
        root.AddCommand(ListCommand.Build());
        root.AddCommand(DiscoverCommand.Build());
        root.AddCommand(ScheduleCommand.Build());
        root.AddCommand(RotateCommand.Build());
        return await root.InvokeAsync(args).ConfigureAwait(false);
    }
}
