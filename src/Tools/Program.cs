namespace Rts.Tools;

/// <summary>Process entry point. All logic lives in <see cref="Cli"/>.</summary>
public static class Program
{
    /// <summary>
    /// Runs the CLI with the given arguments and returns the exit code. A lone <c>--version</c> prints the build
    /// identity (<see cref="Rts.Data.BuildInfo.Version"/>) and exits.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    public static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--version")
        {
            Console.WriteLine(Rts.Data.BuildInfo.Version);
            return 0;
        }
        return Cli.Run(args);
    }
}
