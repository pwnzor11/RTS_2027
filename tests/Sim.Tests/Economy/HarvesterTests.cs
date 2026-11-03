// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Economy;
using Rts.Sim.Missions;
using Rts.Sim.Rules;
using static Rts.Sim.Tests.Combat.CombatFixture;
using static Rts.Sim.Tests.Economy.EconomyFixture;

namespace Rts.Sim.Tests.Economy;

public class HarvesterTests
{
    [Fact]
    public void PlayersStartWithStartingCash()
    {
        var sim = NewSim(PaintMap(OverlayKind.None, 0));
        Assert.Equal(Loaded.Sim.Economy.StartingCash, sim.Players.Cash[0]);
        Assert.Equal(Loaded.Sim.Economy.StartingCash, sim.Players.Cash[1]);
    }

    [Fact]
    public void FullLoop_LoadsReturnsUnloadsAndCreditsArrive()
    {
        var (sim, h) = Basic();
        int before = TotalDensity(sim);
        Assert.Equal(108, before);
        int i = h.Index;

        // auto-started: Harvest, carrying nothing
        sim.Step(None);
        sim.Step(None);
        Assert.Equal(MissionKind.Harvest, MissionOf(sim, h));

        int sawFull = 0, sawReturn = 0, sawUnload = 0;
        RunUntil(sim, () =>
        {
            if (sim.Economy.Cargo[i] == 20) sawFull++;
            if (MissionOf(sim, h) == MissionKind.Return) sawReturn++;
            if (sim.Economy.DockState[i] == (byte)DockStep.Unload && MissionOf(sim, h) == MissionKind.Return) sawUnload++;
            return sim.Players.Stored[0] == 20 * 25;
        });
        Assert.True(sawFull > 0 && sawReturn > 0 && sawUnload > 0);
        Assert.Equal(0, sim.Economy.Cargo[i]);
        Assert.Equal(before - 20, TotalDensity(sim));          // density dropped by exactly what was loaded
        Assert.Equal(sim.Players.Stored[0], sim.Funds(0) - sim.Players.Cash[0]);

        // and it goes back to work
        RunUntil(sim, () => MissionOf(sim, h) == MissionKind.Harvest && sim.Economy.HarvestState[i] == (byte)HarvestStep.Load);
    }

    [Fact]
    public void Unloading_IsATrickle_OneUnitPerUnloadInterval()
    {
        var (sim, h) = Basic();
        int unload = sim.Setup.Rules.Economy.UnloadTicksPerUnit;
        RunUntil(sim, () => sim.Economy.DockState[h.Index] == (byte)DockStep.Unload && MissionOf(sim, h) == MissionKind.Return);
        int storedAtStart = sim.Players.Stored[0];
        int t0 = sim.Tick;
        RunUntil(sim, () => sim.Players.Stored[0] > storedAtStart);
        Assert.InRange(sim.Tick - t0, 1, unload + 1);
        Assert.Equal(25, sim.Players.Stored[0] - storedAtStart);
        // docked harvester faces the building (north-west of the dock cell: building centre is above)
        Assert.Equal(Rts.Sim.Numerics.Facing.FromDelta(0, -1) , sim.Units.Facing[h.Index]);
    }

    [Fact]
    public void Pitchblende_PaysMoreThanAshfall()
    {
        var (ash, _) = Basic(OverlayKind.Ashfall, silo: true);
        var (pit, _) = Basic(OverlayKind.Pitchblende, silo: true);
        RunUntil(ash, () => ash.Players.Stored[0] >= 20 * 25);
        RunUntil(pit, () => pit.Players.Stored[0] >= 20 * 60);
        RunUntil(ash, () => ash.Economy.Cargo[1] == 0 && ash.Players.Stored[0] > 0, 50);
        Assert.Equal(500, ash.Players.Stored[0]);
        Assert.Equal(1200, pit.Players.Stored[0]);
    }

    [Fact]
    public void StorageCap_OverflowIsLost()
    {
        var (sim, h) = Basic(OverlayKind.Pitchblende);     // one refinery: cap 1000, a full load is worth 1200
        Assert.Equal(1000, sim.StorageCap(0));
        RunUntil(sim, () => sim.Players.Stored[0] > 0 && sim.Economy.Cargo[h.Index] == 0 && MissionOf(sim, h) == MissionKind.Harvest);
        Assert.Equal(1000, sim.Players.Stored[0]);
        Assert.Equal(1000, sim.HudFor(0).StorageCap);
    }

    [Fact]
    public void LosingASilo_LosesTheStoredCreditsAboveTheNewCap()
    {
        var sim = NewSim(PaintMap(OverlayKind.None, 0));
        sim.PlaceBuilding(0, Bld("test_refinery"), Refinery);
        EntityId silo = sim.PlaceBuilding(0, Bld("test_silo"), new CellCoord(10, 2));
        sim.Step(None);
        sim.Players.Stored[0] = 2000;
        sim.Step(None);
        Assert.Equal(2000, sim.Players.Stored[0]);
        Assert.Equal(2500, sim.StorageCap(0));
        sim.Step(new[] { Debug(sim, 0, DebugOp.Kill, silo, (int)ActorKind.Building) });
        sim.Step(None);
        Assert.Equal(1000, sim.StorageCap(0));
        Assert.Equal(1000, sim.Players.Stored[0]);
    }

