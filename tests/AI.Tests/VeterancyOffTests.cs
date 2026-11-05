// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI;
using Rts.AI.Recruit;
using Rts.Data;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Missions;
using Rts.Sim.Rules;
using Rts.Sim.View;
using Xunit;

namespace AI.Tests;

/// <summary>VET-6 / VET-4 (D-127): with <c>[veterancy] enabled = false</c> nothing about veterancy reaches the game. An AI-vs-AI game
/// repeats its state hash sequence exactly, every unit ranks 0, and the army value is the plain cost.</summary>
public sealed class VeterancyOffTests
{
    private const int Ticks = 3000;
    private const int Seed = 11;

    /// <summary>The loaded rules with veterancy switched off (the rules-file flag, not a code path).</summary>
    private static SimRules Off() => TestWorld.Rules.Sim with { Veterancy = TestWorld.Rules.Sim.Veterancy with { Enabled = false } };

    private sealed record Game(List<ulong> Hashes, Simulation? Last);

    /// <summary>Plays one short Recruit-AI mirror game and records the state hash after every tick.</summary>
    private static Game Play(SimRules rules)
    {
        var hashes = new List<ulong>();
        Simulation? last = null;
        DifficultyProfile tier3 = TestWorld.Tiers[2];
        var map = MapGenerator.Generate(48, Seed);
        AiMatchRunner.Play(map, rules, TestWorld.Rules.RulesHash, Seed,
            new AiSide(AiRegistry.TieredId, tier3), new AiSide(AiRegistry.TieredId, tier3), true, Ticks,
            observe: (sim, _, _, _, _) => { hashes.Add(sim.ComputeStateHash()); last = sim; });
        return new Game(hashes, last);
    }

    [Fact]
    public void AVeterancyOffGameRepeatsItsStateHashSequenceExactly()
    {
        Game first = Play(Off());
        Game second = Play(Off());
        Assert.NotEmpty(first.Hashes);
        Assert.True(first.Hashes.Count <= Ticks);
        Assert.Equal(first.Hashes, second.Hashes);
    }

    [Fact]
    public void WithVeterancyOffEveryUnitInTheGameRanksZero()
    {
        Simulation sim = Play(Off()).Last!;
        var units = new List<ViewActor>();
        for (byte p = 0; p < 2; p++)
        {
            var own = new List<ViewActor>();
            sim.ViewFor(p).CopyOwn(own, includeBuildings: false);
            units.AddRange(own);
        }
        Assert.NotEmpty(units);
        Assert.All(units, u => Assert.Equal(0, u.Rank));
        Assert.All(units, u => Assert.Equal(PlainCost(Off(), u), VeterancyValue.Of(Off(), u)));
    }

    [Fact]
    public void VeterancyValueIsThePlainCostForEveryUnitType()
    {
        SimRules rules = Off();
        for (int i = 0; i < rules.Vehicles.Count; i++)
            Assert.Equal(rules.Vehicles[i].Cost, VeterancyValue.Of(rules, Unit(ActorKind.Vehicle, i, 0)));
        for (int i = 0; i < rules.Infantry.Count; i++)
            Assert.Equal(rules.Infantry[i].Cost, VeterancyValue.Of(rules, Unit(ActorKind.Infantry, i, 0)));
    }

    // ---- helpers ---------------------------------------------------------------------------------

    private static ViewActor Unit(ActorKind kind, int typeIndex, int rank)
        => new(new EntityId(1, 1), kind, 0, Relation.Self, typeIndex, new CellCoord(10, 10), new LeptonCoord(10 * 256, 10 * 256),
            100, 100, MissionKind.Guard, true, EntityId.None, false, 1, 1) { Rank = rank };

    /// <summary>The rules cost of a mobile unit, read straight from its type (not through the value formula).</summary>
    private static int PlainCost(SimRules rules, ViewActor a) =>
        a.Kind == ActorKind.Vehicle ? rules.Vehicles[a.TypeIndex].Cost : a.Kind == ActorKind.Infantry ? rules.Infantry[a.TypeIndex].Cost : 0;
}
