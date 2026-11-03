using Rts.Game.Sandbox;

namespace Rts.Game;

/// <summary>Process entry point for the MonoGame client.</summary>
public static class Program
{
    /// <summary>
    /// Opens the battle sandbox and runs until the player quits. Options: <c>--empty</c> starts with the legacy 3-tank
    /// setup; <c>--demo</c> draws the fabricated demo source instead of the sim; <c>--headless-check &lt;ticks&gt;</c>
    /// runs the scripted sandbox fight with no window, saves the replay and exits.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>The exit code (only the headless check returns anything but 0 on success).</returns>
    public static int Main(string[] args)
    {
        if (args.Contains("--headless-check", StringComparer.Ordinal)) return HeadlessCheck.Run(args);

        bool demo = args.Contains("--demo", StringComparer.Ordinal);
        bool empty = args.Contains("--empty", StringComparer.Ordinal);
        using var game = new RtsGame(demo, empty);
        game.Run();
        return 0;
    }
}
