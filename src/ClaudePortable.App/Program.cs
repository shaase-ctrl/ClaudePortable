using System.CommandLine;
using System.Runtime.InteropServices;
using ClaudePortable.App.Commands;
using UiApp = ClaudePortable.App.Ui.App;

namespace ClaudePortable.App;

public static class Program
{
    private const int AttachParentProcess = -1;

    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length == 0 || args.Contains("--gui"))
        {
            return UiApp.RunGui();
        }

        // CLI mode: we're a WinExe so no console was allocated. Attach to
        // the parent terminal's console if there is one; otherwise allocate
        // our own so exit code propagates and output is visible.
        EnsureConsoleForCli();
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

    private static void EnsureConsoleForCli()
    {
        if (AttachConsole(AttachParentProcess))
        {
            // Redirect managed Console streams to the real console so our
            // output mixes cleanly with whatever the parent shell prints.
            var stdOut = Console.OpenStandardOutput();
            if (stdOut != Stream.Null)
            {
                var writer = new StreamWriter(stdOut) { AutoFlush = true };
                Console.SetOut(writer);
            }
            var stdErr = Console.OpenStandardError();
            if (stdErr != Stream.Null)
            {
                var writer = new StreamWriter(stdErr) { AutoFlush = true };
                Console.SetError(writer);
            }
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(int processId);
}
