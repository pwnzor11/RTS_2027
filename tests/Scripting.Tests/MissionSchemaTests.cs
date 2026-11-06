// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Scripting.Tests;

/// <summary>M8-02: the loader reads schema 1 (auth_01, unchanged) and schema 2 (plan 3.5).</summary>
public sealed class MissionSchemaTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "rts_schema_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private const string Players = "\n[[players]]\nslot = 0\nname = \"A\"\nrole = \"human\"\nteam = 0\ncash = 100\n";

    private MissionDef Load(string toml)
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "mission.toml"), toml);
        return MissionLoader.Load(_dir);
    }

    private static string Header(string extraMission) =>
        "[mission]\n" + extraMission + "title = \"T\"\nmap = \"m.map.toml\"\nscript = \"mission.lua\"\nbriefing = \"briefing.md\"\nseed = 1\n";

    [Fact]
    public void Pilot_mission_loads_as_schema_one_with_empty_v2_fields()
    {
        MissionDef d = MissionLoader.Load(MissionLoader.FolderOf(TestData.DataDir, "auth_01"));
        Assert.Equal(1, d.Schema);
        Assert.Equal(0, d.Act);
        Assert.Equal("", d.Faction);
        Assert.Empty(d.Next);
        Assert.Empty(d.StartsWith);
        Assert.Empty(d.UnlocksOnWin);
        Assert.Equal("", d.LocaleFile);
        Assert.Equal("Survey Convoy", d.Title);
    }

    [Fact]
    public void Schema_two_reads_every_v2_key()
    {
        MissionDef d = Load(Header("schema = 2\nact = 3\nfaction = \"left_behind\"\nnext = [\"lb_04a\", \"lb_04b\"]\nlocale = \"locale.toml\"\n")
            + Players + "\n[starts_with]\ntech = [\"auth_power\", \"auth_rifleman\"]\n\n[unlocks_on_win]\ntech = [\"auth_wall\"]\n");
        Assert.Equal(2, d.Schema);
        Assert.Equal(3, d.Act);
        Assert.Equal("left_behind", d.Faction);
        Assert.Equal(new[] { "lb_04a", "lb_04b" }, d.Next);
        Assert.Equal("locale.toml", d.LocaleFile);
        Assert.Equal(new[] { "auth_power", "auth_rifleman" }, d.StartsWith);
        Assert.Equal(new[] { "auth_wall" }, d.UnlocksOnWin);
        Assert.Equal("T", d.Title);
    }

    [Fact]
    public void Schema_two_accepts_a_single_next_id_and_missing_optional_keys()
    {
        MissionDef d = Load(Header("schema = 2\nnext = \"auth_02\"\n") + Players);
        Assert.Equal(new[] { "auth_02" }, d.Next);
        Assert.Equal(0, d.Act);
        Assert.Empty(d.StartsWith);
    }

    [Theory]
    [InlineData("schema = 3\n", "")]
    [InlineData("act = 2\n", "")]                                   // v2 key without schema = 2
    [InlineData("schema = 2\nact = 5\n", "")]
    [InlineData("schema = 2\nfaction = \"martians\"\n", "")]
    [InlineData("schema = 2\nnext = [\"a\", \"b\", \"c\"]\n", "")]
    [InlineData("schema = 2\nnext = 4\n", "")]
    [InlineData("", "\n[starts_with]\ntech = [\"x\"]\n")]          // v2 table without schema = 2
    [InlineData("schema = 2\n", "\n[unlocks_on_win]\ntech = \"x\"\n")]
    public void Bad_schema_use_is_rejected_with_the_file_name(string mission, string tail)
    {
        var e = Assert.Throws<InvalidDataException>(() => Load(Header(mission) + Players + tail));
        Assert.Contains("mission.toml", e.Message);
    }
}
