// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Scripting.Tests;

/// <summary>D-289: the optional <c>par_seconds</c> key of [mission] (60..36000, 0 = no par) and the shipped values.</summary>
public sealed class MissionParTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "rts_par_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private const string Players = "\n[[players]]\nslot = 0\nname = \"A\"\nrole = \"human\"\nteam = 0\ncash = 100\n";

    private MissionDef Load(string extraMission)
    {
        Directory.CreateDirectory(_dir);
        string toml = "[mission]\n" + extraMission + "title = \"T\"\nmap = \"m.map.toml\"\nscript = \"mission.lua\"\nbriefing = \"briefing.md\"\nseed = 1\n" + Players;
        File.WriteAllText(Path.Combine(_dir, "mission.toml"), toml);
        return MissionLoader.Load(_dir);
    }

    [Fact]
    public void A_mission_without_par_seconds_has_no_par()
    {
        Assert.Equal(0, Load("").ParSeconds);
    }

    [Fact]
    public void par_seconds_is_loaded()
    {
        Assert.Equal(480, Load("par_seconds = 480\n").ParSeconds);
    }

    [Theory]
    [InlineData(59)]
    [InlineData(36001)]
    [InlineData(0)]
    public void par_seconds_outside_60_to_36000_is_rejected(int value)
    {
        var e = Assert.Throws<InvalidDataException>(() => Load($"par_seconds = {value}\n"));
        Assert.Contains("par_seconds", e.Message);
    }

    [Fact]
    public void par_seconds_at_the_range_edges_is_accepted()
    {
        Assert.Equal(60, Load("par_seconds = 60\n").ParSeconds);
        Assert.Equal(36000, Load("par_seconds = 36000\n").ParSeconds);
    }

    [Fact]
    public void Every_shipped_mission_has_zero_or_a_par_in_range()
    {
        string missions = Path.Combine(TestData.DataDir, "missions");
        int withPar = 0;
        foreach (string folder in Directory.EnumerateDirectories(missions))
        {
            int par = MissionLoader.Load(folder).ParSeconds;
            Assert.True(par == 0 || par is >= 60 and <= 36000, $"{Path.GetFileName(folder)}: par {par}");
            if (par > 0) withPar++;
        }
        Assert.Equal(13, withPar);   // S47: 12 measured pars plus auth_07 (sw47a)
    }

    [Fact]
    public void The_timer_missions_have_no_par()
    {
        foreach (string id in new[] { "auth_02", "dir_03", "dir_04", "lb_03" })
            Assert.Equal(0, MissionLoader.Load(MissionLoader.FolderOf(TestData.DataDir, id)).ParSeconds);
    }

    [Fact]
    public void Shipped_pars_match_the_s37_suggestions()
    {
        var expected = new Dictionary<string, int>
        {
            ["auth_01"] = 480, ["auth_03"] = 540, ["auth_04"] = 540, ["dir_01"] = 300, ["dir_02"] = 1020, ["exp_01"] = 660,
            ["exp_02"] = 1020, ["exp_03"] = 900, ["exp_04"] = 1680, ["lb_01"] = 420, ["lb_02"] = 900, ["lb_04"] = 1260,
        };
        foreach ((string id, int par) in expected)
            Assert.Equal(par, MissionLoader.Load(MissionLoader.FolderOf(TestData.DataDir, id)).ParSeconds);
    }
}
