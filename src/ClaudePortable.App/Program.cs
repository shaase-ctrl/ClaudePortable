using System.CommandLine;
using ClaudePortable.App.Commands;

namespace ClaudePortable.App;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var root = new RootCommand("ClaudePortable - backup and restore Claude Desktop / Claude Code state.");

        root.AddCommand(BackupCommand.Build());
        root.AddCommand(RestoreCommand.Build());
        root.AddCommand(ListCommand.Build());
        root.AddCommand(DiscoverCommand.Build());

        return await root.InvokeAsync(args).ConfigureAwait(false);
    }
}
