namespace Rts.Game;

/// <summary>Process entry point for the MonoGame client.</summary>
public static class Program
{
    /// <summary>Opens the window and runs until the player quits. Pass <c>--demo</c> for the fabricated demo source.</summary>
    /// <param name="args">Command-line arguments. Only <c>--demo</c> is recognised.</param>
    public static void Main(string[] args)
    {
        bool demo = args.Contains("--demo", StringComparer.Ordinal);
        using var game = new RtsGame(demo);
        game.Run();
    }
}
