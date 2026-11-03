// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Determinism;
using Rts.Sim.World;

namespace Rts.Sim.Tests.Buildings;

public class PlacementTests
{
    private const int Barracks = 0, Hq = 1, Turret = 2;

    private static Simulation NewSim(int w, int h, Action<LandType[], OverlayKind[]>? edit = null, int players = 2)
    {
        int n = w * h;
        var land = new LandType[n];
        var overlay = new OverlayKind[n];
        edit?.Invoke(land, overlay);
        var map = new MapData("t", "rust_belt", w, h, new ushort[n], land, new byte[n], overlay, new byte[n],
            new[] { new CellCoord(0, 0) });
        return new Simulation(new SimSetup(1, map, players,
            ScriptedGame.RulesWith(new[] { ScriptedGame.Vehicle("t", 20, 10) }, new[] { ScriptedGame.Infantry("i", 9, 10) }), 1) { AllowDebug = true });
    }

    private static Command Cmd(int tick, CommandKind kind, EntityId subj, CellCoord cell, int arg, ushort seq = 0, byte player = 0) =>
        new(tick, player, seq, kind, subj, default, cell, arg);

    [Fact]
    public void PlaceOnClearGround_Succeeds_AndFillsFootprint()
    {
        var sim = NewSim(16, 16);
        Assert.Equal(PlacementResult.Ok, sim.CanPlace(0, Hq, new CellCoord(4, 4)));
        int v0 = sim.Map.Version;
        var id = sim.PlaceBuilding(0, Hq, new CellCoord(4, 4));
        Assert.False(id.IsNone);
        Assert.True(sim.TryResolve(new ActorRef(ActorKind.Building, id)));
        Assert.True(sim.Map.Version > v0);
        for (int y = 4; y < 7; y++)
            for (int x = 4; x < 7; x++) Assert.Equal(id, sim.Map.BuildingAt(new CellCoord(x, y)));
        Assert.True(sim.Map.BuildingAt(new CellCoord(7, 4)).IsNone);
        Assert.Equal(500, sim.Buildings.Health[id.Index]);
        var snap = sim.Snapshot();
        var view = Assert.Single(snap.Buildings);
        Assert.Equal(new BuildingView(id, 0, Hq, new CellCoord(4, 4), 3, 3, 500, false), view);
        Assert.Equal(-1, snap.Viewer);
        Assert.Empty(snap.Vision);
    }

    [Fact]
    public void OutOfMap_IsRejected()
    {
        var sim = NewSim(16, 16);
        Assert.Equal(PlacementResult.OutOfMap, sim.CanPlace(0, Hq, new CellCoord(14, 4)));
        Assert.Equal(PlacementResult.OutOfMap, sim.CanPlace(0, Turret, new CellCoord(-1, 0)));
        Assert.Equal(PlacementResult.OutOfMap, sim.CanPlace(0, Turret, new CellCoord(0, 16)));
        Assert.Equal(PlacementResult.Ok, sim.CanPlace(0, Hq, new CellCoord(13, 13)));
        Assert.True(sim.PlaceBuilding(0, Hq, new CellCoord(14, 4)).IsNone);
    }

    [Theory]
    [InlineData(LandType.Rock, OverlayKind.None)]
    [InlineData(LandType.Water, OverlayKind.None)]
    [InlineData(LandType.Clear, OverlayKind.Ashfall)]
    public void TerrainOrOverlay_Blocks(LandType land, OverlayKind overlay)
    {
        var sim = NewSim(16, 16, (l, o) => { l[6 * 16 + 5] = land; o[6 * 16 + 5] = overlay; });
        Assert.Equal(PlacementResult.Blocked, sim.CanPlace(0, Barracks, new CellCoord(4, 5)));
        Assert.Equal(PlacementResult.Ok, sim.CanPlace(0, Barracks, new CellCoord(1, 1)));
    }

    [Fact]
    public void OverlappingBuilding_IsBlocked()
    {
        var sim = NewSim(16, 16);
        Assert.False(sim.PlaceBuilding(0, Hq, new CellCoord(4, 4)).IsNone);
        Assert.Equal(PlacementResult.Blocked, sim.CanPlace(0, Turret, new CellCoord(6, 6)));
        Assert.Equal(PlacementResult.Blocked, sim.CanPlace(1, Barracks, new CellCoord(6, 5)));
        Assert.Equal(PlacementResult.Ok, sim.CanPlace(1, Barracks, new CellCoord(7, 4)));
    }

