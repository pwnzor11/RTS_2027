namespace Rts.Data;

/// <summary>Thrown when a map file cannot be read or fails validation.</summary>
public sealed class MapFileException : Exception
{
    public MapFileException(string path, string message)
        : base($"{path}: {message}")
    {
        Path = path;
    }

    public MapFileException(string path, string message, Exception inner)
        : base($"{path}: {message}", inner)
    {
        Path = path;
    }

    /// <summary>The file that failed.</summary>
    public string Path { get; }
}
