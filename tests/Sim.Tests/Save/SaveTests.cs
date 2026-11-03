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
        Assert.Equal(new[] { "map", "units", "infantry", "buildings", "paths", "vision", "combat", "projectiles", "players" }, sim.StateParts.Select(p => p.Name).ToArray());
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
}
