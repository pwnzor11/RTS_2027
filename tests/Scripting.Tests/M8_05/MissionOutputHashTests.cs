// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Scripting.Tests;

/// <summary>
/// M8-05: dialogue and camera hints are outputs (plan 3.7, D-071). A script that only adds them leaves every sim part unchanged. The
/// "mission" part also stores the script's source hash, so runs with different script text cannot share it; those runs are compared
/// part by part, skipping "mission", and the mission state value is compared separately.
/// </summary>
public sealed class MissionOutputHashTests
{
    private const string Header = "state = { n = 0 }\nfunction OnInit() Trigger.Every(3, Tick) end\n";

    [Fact]
    public void Dialogue_and_camera_calls_leave_every_sim_part_unchanged()
    {
        const string plain = Header + "function Tick() state.n = state.n + 1 end";
        const string outputs = Header +
            "function Tick() state.n = state.n + 1; Dialogue.Say('strand', 'strand_stern', 'm1.warning'); " +
            "Camera.Focus('z', 30); Dialogue.Say('strand', 'strand_stern', 'm1.line2'); Dialogue.Clear(); " +
            "Dialogue.Say('strand', 'strand_stern', 'm1.line2') end";

        var (plainSim, plainHost) = M85Mission.Build(plain, M85Mission.Sample());
        var (outSim, outHost) = M85Mission.Build(outputs, M85Mission.Sample());
        TestData.Run(plainSim, 60);
        TestData.Run(outSim, 60);

        Assert.Empty(plainHost.Errors);
        Assert.Empty(outHost.Errors);
        Assert.Single(outHost.Dialogue);
        Assert.True(outHost.CameraHint.Seq > 0);

        Assert.Equal(TestAccess.StateValue(plainHost, "n"), TestAccess.StateValue(outHost, "n"));
        var plainParts = plainSim.ComputeSubHashes();
        var outParts = outSim.ComputeSubHashes();
        Assert.Equal(plainParts.Select(p => p.Part), outParts.Select(p => p.Part));
        foreach ((string part, ulong hash) in plainParts.Where(p => p.Part != "mission"))
            Assert.Equal(hash, outParts.Single(p => p.Part == part).Hash);
    }

    [Fact]
    public void The_same_script_with_different_locale_text_has_the_same_hash_and_save_bytes()
    {
        const string lua = "function OnInit() Dialogue.Say('a', 'p', 'm1.line2'); Camera.Focus('z', 9) end";
        var (a, ha) = M85Mission.Build(lua, M85Mission.Sample());
        var (b, hb) = M85Mission.Build(lua, MissionLocale.Parse("\"m1.line2\" = \"A different line\"\n", "other.toml"));
        TestData.Run(a, 3);
        TestData.Run(b, 3);

        Assert.NotEqual(ha.Dialogue[0].Text, hb.Dialogue[0].Text);
        Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
        using var sa = new MemoryStream();
        using var sb = new MemoryStream();
        a.Save(sa);
        b.Save(sb);
        Assert.Equal(sb.ToArray(), sa.ToArray());
    }
}
