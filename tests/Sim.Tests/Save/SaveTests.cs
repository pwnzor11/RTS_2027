// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Tests.Determinism;

namespace Rts.Sim.Tests.Save;

public class SaveTests
{
    [Fact]
    public void SaveRoundTrip_ThenStepN_MatchesStraightRun()
    {
        var setup = ScriptedGame.Setup();
        var script = ScriptedGame.Script(500);
        var straight = ScriptedGame.NewSim(setup);
        for (int i = 0; i < 200; i++) straight.Step(script);

        var ms = new MemoryStream();
        straight.Save(ms);
        ms.Position = 0;
        var loaded = Simulation.Load(ms, setup);
        Assert.Equal(straight.Tick, loaded.Tick);
        Assert.Equal(straight.ComputeStateHash(), loaded.ComputeStateHash());

        for (int i = 0; i < 300; i++)
        {
            straight.Step(script); loaded.Step(script);
            Assert.Equal(straight.ComputeStateHash(), loaded.ComputeStateHash());
        }
    }

    [Fact]
    public void Load_RejectsBadMagicVersionAndRulesHash()
    {
        var setup = ScriptedGame.Setup();
        var sim = ScriptedGame.NewSim(setup);
        var ms = new MemoryStream(); sim.Save(ms);
        byte[] good = ms.ToArray();

        byte[] badMagic = (byte[])good.Clone(); badMagic[0] ^= 0xFF;
        Assert.Throws<InvalidDataException>(() => Simulation.Load(new MemoryStream(badMagic), setup));

        byte[] badVer = (byte[])good.Clone(); badVer[4] = 99;
        Assert.Throws<InvalidDataException>(() => Simulation.Load(new MemoryStream(badVer), setup));

        Assert.Throws<InvalidDataException>(() =>
            Simulation.Load(new MemoryStream(good), setup with { RulesHash = setup.RulesHash + 1 }));
    }

    [Fact]
    public void Load_RejectsWrongMapHash()
    {
        var setup = ScriptedGame.Setup();
        var sim = ScriptedGame.NewSim(setup);
        var ms = new MemoryStream(); sim.Save(ms);
        var other = setup with { Map = MapData.CreateFlat(64, 64, "other") };
        var ex = Assert.Throws<InvalidDataException>(() => Simulation.Load(new MemoryStream(ms.ToArray()), other));
        Assert.Contains("Map hash", ex.Message);
    }

    [Fact]
    public void StateParts_AreInCanonicalOrder()
    {
        var sim = ScriptedGame.NewSim(ScriptedGame.Setup());
        Assert.Equal(new[] { "map", "units", "infantry", "buildings", "paths", "vision", "combat", "projectiles", "players", "economy", "production",
            "tunnels", "ground", "effects" }, sim.StateParts.Select(p => p.Name).ToArray());   // M3-01 appended the last three
    }

    [Fact]
    public void SaveRoundTrip_PreservesInfantry()
    {
        var setup = ScriptedGame.Setup();
        var sim = ScriptedGame.NewSim(setup);
        var script = ScriptedGame.Script(100);
        for (int i = 0; i < 60; i++) sim.Step(script);
        var ms = new MemoryStream(); sim.Save(ms); ms.Position = 0;
        var loaded = Simulation.Load(ms, setup);
        for (int i = 1; i <= 10; i++)
        {
            Assert.Equal(sim.Infantry.Position[i], loaded.Infantry.Position[i]);
            Assert.Equal(sim.Infantry.SubSlot[i], loaded.Infantry.SubSlot[i]);
            Assert.Equal(sim.Infantry.Mission[i], loaded.Infantry.Mission[i]);
            Assert.Equal(sim.Infantry.WakeTick[i], loaded.Infantry.WakeTick[i]);
        }
        Assert.Contains(Enumerable.Range(1, 10), i => sim.Infantry.Mission[i] != Rts.Sim.Missions.MissionKind.None);
        Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
    }

    [Fact]
    public void SaveRoundTrip_PreservesFreeSlotsAndGenerations()
    {
        var setup = ScriptedGame.Setup();
        var sim = ScriptedGame.NewSim(setup);
        sim.Units.Remove(new EntityId(3, 1));
        var ms = new MemoryStream(); sim.Save(ms); ms.Position = 0;
        var loaded = Simulation.Load(ms, setup);
        var a = sim.SpawnVehicle(0, 0, new CellCoord(1, 1));
        var b = loaded.SpawnVehicle(0, 0, new CellCoord(1, 1));
        Assert.Equal(new EntityId(3, 2), a);
        Assert.Equal(a, b);
    }

