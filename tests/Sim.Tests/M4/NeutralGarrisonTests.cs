// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Garrison;
using Rts.Sim.Map;
using Rts.Sim.Replay;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.M4;

/// <summary>M4-15 (design 08 §3, §8): a generated map's ruins are placed as neutral before tick 0 (setup log); entering flips the
/// owner and emptying reverts it; flipped ruins never count for defeat or tiers; replays verify.</summary>
public sealed class NeutralGarrisonTests
{
    private const byte Neutral = 2;
    private static readonly Command[] None = Array.Empty<Command>();
    private static readonly IReadOnlyList<(string Name, string Text)> RulesFiles = new[] { ("game.toml", "[game]\ntick_rate = 20\n") };
    private static ushort _seq;

    private static (RuleSet Rules, MapData Map, MapMeta Meta) Generated()
    {
        string dir = RulesLocator.FindRulesDir(AppContext.BaseDirectory)!;
        RuleSet rules = RulesLoader.Load(dir);
        for (ulong seed = 1; seed < 40; seed++)
        {
            var (map, meta) = MapGenerator.GenerateWithMeta(new MapGenParams(Biome.RustBelt, MapSize.Small, 2, seed));
            if (meta.Features.Count >= 2) return (rules, map, meta);
        }
        throw new InvalidOperationException("no generated map with 2 ruins");
    }

    private static SimSetup SetupFor(RuleSet rules, MapData map) =>
        new(7, map, 3, rules.Sim, rules.RulesHash) { AllowDebug = true, Teams = new byte[] { 0, 1, Teams.Neutral } };

    private static int Garrisoner(SimRules r)
    {
        for (int i = 0; i < r.Infantry.Count; i++) if (r.Infantry[i].CanGarrison && r.Infantry[i].PrimaryWeapon >= 0) return i;
        throw new InvalidOperationException("no garrison-capable infantry in data");
    }

    private static Command Enter(Simulation sim, byte player, EntityId who, EntityId building) =>
        new(sim.Tick, player, _seq++, CommandKind.Enter, who, building, default, (int)ActorKind.Infantry | ((int)ActorKind.Building << 8), 0);

    private static EntityId SpawnBeside(Simulation sim, byte player, int type, EntityId ruin)
    {
        CellCoord tl = sim.Buildings.Cell[ruin.Index];
        for (int r = 1; r <= 3; r++)
            for (int dx = -r; dx <= r + 2; dx++)
                foreach (int dy in new[] { -r, r + 1 })
                {
                    EntityId id = sim.SpawnInfantry(player, type, new CellCoord(tl.X + dx, tl.Y + dy));
                    if (!id.IsNone) return id;
                }
        throw new InvalidOperationException("no free cell next to the ruin");
    }

    private static (Simulation Sim, EntityId[] Ruins, int Type, MapMeta Meta) Scene()
    {
        var (rules, map, meta) = Generated();
        var sim = new Simulation(SetupFor(rules, map));
        EntityId[] ruins = MapFeatures.Place(sim, meta, Neutral);
        PlaceBase(sim, 0); PlaceBase(sim, 1);   // a real base each, so garrisoning is not what keeps a player alive
        return (sim, ruins, Garrisoner(rules.Sim), meta);
    }

    private static EntityId PlaceBase(Simulation sim, byte player)
    {
        int type = SimRules.IndexOf(sim.Setup.Rules.Buildings, "auth_conyard", b => b.Id);
        CellCoord s = sim.Setup.Map.StartPositions[player];
        for (int r = 0; r <= 8; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    EntityId id = sim.PlaceBuilding(player, type, new CellCoord(s.X + dx - 1, s.Y + dy - 1));
                    if (!id.IsNone) return id;
                }
        throw new InvalidOperationException("no spot for a base");
    }

    private static int FirstBuildingOf(Simulation sim, byte player)
    {
        for (int i = 1; i < sim.Buildings.Capacity; i++)
            if (sim.Buildings.Slots.IsIndexAlive(i) && sim.Buildings.Player[i] == player) return i;
        throw new InvalidOperationException("no building");
    }

