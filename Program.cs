using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PKSVModMerger;

internal static class Program
{
    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int processId);
    private const int ATTACH_PARENT_PROCESS = -1;

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
            return 0;
        }

        // CLI mode: WinExe has no console of its own, so reattach to the parent shell
        // and rebuild the stdout/stderr writers so Console.WriteLine reaches the user.
        AttachConsole(ATTACH_PARENT_PROCESS);
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });

        return RunCli(args);
    }

    private static int RunCli(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("Usage: PKSVModMerger <output.trpfd> <base.trpfd> <add1.trpfd> [<add2.trpfd> ...]");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Reads <base> as the starting TRPFD and unions every <addN>'s UnusedHashes");
            Console.Error.WriteLine("into it. For each hash any input marked unused, the merged TRPFD also marks");
            Console.Error.WriteLine("it unused so the game looks for a loose file on disk instead.");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Run with no arguments to open the GUI.");
            return 1;
        }

        try
        {
            var outputPath = args[0];
            var basePath = args[1];
            var additionalPaths = args[2..];
            Merger.Run(basePath, additionalPaths, outputPath, Console.WriteLine);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ERROR] {ex.GetType().Name}: {ex.Message}");
            return 1;
        }
    }
}
