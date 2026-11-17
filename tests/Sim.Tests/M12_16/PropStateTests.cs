// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Rules;

namespace Rts.Sim.Tests.M12_16;

/// <summary>M12-16 step 1 (D-264 (3)): the [props] rules table, the "props" state part, its hash and its save.</summary>
public sealed class PropStateTests
{
    private static readonly CellCoord Tree = new(3, 3), Snag = new(4, 3), Wreck = new(5, 3), Block = new(6, 3), Pylon = new(7, 3), Wall = new(8, 3);

    private static MapData Dressed() => PropFixture.Map(16,
        (Tree, OverlayKind.Tree), (Snag, OverlayKind.Snag), (Wreck, OverlayKind.Wreck), (Block, OverlayKind.Block), (Pylon, OverlayKind.Pylon), (Wall, OverlayKind.Wall));

    [Fact]
    public void ShippedRules_HaveAPropsTable_WithATougherWreck()
    {
        string? dir = RulesLocator.FindRulesDir(AppContext.BaseDirectory);
        Assert.NotNull(dir);
        PropRules p = RulesLoader.Load(dir!).Sim.Props;
        Assert.True(p.TreeHealth > 0 && p.SnagHealth > 0);
        Assert.InRange(p.WreckHealth, 3 * p.TreeHealth, 4 * p.TreeHealth);   // "Wreck tough, 3-4x a Tree"
        Assert.Equal(ArmorClass.Structure, p.SplashArmor);
        Assert.True(p.CrushPathPenalty > 1);
    }

    [Fact]
    public void PropsTable_Parses_DefaultsWhenAbsent_AndRejectsBadInput()
    {
        using (var t = new TempRules()) Assert.NotNull(RulesLoader.Load(t.Dir).Sim.Props);
        string game = TempRules.Real("game.toml");
        int at = game.IndexOf("[props]", StringComparison.Ordinal);
        Assert.True(at > 0);
        string without = game[..at];
        using (var t = new TempRules(("game.toml", without))) Assert.Equal(PropRules.Default, RulesLoader.Load(t.Dir).Sim.Props);
        using (var t = new TempRules(("game.toml", without + "[props]\ntree_health = 77\nsplash_armor = \"light\"\n")))
        {
            PropRules p = RulesLoader.Load(t.Dir).Sim.Props;
            Assert.Equal(77, p.TreeHealth);
            Assert.Equal(ArmorClass.Light, p.SplashArmor);
            Assert.Equal(PropRules.Default.WreckHealth, p.WreckHealth);
        }
        using (var t = new TempRules(("game.toml", without + "[props]\nbogus = 1\n"))) Assert.Throws<RulesException>(() => RulesLoader.Load(t.Dir));
        using (var t = new TempRules(("game.toml", without + "[props]\nsplash_armor = \"wood\"\n"))) Assert.Throws<RulesException>(() => RulesLoader.Load(t.Dir));
        using (var t = new TempRules(("game.toml", without + "[props]\ntree_health = 0\n"))) Assert.Throws<RulesException>(() => RulesLoader.Load(t.Dir));
    }

    [Fact]
    public void RulesHash_CoversTheProps()
    {
        string game = TempRules.Real("game.toml");
        string without = game[..game.IndexOf("[props]", StringComparison.Ordinal)];
        using var a = new TempRules(("game.toml", without + "[props]\ntree_health = 40\n"));
        using var b = new TempRules(("game.toml", without + "[props]\ntree_health = 41\n"));
        using var c = new TempRules(("game.toml", without + "[props]\ncrush_path_penalty = 4\n"));
        ulong ha = RulesLoader.Load(a.Dir).RulesHash, hb = RulesLoader.Load(b.Dir).RulesHash, hc = RulesLoader.Load(c.Dir).RulesHash;
        Assert.NotEqual(ha, hb);
        Assert.NotEqual(ha, hc);
    }

    [Fact]
    public void Part_SeedsOnlyTheDestructibleProps_FromTheOverlay()
    {
        Simulation sim = PropFixture.Sim(Dressed());
        PropState p = sim.Props;
        PropRules r = PropRules.Default;
        Assert.Equal("props", p.Name);
        Assert.Equal(r.TreeHealth, p.HealthAt(Tree));
        Assert.Equal(r.SnagHealth, p.HealthAt(Snag));
        Assert.Equal(r.WreckHealth, p.HealthAt(Wreck));
        Assert.Equal(0, p.HealthAt(Block));    // never breaks
        Assert.Equal(0, p.HealthAt(Pylon));
        Assert.Equal(0, p.HealthAt(Wall));     // player walls have their own rules
        Assert.Equal(3, p.Count);
        Assert.Equal(0, PropFixture.Sim(PropFixture.Map(16)).Props.Count);
    }

    [Fact]
    public void Part_UsesTheRulesHealth()
    {
        var rules = new PropRules(10, 20, 30, ArmorClass.Light, 2);
        Simulation sim = PropFixture.Sim(Dressed(), rules);
        Assert.Equal((10, 20, 30), (sim.Props.HealthAt(Tree), sim.Props.HealthAt(Snag), sim.Props.HealthAt(Wreck)));
    }

    [Fact]
    public void Part_IsInTheHash_AndOnlyThePropsSubHashMoves()
    {
        Simulation sim = PropFixture.Sim(Dressed());
        var before = sim.ComputeSubHashes().ToList();
        ulong full = sim.ComputeStateHash();
        sim.Props.Health[Tree.Y * 16 + Tree.X] -= 5;
        var after = sim.ComputeSubHashes().ToList();
        Assert.NotEqual(full, sim.ComputeStateHash());
        Assert.Equal("props", Simulation.FirstDifferentPart(before, after));
        Assert.Equal(1, before.Zip(after).Count(z => z.First.Hash != z.Second.Hash));
    }

    [Fact]
    public void Save_RoundTrips_DamagedHealth_MidGame()
    {
        MapData map = Dressed();
        var setup = new SimSetup(1, map, 2, PropFixture.Rules(), 1) { AllowDebug = true };
        var sim = new Simulation(setup);
        for (int i = 0; i < 10; i++) sim.Step(Array.Empty<Command>());
        sim.Props.Health[Wreck.Y * 16 + Wreck.X] = 77;
        sim.Props.Health[Tree.Y * 16 + Tree.X] = 0;
        var ms = new MemoryStream();
        sim.Save(ms);
        ms.Position = 0;
        Simulation loaded = Simulation.Load(ms, setup);
        Assert.Equal(77, loaded.Props.HealthAt(Wreck));
        Assert.Equal(0, loaded.Props.HealthAt(Tree));
        Assert.Equal(PropRules.Default.SnagHealth, loaded.Props.HealthAt(Snag));
        Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
    }
}
