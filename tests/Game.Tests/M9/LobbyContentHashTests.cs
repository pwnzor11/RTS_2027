// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Lan;
using Xunit;

namespace Rts.Game.Tests.M9;

/// <summary>M9-13: the content hash follows gameplay data only.</summary>
public sealed class LobbyContentHashTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "rts_ch_" + Guid.NewGuid().ToString("N"));

    public LobbyContentHashTests()
    {
        Write("rules/units.toml", "a = 1\nb = 2\n");
        Write("maps/m.map.toml", "name = \"m\"\n");
        Write("missions/x/script.lua", "print(1)\n");
        Write("audio/s.toml", "x = 1\n");
    }

    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

    private void Write(string rel, string text)
    {
        string path = Path.Combine(_dir, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    [Fact]
    public void SameDataSameHash() => Assert.Equal(ContentHash.Compute(_dir), ContentHash.Compute(_dir));

    [Fact]
    public void RulesChangeChangesHash()
    {
        ulong before = ContentHash.Compute(_dir);
        Write("rules/units.toml", "a = 1\nb = 3\n");
        Assert.NotEqual(before, ContentHash.Compute(_dir));
    }

    [Fact]
    public void ScriptAndMapChangesChangeHash()
    {
        ulong before = ContentHash.Compute(_dir);
        Write("missions/x/script.lua", "print(2)\n");
        ulong afterScript = ContentHash.Compute(_dir);
        Assert.NotEqual(before, afterScript);
        Write("maps/new.map.toml", "name = \"n\"\n");
        Assert.NotEqual(afterScript, ContentHash.Compute(_dir));
    }

    [Fact]
    public void AudioIsIgnored()
    {
        ulong before = ContentHash.Compute(_dir);
        Write("audio/s.toml", "x = 2\n");
        Write("sprites/a.toml", "y = 2\n");
        Assert.Equal(before, ContentHash.Compute(_dir));
    }

    [Fact]
    public void LineEndingsDoNotMatter()
    {
        ulong before = ContentHash.Compute(_dir);
        File.WriteAllText(Path.Combine(_dir, "rules/units.toml"), "a = 1\r\nb = 2\r\n");
        Assert.Equal(before, ContentHash.Compute(_dir));
    }

    [Fact]
    public void RealDataFolderHashesAndIsStable()
    {
        string data = Path.Combine(AppContext.BaseDirectory, "data");
        if (!Directory.Exists(data)) return;
        Assert.Equal(ContentHash.Compute(data), ContentHash.Compute(data));
        Assert.NotEqual(ContentHash.Compute(data), ContentHash.Compute(_dir));
    }
}