    private static readonly IReadOnlyList<(string Name, string Text)> Files =
        new[] { ("game.toml", "[game]\ntick_rate = 20\n"), ("vehicles.toml", new string('v', 3000)) };

    private static SimSetup CustomSetup() => ScriptedGame.Setup(9) with
    {
        Teams = new byte[] { 1, 1 }, Limits = new SimLimits(300, 200, 100, 64), AllowDebug = true,
    };

    [Fact]
    public void LoadWithoutASetup_RebuildsItFromTheSaveAndItsEmbeddedRules()
    {
        var setup = CustomSetup();
        var sim = ScriptedGame.NewSim(setup);
        var script = ScriptedGame.Script(150);
        for (int i = 0; i < 100; i++) sim.Step(script);
        var ms = new MemoryStream(); sim.Save(ms, "9.9.9+abc", Files);

        IReadOnlyList<(string Name, string Text)>? seen = null;
        var loaded = Simulation.Load(new MemoryStream(ms.ToArray()), files =>
        {
            seen = files;
            return (setup.Rules, setup.RulesHash);
        }, setup.Map);

        Assert.Equal(Files, seen);
        Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
        Assert.Equal(setup.Seed, loaded.Setup.Seed);
        Assert.Equal(setup.PlayerCount, loaded.Setup.PlayerCount);
        Assert.Equal(setup.Teams, loaded.Setup.Teams);
        Assert.Equal(setup.Limits, loaded.Setup.Limits);
        Assert.True(loaded.Setup.AllowDebug);
        for (int i = 0; i < 50; i++)
        {
            sim.Step(script); loaded.Step(script);
            Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
        }
    }

    [Fact]
    public void LoadWithoutASetup_RejectsRulesOrMapThatDoNotMatchTheHeader()
    {
        var setup = CustomSetup();
        var ms = new MemoryStream(); ScriptedGame.NewSim(setup).Save(ms, "b", Files);
        byte[] bytes = ms.ToArray();
        Assert.Throws<InvalidDataException>(() =>
            Simulation.Load(new MemoryStream(bytes), _ => (setup.Rules, setup.RulesHash + 1), setup.Map));
        Assert.Throws<InvalidDataException>(() =>
            Simulation.Load(new MemoryStream(bytes), _ => (setup.Rules, setup.RulesHash), MapData.CreateFlat(64, 64, "other")));
    }

    [Fact]
    public void Load_RejectsASetupThatDiffersInTeamsLimitsDebugOrPlayers()
    {
        var setup = CustomSetup();
        var ms = new MemoryStream(); ScriptedGame.NewSim(setup).Save(ms);
        byte[] bytes = ms.ToArray();
        Assert.NotNull(Simulation.Load(new MemoryStream(bytes), setup));
        Assert.Throws<InvalidDataException>(() => Simulation.Load(new MemoryStream(bytes), setup with { Teams = null }));
        Assert.Throws<InvalidDataException>(() => Simulation.Load(new MemoryStream(bytes), setup with { Limits = SimLimits.Default }));
        Assert.Throws<InvalidDataException>(() => Simulation.Load(new MemoryStream(bytes), setup with { AllowDebug = false }));
    }

    [Fact]
    public void OldSaveVersion_IsRejectedWithAClearMessage()
    {
        var ms = new MemoryStream(); ScriptedGame.NewSim(ScriptedGame.Setup()).Save(ms);
        byte[] bytes = ms.ToArray();
        bytes[4] = 6;   // M3-01 bumped saves to 7
        var ex = Assert.Throws<InvalidDataException>(() => Simulation.Load(new MemoryStream(bytes), ScriptedGame.Setup()));
        Assert.Contains("version 6", ex.Message);
    }

    [Fact]
    public void TruncatedSave_IsAnInvalidDataError()
    {
        var setup = ScriptedGame.Setup();
        var ms = new MemoryStream(); ScriptedGame.NewSim(setup).Save(ms);
        byte[] bytes = ms.ToArray();
        Assert.Throws<InvalidDataException>(() => Simulation.Load(new MemoryStream(bytes[..40]), setup));
        Assert.Throws<InvalidDataException>(() => Simulation.Load(new MemoryStream(bytes[..^50]), setup));
    }
}