    [Fact]
    public void UnitOnFootprint_IsBlocked()
    {
        var sim = NewSim(16, 16);
        sim.SpawnVehicle(0, 0, new CellCoord(5, 5));
        sim.SpawnInfantry(0, 0, new CellCoord(10, 10), 3);
        Assert.Equal(PlacementResult.Blocked, sim.CanPlace(0, Hq, new CellCoord(4, 4)));
        Assert.Equal(PlacementResult.Blocked, sim.CanPlace(0, Barracks, new CellCoord(9, 9)));
        Assert.Equal(PlacementResult.Ok, sim.CanPlace(0, Barracks, new CellCoord(6, 6)));
    }

    // Corridor: rock everywhere except rows 1..5 (5 tall) of a 20x7 map.
    private static Simulation Corridor() => NewSim(20, 7, (l, o) =>
    {
        for (int y = 0; y < 7; y++)
            for (int x = 0; x < 20; x++)
                if (y < 1 || y > 5) l[y * 20 + x] = LandType.Rock;
    });

    [Fact]
    public void WallAcrossCorridor_IsRejected_WhileOneCellGapIsAccepted()
    {
        var sim = Corridor();
        Assert.False(sim.PlaceBuilding(0, Hq, new CellCoord(8, 1)).IsNone);               // rows 1..3
        Assert.Equal(PlacementResult.Ok, sim.CanPlace(0, Turret, new CellCoord(8, 4)));   // leaves row 5
        Assert.False(sim.PlaceBuilding(0, Turret, new CellCoord(8, 4)).IsNone);
        Assert.Equal(PlacementResult.SealsRegion, sim.CanPlace(0, Turret, new CellCoord(8, 5)));
        Assert.True(sim.PlaceBuilding(0, Turret, new CellCoord(8, 5)).IsNone);
        // A barracks covering rows 4..5 next to the wall would also close the last gap.
        Assert.Equal(PlacementResult.SealsRegion, sim.CanPlace(0, Barracks, new CellCoord(9, 4)));
    }

    [Fact]
    public void EnclosingAPocket_IsRejected()
    {
        var sim = NewSim(16, 16);
        Assert.False(sim.PlaceBuilding(0, Turret, new CellCoord(4, 5)).IsNone);
        Assert.False(sim.PlaceBuilding(0, Turret, new CellCoord(6, 5)).IsNone);
        Assert.False(sim.PlaceBuilding(0, Turret, new CellCoord(5, 4)).IsNone);
        Assert.Equal(PlacementResult.SealsRegion, sim.CanPlace(0, Turret, new CellCoord(5, 6)));
    }

    [Fact]
    public void FillingWholeSmallRegion_IsAllowed()
    {
        // A one-cell nook that is completely filled disappears rather than splitting anything.
        var sim = NewSim(8, 8, (l, o) =>
        {
            for (int i = 0; i < 64; i++) l[i] = LandType.Rock;
            l[3 * 8 + 3] = LandType.Clear;
        });
        Assert.Equal(PlacementResult.Ok, sim.CanPlace(0, Turret, new CellCoord(3, 3)));
    }

