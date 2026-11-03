namespace Rts.Data;

/// <summary>Finds the rules directory by walking up from a starting directory.</summary>
public static class RulesLocator
{
    /// <summary>Returns the first <c>data/rules</c> directory found walking up from <paramref name="startDirectory"/>, or null.</summary>
    public static string? FindRulesDir(string startDirectory)
    {
        var dir = new DirectoryInfo(Path.GetFullPath(startDirectory));
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "data", "rules");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }
        return null;
    }
}
