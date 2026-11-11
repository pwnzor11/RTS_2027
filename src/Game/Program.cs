// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Runtime.InteropServices;
using System.Text;
using Rts.Data;
using Rts.Game.Sandbox;

namespace Rts.Game;

/// <summary>Process entry point for the MonoGame client.</summary>
public static class Program
{
    /// <summary>
    /// Opens the game at the main menu and runs until the player quits. Options: <c>--sandbox</c> starts directly in the
    /// battle sandbox (the old behaviour); <c>--empty</c> makes the sandbox use the legacy 3-tank
    /// setup; <c>--demo</c> draws the fabricated demo source instead of the sim; <c>--headless-check &lt;ticks&gt;</c>
    /// runs the scripted sandbox fight with no window, saves the replay and exits; <c>--skirmish-check &lt;ticks&gt;</c> runs the
    /// menu's skirmish (idle player 0 against the AI) with no window and prints the result; <c>--mission-check &lt;id&gt; &lt;ticks&gt;</c> does
    /// the same for a campaign mission (<see cref="MissionCheck"/>). Any unhandled exception writes
    /// <c>logs/crash-*.txt</c> beside the exe (and a copy under the application-data folder) and returns exit code 1 (C-6).
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>The exit code: 0 on success, 1 after a crash, otherwise the headless check's result.</returns>
    public static int Main(string[] args)
    {
        AppLog.Install();   // S18 GAMEREND-03: a WinExe has no console, so Console.Error lines also go to %APPDATA%/RTS_2027/logs
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            CrashReport.Report(e.ExceptionObject as Exception ?? new InvalidOperationException("Unknown exception."), args);

        try
        {
            if (args.Contains("--headless-check", StringComparer.Ordinal)) return HeadlessCheck.Run(args);
            if (args.Contains("--skirmish-check", StringComparer.Ordinal)) return SkirmishCheck.Run(args);
            if (args.Contains("--mission-check", StringComparer.Ordinal)) return MissionCheck.Run(args);

            bool demo = args.Contains("--demo", StringComparer.Ordinal);
            bool empty = args.Contains("--empty", StringComparer.Ordinal);
            bool sandbox = args.Contains("--sandbox", StringComparer.Ordinal);
            using var game = new RtsGame(demo, empty, sandbox);
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

        AppLog.Line("crash: " + ex.GetType().Name + ": " + ex.Message);
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
        var sb = new StringBuilder();
        sb.AppendLine("RTS_2027 crash report");
        sb.AppendLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine($"Version: {BuildInfo.Version}");
        sb.AppendLine($"OS: {RuntimeInformation.OSDescription} ({RuntimeInformation.ProcessArchitecture})");
        sb.AppendLine($"Runtime: {RuntimeInformation.FrameworkDescription}");
        sb.AppendLine($"Arguments: {string.Join(' ', args)}");
        sb.AppendLine();
        sb.AppendLine(ex.ToString());
        string text = sb.ToString();

        // Beside the exe (fails under a read-only install) and, S18 GAMEREND-03, under the application-data folder with the other logs.
        string name = $"crash-{DateTime.Now:yyyyMMdd-HHmmss}.txt";
        string? beside = TryWrite(Path.Combine(AppContext.BaseDirectory, "logs"), name, text);
        string? appData = TryWrite(Path.GetDirectoryName(AppLog.DefaultPath) ?? ".", name, text);
        return beside ?? appData;
    }

    private static string? TryWrite(string dir, string name, string text)
    {
        try
        {
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, name);
            File.WriteAllText(path, text);
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
