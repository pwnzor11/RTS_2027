using System.Runtime.InteropServices;
using System.Text;
using Rts.Data;
using Rts.Game.Sandbox;

namespace Rts.Game;

/// <summary>Process entry point for the MonoGame client.</summary>
public static class Program
{
    /// <summary>
    /// Opens the battle sandbox and runs until the player quits. Options: <c>--empty</c> starts with the legacy 3-tank
    /// setup; <c>--demo</c> draws the fabricated demo source instead of the sim; <c>--headless-check &lt;ticks&gt;</c>
    /// runs the scripted sandbox fight with no window, saves the replay and exits. Any unhandled exception writes
    /// <c>logs/crash-*.txt</c> beside the exe and returns exit code 1 (C-6).
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>The exit code: 0 on success, 1 after a crash, otherwise the headless check's result.</returns>
    public static int Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            CrashReport.Report(e.ExceptionObject as Exception ?? new InvalidOperationException("Unknown exception."), args);

        try
        {
            if (args.Contains("--headless-check", StringComparer.Ordinal)) return HeadlessCheck.Run(args);

            bool demo = args.Contains("--demo", StringComparer.Ordinal);
            bool empty = args.Contains("--empty", StringComparer.Ordinal);
            using var game = new RtsGame(demo, empty);
            game.Run();
            return 0;
        }
        catch (Exception ex)
        {
            CrashReport.Report(ex, args);
            return 1;
        }
    }
}

/// <summary>
/// Writes a crash log next to the exe and, on Windows, tells the player where it is. Only the first report of a
/// process is written, so the main catch and the unhandled-exception hook never show two dialogs.
/// </summary>
internal static class CrashReport
{
    private const uint MbOk = 0x0;
    private const uint MbIconError = 0x10;

    private static int _reported;

    /// <summary>Writes the crash log for <paramref name="ex"/> and shows the message box on Windows.</summary>
    /// <param name="ex">The exception that ended the run.</param>
    /// <param name="args">Command-line arguments of this run, copied into the log.</param>
    public static void Report(Exception ex, string[] args)
    {
        if (Interlocked.Exchange(ref _reported, 1) == 1) return;

        string? path = WriteLog(ex, args);
        if (OperatingSystem.IsWindows())
        {
            string text = path is null
                ? "RTS_2027 crashed. The crash log could not be written."
                : $"RTS_2027 crashed. Details were written to {path}.";
            try
            {
                MessageBoxW(nint.Zero, text, "RTS_2027", MbOk | MbIconError);
            }
            catch (Exception)
            {
                // Nothing more can be shown; the log file (if any) is the record.
            }
        }
    }

    private static string? WriteLog(Exception ex, string[] args)
    {
        try
        {
            string dir = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, $"crash-{DateTime.Now:yyyyMMdd-HHmmss}.txt");

            var sb = new StringBuilder();
            sb.AppendLine("RTS_2027 crash report");
            sb.AppendLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}");
            sb.AppendLine($"Version: {BuildInfo.Version}");
            sb.AppendLine($"OS: {RuntimeInformation.OSDescription} ({RuntimeInformation.ProcessArchitecture})");
            sb.AppendLine($"Runtime: {RuntimeInformation.FrameworkDescription}");
            sb.AppendLine($"Arguments: {string.Join(' ', args)}");
            sb.AppendLine();
            sb.AppendLine(ex.ToString());
            File.WriteAllText(path, sb.ToString());
            return path;
        }
        catch (Exception)
        {
            return null;
        }
    }

    [DllImport("user32.dll", EntryPoint = "MessageBoxW", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(nint hWnd, string text, string caption, uint type);
}
