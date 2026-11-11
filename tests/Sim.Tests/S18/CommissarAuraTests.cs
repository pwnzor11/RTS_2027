// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.S18;

/// <summary>D-205 / FAD-09 and FAD-20: the Commissar's own fire-rate aura, and the cheaper, softer Directorate structures.</summary>
public sealed class CommissarAuraTests
{
    [Fact]
    public void Commissar_HasAFireRateAura_ForOwnInfantry()
    {
        InfantryType c = S18World.RealRules.Infantry.Single(i => i.Id == "dir_commissar");
        Assert.Equal(3, c.AuraRadius);
        Assert.Equal(870, c.AuraRofPermille);
        Assert.Contains("dir_conscript", c.AuraTargets);
    }

    [Fact]
    public void ConscriptNearACommissar_ReloadsFaster()
    {
        var w = new S18World(Faction.Directorate, Faction.Authority);
        EntityId man = w.Inf(0, "dir_conscript", 28, 30, 0);
        w.Inf(0, "dir_commissar", 29, 30, 0);
        w.Run(11);
        Assert.Equal(870, w.Sim.Effects.RofPermille(ActorKind.Infantry, man.Index));
    }

    [Theory]
    [InlineData("dir_shaft", 800, 0)]
    [InlineData("dir_reactor_vent", 320, 260)]
    [InlineData("dir_smelter_vent", 560, 1700)]
    [InlineData("dir_muster_hatch", 360, 260)]
    [InlineData("dir_assembly_bore", 640, 1700)]
    [InlineData("dir_cistern", 200, 130)]
    [InlineData("dir_bunker", 160, 40)]
    [InlineData("dir_pillbox", 320, 340)]
    public void DirectorateStructures_AreSofterAndCheaperThanTheirAuthorityTwins(string id, int hp, int cost)
    {
        BuildingType s = S18World.RealRules.Buildings.Single(x => x.Id == id);
        Assert.Equal(hp, s.MaxHealth);
        Assert.Equal(cost, s.Cost);
    }
}