    private static void Run(Simulation sim, int ticks, params Command[] first)
    {
        for (int i = 0; i < ticks; i++) sim.Step(i == 0 ? first : None);
    }

    [Fact]
    public void Place_PutsEveryRuinDownAsNeutral_InTheSetupLog_BeforeTickZero()
    {
        var (sim, ruins, _, meta) = Scene();
        Assert.True(ruins.Length >= 2);
        Assert.Equal(meta.Features.Count, ruins.Length);
        Assert.Equal(0, sim.Tick);
        for (int k = 0; k < ruins.Length; k++)
        {
            Assert.False(ruins[k].IsNone, $"feature {k} failed to place");
            Assert.Equal(Neutral, sim.Buildings.Player[ruins[k].Index]);
            Assert.Equal(meta.Features[k].Cell, sim.Buildings.Cell[ruins[k].Index]);
            Assert.Equal(meta.Features[k].Type, sim.Setup.Rules.Buildings[sim.Buildings.TypeIndex[ruins[k].Index]].Id);
        }
        Assert.Equal(ruins.Length, sim.SetupLog.Count(e => e.Kind == ActorKind.Building && e.Player == Neutral));
        Assert.Equal(Enumerable.Repeat(GarrisonState.NoOwner, ruins.Length), ruins.Select(r => sim.Garrison.HomeOwner[r.Index]));
    }

    [Fact]
    public void Place_UnknownTypeGivesNone()
    {
        var (rules, map, _) = Generated();
        var sim = new Simulation(SetupFor(rules, map));
        var meta = MapMeta.DefaultFor(map) with { Features = new[] { new MapFeature("no_such_ruin", new CellCoord(3, 3)) } };
        Assert.True(MapFeatures.Place(sim, meta, Neutral)[0].IsNone);
    }

    [Fact]
    public void Entering_FlipsTheOwner_AndAnEnemyCannotJoin()
    {
        var (sim, ruins, type, _) = Scene();
        EntityId ruin = ruins[0];
        EntityId a = SpawnBeside(sim, 0, type, ruin);
        EntityId foe = SpawnBeside(sim, 1, type, ruin);
        Run(sim, 1, Enter(sim, 0, a, ruin));
        Assert.Equal(ActorLayer.Garrisoned, sim.LayerOf(ActorKind.Infantry, a.Index));
        Assert.Equal((byte)0, sim.Buildings.Player[ruin.Index]);
        Assert.Equal(Neutral, sim.Garrison.HomeOwner[ruin.Index]);
        Run(sim, 1, Enter(sim, 1, foe, ruin));   // now an ordinary enemy building: not enterable
        Assert.Equal(1, sim.OccupantsIn(ruin));
        Assert.Equal(ActorLayer.Surface, sim.LayerOf(ActorKind.Infantry, foe.Index));
        Assert.Equal(Relation.Enemy, sim.RelationOf(1, sim.Buildings.Player[ruin.Index]));
    }

    [Fact]
    public void Unloading_RevertsToNeutral()
    {
        var (sim, ruins, type, _) = Scene();
        EntityId ruin = ruins[0];
        EntityId a = SpawnBeside(sim, 0, type, ruin);
        EntityId b = SpawnBeside(sim, 1, type, ruin);
        Run(sim, 1, Enter(sim, 0, a, ruin));
        Assert.Equal((byte)0, sim.Buildings.Player[ruin.Index]);
        Run(sim, 1, new Command(sim.Tick, 0, _seq++, CommandKind.Unload, ruin, default, default, (int)ActorKind.Building, 0));
        Assert.Equal(0, sim.OccupantsIn(ruin));
        Assert.Equal(Neutral, sim.Buildings.Player[ruin.Index]);
        Assert.Equal(GarrisonState.NoOwner, sim.Garrison.HomeOwner[ruin.Index]);
        // and the other side may take it
        Run(sim, 1, Enter(sim, 1, b, ruin));
        Assert.Equal((byte)1, sim.Buildings.Player[ruin.Index]);
    }

