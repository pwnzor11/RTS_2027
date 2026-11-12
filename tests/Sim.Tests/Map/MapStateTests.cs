// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.Map;

public class MapStateTests
{
    private static readonly TerrainTable Table = RulesLoader.Load(TestData.RulesDir).Sim.Terrain;
    private static readonly CellCoord C = new(3, 4);
    private static EntityId Id(int i) => new((ushort)i, 1);

    private static MapState Make() => new(MapData.CreateFlat(8, 8));

    [Fact]
    public void InfantrySlotsFillCentreThenCornersInOrder()
    {
        var m = Make();
        for (int expected = 0; expected < 5; expected++)
        {
            Assert.Equal(expected, m.FreeInfantrySlot(C));
            m.SetInfantry(C, expected, Id(expected + 1));
        }
        Assert.Equal(-1, m.FreeInfantrySlot(C));
        m.SetInfantry(C, 2, EntityId.None);
        Assert.Equal(2, m.FreeInfantrySlot(C));
        Assert.Equal(Id(4), m.InfantryAt(C, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => m.SetInfantry(C, 5, Id(9)));
    }

    [Fact]
    public void VehicleAndBuildingOccupancy()
    {
        var m = Make();
        Assert.True(m.VehicleAt(C).IsNone);
        m.SetVehicle(C, Id(7));
        m.SetBuilding(C, Id(8));
        Assert.Equal(Id(7), m.VehicleAt(C));
        Assert.Equal(Id(8), m.BuildingAt(C));
        m.SetVehicle(C, EntityId.None);
        Assert.True(m.VehicleAt(C).IsNone);
        Assert.True(m.VehicleAt(new CellCoord(0, 0)).IsNone);
    }

    private static MapState Terrain()
    {
        var d = MapData.CreateFlat(4, 1);
        var land = new[] { LandType.Clear, LandType.Rough, LandType.Cliff, LandType.Water };
        return new MapState(new MapData("t", "rust_belt", 4, 1, new ushort[4], land, new byte[4],
            new OverlayKind[4], new byte[4], d.StartPositions));
    }

    [Fact]
    public void SpeedPercentFollowsTable()
    {
        var m = Terrain();
        Assert.Equal(100, m.SpeedPercent(new CellCoord(0, 0), Locomotion.Tracked, Table));
        Assert.Equal(75, m.SpeedPercent(new CellCoord(1, 0), Locomotion.Tracked, Table));
        Assert.Equal(0, m.SpeedPercent(new CellCoord(2, 0), Locomotion.Tracked, Table));
        Assert.Equal(0, m.SpeedPercent(new CellCoord(3, 0), Locomotion.Foot, Table));
        Assert.Equal(100, m.SpeedPercent(new CellCoord(3, 0), Locomotion.Hover, Table));
        Assert.Equal(0, m.SpeedPercent(new CellCoord(-1, 0), Locomotion.Foot, Table));
        Assert.Equal(0, m.SpeedPercent(new CellCoord(4, 0), Locomotion.Air, Table));
        Assert.True(m.IsPassableTerrain(new CellCoord(1, 0), Locomotion.Foot, Table));
        Assert.False(m.IsPassableTerrain(new CellCoord(2, 0), Locomotion.Foot, Table));
    }

    [Fact]
    public void IsBuildableChecksTerrainOverlayAndOccupants()
    {
        var m = Terrain();
        Assert.True(m.IsBuildable(new CellCoord(0, 0), Table));
        Assert.False(m.IsBuildable(new CellCoord(1, 0), Table)); // rough
        Assert.False(m.IsBuildable(new CellCoord(9, 0), Table)); // outside

        var c = new CellCoord(0, 0);
        m.SetOverlay(c, OverlayKind.Ashfall, 5);
        Assert.False(m.IsBuildable(c, Table));
        m.SetOverlay(c, OverlayKind.None, 0);
        Assert.True(m.IsBuildable(c, Table));
        m.SetVehicle(c, Id(1));
        Assert.False(m.IsBuildable(c, Table));
        m.SetVehicle(c, EntityId.None);
        m.SetInfantry(c, 4, Id(2));
        Assert.False(m.IsBuildable(c, Table));
        m.SetInfantry(c, 4, EntityId.None);
        m.SetBuilding(c, Id(3));
        Assert.False(m.IsBuildable(c, Table));
    }

    [Fact]
    public void VersionChangesOnlyForBuildingsAndWalls()
    {
        var m = Make();
        int v = m.Version;
        m.SetVehicle(C, Id(1));
        m.SetInfantry(C, 0, Id(2));
        Assert.Equal(v, m.Version);
        m.SetOverlay(C, OverlayKind.Ashfall, 4);    // S19 M13-12: a field appearing changes infantry path costs (ash_path_penalty)
        Assert.Equal(v + 1, m.Version);
        m.SetOverlay(C, OverlayKind.Pitchblende, 9);   // still a field: no change
        Assert.Equal(v + 1, m.Version);
        v++;

        m.SetBuilding(C, Id(3));
        Assert.Equal(v + 1, m.Version);
        m.SetBuilding(C, Id(3)); // no change
        Assert.Equal(v + 1, m.Version);
        m.SetBuilding(C, EntityId.None);
        Assert.Equal(v + 2, m.Version);

        m.SetOverlay(C, OverlayKind.Wall, 0);
        Assert.Equal(v + 3, m.Version);
        Assert.Equal(0, m.SpeedPercent(C, Locomotion.Foot, Table));
        m.SetOverlay(C, OverlayKind.None, 0);
        Assert.Equal(v + 4, m.Version);
    }

    [Fact]
    public void WriteReadRoundTripCoversEveryLayer()
    {
        var a = Make();
        a.SetLand(new CellCoord(1, 1), LandType.Rock);
        a.SetOverlay(new CellCoord(2, 2), OverlayKind.Pitchblende, 11);
        a.SetVehicle(new CellCoord(3, 3), Id(5));
        a.SetInfantry(new CellCoord(4, 4), 3, Id(6));
        a.SetBuilding(new CellCoord(5, 5), Id(7));

        byte[] bytes = Save(a);
        var b = Make();
        using (var r = new BinaryReader(new MemoryStream(bytes))) b.Read(r);

        Assert.Equal(a.Version, b.Version);
        Assert.Equal(LandType.Rock, b.LandAt(new CellCoord(1, 1)));
        Assert.Equal(OverlayKind.Pitchblende, b.OverlayAt(new CellCoord(2, 2)));
        Assert.Equal(11, b.DensityAt(new CellCoord(2, 2)));
        Assert.Equal(Id(5), b.VehicleAt(new CellCoord(3, 3)));
        Assert.Equal(Id(6), b.InfantryAt(new CellCoord(4, 4), 3));
        Assert.Equal(Id(7), b.BuildingAt(new CellCoord(5, 5)));
        Assert.Equal(bytes, Save(b));
    }

    private static byte[] Save(MapState s)
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true)) s.Write(w);
        return ms.ToArray();
    }
}
