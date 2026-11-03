// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Tests.Rules;

/// <summary>
/// A copy of the fixture rules directory written under the test output folder. Overrides replace a file's
/// content (or delete it when the content is null). Deleted on dispose.
/// </summary>
internal sealed class TempRules : IDisposable
{
    public TempRules(params (string File, string? Content)[] overrides)
    {
        Dir = Path.Combine(AppContext.BaseDirectory, "rules-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Dir);
        foreach (string file in Directory.GetFiles(RealDir(), "*.toml"))
        {
            File.Copy(file, Path.Combine(Dir, Path.GetFileName(file)));
        }
        foreach ((string file, string? content) in overrides)
        {
            string path = Path.Combine(Dir, file);
            if (content is null)
            {
                File.Delete(path);
            }
            else
            {
                File.WriteAllText(path, content);
            }
        }
    }

    /// <summary>The temp copy's directory.</summary>
    public string Dir { get; }

    /// <summary>The fixture rules directory (see <see cref="TestData.RulesDir"/>).</summary>
    public static string RealDir() => TestData.RulesDir;

    /// <summary>Text of one fixture rules file.</summary>
    /// <remarks>Line endings are normalised to LF so tests behave the same on Windows checkouts.</remarks>
    public static string Real(string file) => File.ReadAllText(Path.Combine(RealDir(), file)).Replace("\r\n", "\n");

    /// <summary>The fixture file with <paramref name="old"/> replaced by <paramref name="new"/>; asserts that <paramref name="old"/> is present.</summary>
    public static string RealWith(string file, string old, string @new)
    {
        string text = Real(file);
        Assert.Contains(old, text);
        return text.Replace(old, @new);
    }

    public void Dispose()
    {
        if (Directory.Exists(Dir))
        {
            Directory.Delete(Dir, recursive: true);
        }
    }
}
