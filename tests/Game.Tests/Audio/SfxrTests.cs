// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).

using System.Diagnostics;

namespace Rts.Game.Tests.Audio;

/// <summary>tools/audio/sfxr.py is deterministic: the same presets give byte-identical WAV files, and the committed files match.</summary>
public sealed class SfxrTests
{
    private static string? FindPython()
    {
        foreach (string exe in new[] { "python3", "python" })
        {
            try
            {
                var psi = new ProcessStartInfo(exe, "-I -c \"import sys; assert sys.version_info >= (3, 11)\"")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using Process? p = Process.Start(psi);
                if (!p!.WaitForExit(20000))
                {
                    // A slow or hanging launcher (e.g. a Windows Store stub on a busy CI runner): treat as "not available".
                    try { p.Kill(true); } catch (InvalidOperationException) { }
                    continue;
                }
                if (p.ExitCode == 0) return exe;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // not installed under this name
            }
        }

        return null;
    }

    private static void Generate(string python, string outDir)
    {
        string script = Path.Combine(AudioTestSupport.RepoRoot(), "tools", "audio", "sfxr.py");
        var psi = new ProcessStartInfo(python, $"-I \"{script}\" generate --out \"{outDir}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using Process p = Process.Start(psi)!;
        p.StandardOutput.ReadToEnd();
        string err = p.StandardError.ReadToEnd();
        p.WaitForExit();
        Assert.True(p.ExitCode == 0, err);
    }

    /// <summary>S18 TESTS-REPO-05: on CI a missing Python must fail, not pass silently.</summary>
    private static bool OnCi() => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI"));

    [Fact]
    public void SameSeedGivesByteIdenticalWavs()
    {
        string? python = FindPython();
        if (python is null) { Assert.False(OnCi(), "Python 3.11+ is required on CI for this test"); return; } // locally: the Python-side test in tools/audio covers it

        string root = Path.Combine(Path.GetTempPath(), "rts2027_sfxr_" + Guid.NewGuid().ToString("N"));
        try
        {
            Generate(python, Path.Combine(root, "a"));
            Generate(python, Path.Combine(root, "b"));
            string[] files = Directory.GetFiles(Path.Combine(root, "a"), "*.wav");
            Assert.True(files.Length >= 20);
            foreach (string f in files)
            {
                byte[] other = File.ReadAllBytes(Path.Combine(root, "b", Path.GetFileName(f)));
                Assert.Equal(File.ReadAllBytes(f), other);
            }
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void CommittedWavFilesMatchThePresets()
    {
        string? python = FindPython();
        if (python is null) { Assert.False(OnCi(), "Python 3.11+ is required on CI for this test"); return; }

        string root = Path.Combine(Path.GetTempPath(), "rts2027_sfxr_" + Guid.NewGuid().ToString("N"));
        try
        {
            Generate(python, root);
            foreach (string f in Directory.GetFiles(root, "*.wav"))
            {
                string committed = Path.Combine(AudioTestSupport.AudioDir(), "sfx", Path.GetFileName(f));
                Assert.Equal(File.ReadAllBytes(committed), File.ReadAllBytes(f));
            }
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