    [Fact]
    public void WorstCase128_CanPlace_IsFast()
    {
        var sim = NewSim(128, 128);
        sim.CanPlace(0, Hq, new CellCoord(10, 10)); // warm up
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 10; i++) sim.CanPlace(0, Hq, new CellCoord(10 + i, 10));
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds / 10.0 < 5, $"CanPlace took {sw.ElapsedMilliseconds / 10.0} ms");
    }

    [Fact]
    public void PlaceCommand_PlacesThroughStep_AndInvalidIsIgnored()
    {
        var sim = NewSim(16, 16);
        var cmds = new List<Command>
        {
            Cmd(0, CommandKind.Place, default, new CellCoord(4, 4), Hq, 0),
            Cmd(0, CommandKind.Place, default, new CellCoord(5, 5), Turret, 1),   // overlaps: ignored
            Cmd(0, CommandKind.Place, default, new CellCoord(1, 1), 99, 2),        // bad type: ignored
            Cmd(0, CommandKind.Place, default, new CellCoord(12, 12), Turret, 3, player: 7), // bad player: ignored
        };
        sim.Step(cmds);
        Assert.Equal(1, sim.Buildings.Slots.Count);
        Assert.False(sim.Map.BuildingAt(new CellCoord(5, 5)).IsNone);
    }

    [Fact]
    public void Sell_FreesCells_BumpsVersion_AndChecksOwner()
    {
        var sim = NewSim(16, 16);
        var id = sim.PlaceBuilding(0, Barracks, new CellCoord(3, 3));
        sim.PlaceBuilding(0, Hq, new CellCoord(10, 10));   // keeps player 0 undefeated (M2-08 win rule) after the sale
        int v = sim.Map.Version;
        // Wrong owner: ignored.
        sim.Step(new[] { Cmd(0, CommandKind.Sell, id, default, (int)ActorKind.Building, player: 1) });
        Assert.True(sim.TryResolve(new ActorRef(ActorKind.Building, id)));
        Assert.Equal(v, sim.Map.Version);
        sim.Step(new[] { Cmd(1, CommandKind.Sell, id, default, (int)ActorKind.Building, player: 0) });
        Assert.False(sim.TryResolve(new ActorRef(ActorKind.Building, id)));
        Assert.True(sim.Map.Version > v);
        for (int y = 3; y < 5; y++)
            for (int x = 3; x < 5; x++) Assert.True(sim.Map.BuildingAt(new CellCoord(x, y)).IsNone);
        Assert.Equal(PlacementResult.Ok, sim.CanPlace(0, Barracks, new CellCoord(3, 3)));
        Assert.Single(sim.Snapshot().Buildings);
        // Slot reuse yields a new generation; the old id stays dead.
        var again = Boundary.LateSpawn.Building(sim, 0, Barracks, new CellCoord(3, 3));
        Assert.Equal(id.Index, again.Index);
        Assert.NotEqual(id.Generation, again.Generation);
        Assert.False(sim.TryResolve(new ActorRef(ActorKind.Building, id)));
    }

    [Fact]
    public void Cleanup_RemovesDeadBuildings_AndClearsCells()
    {
        var sim = NewSim(16, 16);
        var id = sim.PlaceBuilding(0, Turret, new CellCoord(2, 2));
        sim.Buildings.Health[id.Index] = 0;
        sim.Step(Array.Empty<Command>());
        Assert.False(sim.TryResolve(new ActorRef(ActorKind.Building, id)));
        Assert.True(sim.Map.BuildingAt(new CellCoord(2, 2)).IsNone);
    }

    [Fact]
    public void SaveRoundTrip_PreservesBuildings()
    {
        var sim = NewSim(16, 16);
        var a = sim.PlaceBuilding(0, Hq, new CellCoord(2, 2));
        var b = sim.PlaceBuilding(1, Turret, new CellCoord(10, 10));
        sim.Buildings.Health[b.Index] = 42;
        var ms = new MemoryStream(); sim.Save(ms); ms.Position = 0;
        var loaded = Simulation.Load(ms, sim.Setup);
        Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
        Assert.Equal(42, loaded.Buildings.Health[b.Index]);
        Assert.Equal(new CellCoord(2, 2), loaded.Buildings.Cell[a.Index]);
        Assert.Equal(a, loaded.Map.BuildingAt(new CellCoord(3, 3)));
        Assert.True(loaded.TryResolve(new ActorRef(ActorKind.Building, b)));
        Assert.Equal(sim.Snapshot().Buildings, loaded.Snapshot().Buildings);
        // Both continue identically, including a Sell.
        var cmds = new[] { Cmd(0, CommandKind.Sell, a, default, (int)ActorKind.Building) };
        sim.Step(cmds); loaded.Step(cmds);
        Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
    }

    [Fact]
    public void StateParts_IncludeBuildingsAfterInfantry()
    {
        // The full canonical order is pinned in SaveTests.StateParts_AreInCanonicalOrder; here only buildings' place.
        var names = NewSim(8, 8).StateParts.Select(p => p.Name).ToList();
        Assert.Equal(names.IndexOf("infantry") + 1, names.IndexOf("buildings"));
    }
}