    [Fact]
    public void TwoHarvesters_QueueAtOneDock()
    {
        var (sim, a) = Basic(silo: true);
        EntityId b = sim.SpawnVehicle(0, Harvester, new CellCoord(8, 10));
        int maxDocked = 0, sawWaiting = 0;
        RunUntil(sim, () =>
        {
            int docked = 0;
            foreach (EntityId id in new[] { a, b })
            {
                bool unloading = MissionOf(sim, id) == MissionKind.Return && sim.Economy.DockState[id.Index] == (byte)DockStep.Unload;
                if (unloading) { docked++; Assert.Equal(Dock, sim.Units.Position[id.Index].ToCell()); }
                if (MissionOf(sim, id) == MissionKind.Return && sim.Economy.DockState[id.Index] == (byte)DockStep.Wait) sawWaiting++;
            }
            maxDocked = Math.Max(maxDocked, docked);
            Assert.True(docked <= 1, "two harvesters unloading at once");
            return sim.Players.Stored[0] >= 2 * 20 * 25;
        }, 4000);
        Assert.Equal(1, maxDocked);
        Assert.True(sawWaiting > 0, "the second harvester should have queued");
        Assert.Equal(1000, sim.Players.Stored[0]);
    }

    [Fact]
    public void RefineryDestroyedMidDock_HarvesterUsesAnotherRefinery()
    {
        var (sim, h) = Basic(silo: true);
        EntityId second = sim.PlaceBuilding(0, Bld("test_refinery"), new CellCoord(26, 2));
        EntityId first = sim.Map.BuildingAt(Refinery);
        RunUntil(sim, () => sim.Economy.DockState[h.Index] == (byte)DockStep.Unload && MissionOf(sim, h) == MissionKind.Return
                            && sim.Economy.Cargo[h.Index] < 20);
        // the first (nearer at spawn) refinery was picked; now destroy it
        Assert.Equal(first, sim.Economy.DockTarget[h.Index]);
        sim.Step(new[] { Debug(sim, 0, DebugOp.Kill, first, (int)ActorKind.Building) });
        sim.Step(None);
        Assert.False(sim.Buildings.Slots.IsAlive(first));
        Assert.True(sim.Economy.DockOccupant[first.Index].IsNone);
        RunUntil(sim, () => sim.Economy.DockTarget[h.Index] == second, 50);       // re-targets the other refinery
        RunUntil(sim, () => sim.Players.Stored[0] == 20 * 25 && sim.Economy.Cargo[h.Index] == 0, 3000);
        Assert.True(sim.Buildings.Slots.IsAlive(second));
    }

    [Fact]
    public void RefineryDestroyed_NoOtherRefinery_HarvesterGuards()
    {
        var (sim, h) = Basic();
        EntityId refinery = sim.Map.BuildingAt(Refinery);
        RunUntil(sim, () => MissionOf(sim, h) == MissionKind.Return);
        sim.Step(new[] { Debug(sim, 0, DebugOp.Kill, refinery, (int)ActorKind.Building) });
        RunUntil(sim, () => MissionOf(sim, h) == MissionKind.Guard, 300);
        Assert.Equal(20, sim.Economy.Cargo[h.Index]);          // keeps its cargo
    }

    [Fact]
    public void HarvestCommand_OverridesAndPicksTheNamedField()
    {
        // two fields; the harvester would pick the near one, the command sends it to the far one
        var map = PaintMap(OverlayKind.Ashfall, 12, (o, d, w) => { o[26 * w + 26] = OverlayKind.Ashfall; d[26 * w + 26] = 12; });
        var sim = NewSim(map);
        sim.PlaceBuilding(0, Bld("test_refinery"), Refinery);
        EntityId h = sim.SpawnVehicle(0, Harvester, new CellCoord(8, 8));
        sim.Step(new[] { new Command(sim.Tick, 0, 1, CommandKind.Harvest, h, default, new CellCoord(26, 26), 0) });
        RunUntil(sim, () => sim.Economy.HarvestState[h.Index] == (byte)HarvestStep.Load, 2000);
        Assert.Equal(new CellCoord(26, 26), sim.Units.Position[h.Index].ToCell());
        Assert.Equal(12, TotalDensity(sim) - 108);      // the near field was not touched
    }

    [Fact]
    public void ReturnCommand_SendsHarvesterToUnload_ThenBackToWork()
    {
        var (sim, h) = Basic();
        RunUntil(sim, () => sim.Economy.Cargo[h.Index] >= 5);
        sim.Step(new[] { new Command(sim.Tick, 0, 2, CommandKind.Return, h, default, default, 0) });
        RunUntil(sim, () => sim.Players.Stored[0] >= 5 * 25 && MissionOf(sim, h) == MissionKind.Harvest && sim.Economy.Cargo[h.Index] == 0, 2500);
        Assert.True(sim.Players.Stored[0] >= 125);
    }

    [Fact]
    public void ReusedSlot_DoesNotInheritCargo()
    {
        var (sim, h) = Basic();
        RunUntil(sim, () => sim.Economy.Cargo[h.Index] >= 3);
        sim.Step(new[] { Debug(sim, 0, DebugOp.Kill, h, (int)ActorKind.Vehicle) });
        sim.Step(None);
        Assert.False(sim.Units.Slots.IsAlive(h));
        Assert.Equal(0, sim.Economy.Cargo[h.Index]);
        EntityId fresh = Boundary.LateSpawn.Vehicle(sim, 0, Harvester, new CellCoord(9, 9));
        Assert.Equal(h.Index, fresh.Index);
        Assert.Equal(0, sim.Economy.Cargo[fresh.Index]);
    }
}
