namespace Rts.Tools;

/// <summary>Process entry point. All logic lives in <see cref="Cli"/>.</summary>
public static class Program
{
    /// <summary>Runs the CLI with the given arguments and returns the exit code.</summary>
    public static int Main(string[] args) => Cli.Run(args);
}
