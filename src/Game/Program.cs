namespace Rts.Game;

/// <summary>Process entry point for the MonoGame client.</summary>
public static class Program
{
    /// <summary>Opens the window and runs until the player quits.</summary>
    public static void Main()
    {
        using var game = new RtsGame();
        game.Run();
    }
}