    [Fact]
    public void AllOccupantsDying_RevertsToNeutral()
    {
        var (sim, ruins, type, _) = Scene();
        EntityId ruin = ruins[0];
        EntityId a = SpawnBeside(sim, 0, type, ruin);
        Run(sim, 1, Enter(sim, 0, a, ruin));
        Run(sim, 3, new Command(sim.Tick, 0, _seq++, CommandKind.Debug, a, default, default, (int)DebugOp.Kill, (int)ActorKind.Infantry));
        Assert.Equal(0, sim.OccupantsIn(ruin));
        Assert.Equal(Neutral, sim.Buildings.Player[ruin.Index]);
        Assert.Equal(GarrisonState.NoOwner, sim.Garrison.HomeOwner[ruin.Index]);
    }

    [Fact]
    public void FlippedRuin_DoesNotCountForDefeat_OrTier()
    {
        var (sim, ruins, type, _) = Scene();
        EntityId conyard = sim.Buildings.Slots.IdOf(FirstBuildingOf(sim, 0));
        EntityId a = SpawnBeside(sim, 0, type, ruins[0]);
        int tierBefore = sim.TierOf(0);
        Run(sim, 2, Enter(sim, 0, a, ruins[0]));
        Assert.Equal((byte)0, sim.Buildings.Player[ruins[0].Index]);
        Assert.Equal(tierBefore, sim.TierOf(0));
        Assert.False(sim.Players.Defeated[0]);
        Run(sim, 2, new Command(sim.Tick, 0, _seq++, CommandKind.Debug, conyard, default, default, (int)DebugOp.Kill, (int)ActorKind.Building));
        Assert.True(sim.Buildings.Slots.IsAlive(ruins[0]));
        Assert.True(sim.Players.Defeated[0], "the held ruin must not keep player 0 alive");
        Assert.False(sim.Players.Defeated[1]);
        Assert.False(sim.Players.Defeated[Neutral]);
    }

    [Fact]
    public void Replay_ReproducesPlacementFlipAndRevert()
    {
        const int ticks = 120;
        var (rules, map, meta) = Generated();
        SimSetup setup = SetupFor(rules, map);
        int type = Garrisoner(rules.Sim);

        Simulation Build()
        {
            var s = new Simulation(setup);
            MapFeatures.Place(s, meta, Neutral);
            PlaceBase(s, 0); PlaceBase(s, 1);
            return s;
        }

        Simulation sim = Build();
        EntityId ruin = sim.Buildings.Slots.IdOf(1);
        EntityId a = SpawnBeside(sim, 0, type, ruin);
        var script = new List<Command>
        {
            new(0, 0, 0, CommandKind.Enter, a, ruin, default, (int)ActorKind.Infantry | ((int)ActorKind.Building << 8), 0),
            new(60, 0, 1, CommandKind.Unload, ruin, default, default, (int)ActorKind.Building, 0),
        };
        var ms = new MemoryStream();
        var rec = new ReplayWriter(ms, sim, "test-build", RulesFiles) { RecordSubHashes = true };
        foreach (Command c in script) rec.RecordCommand(c);
        bool sawFlip = false;
        for (int i = 0; i < ticks; i++)
        {
            sim.Step(script);
            sawFlip |= sim.Buildings.Player[ruin.Index] == 0;
            if (sim.Tick % SimConstants.HashIntervalTicks == 0) rec.RecordHash(sim);
        }
        rec.Finish();
        Assert.True(sawFlip);
        Assert.Equal(Neutral, sim.Buildings.Player[ruin.Index]);

        ReplayResult result = ReplayPlayer.Run(new MemoryStream(ms.ToArray()), setup);
        Assert.Equal(-1, result.FirstMismatchTick);
        Assert.Equal(sim.ComputeStateHash(), result.FinalHash);
    }
}
