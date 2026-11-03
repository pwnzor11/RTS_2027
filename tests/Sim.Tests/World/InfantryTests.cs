using Rts.Sim.Core;
using Rts.Sim.Tests.Determinism;
using Rts.Sim.World;

namespace Rts.Sim.Tests.World;

public class InfantryTests
{
    private static Simulation Make() => ScriptedGame.NewSim(ScriptedGame.Setup());

    [Theory]
    [InlineData(0, 0, 0)] [InlineData(1, -64, -64)] [InlineData(2, 64, -64)] [InlineData(3, -64, 64)] [InlineData(4, 64, 64)]
    public void Spawn_PlacesAtSubSlotOffset(int slot, int ox, int oy)
    {
        var sim = Make();
        var id = sim.SpawnInfantry(1, 0, new CellCoord(40, 40), slot);
        var c = LeptonCoord.CenterOf(new CellCoord(40, 40));
        Assert.Equal(new LeptonCoord(c.X + ox, c.Y + oy), sim.Infantry.Position[id.Index]);
        Assert.Equal(slot, sim.Infantry.SubSlot[id.Index]);
        Assert.Equal(1, sim.Infantry.Player[id.Index]);
        Assert.Equal(50, sim.Infantry.Health[id.Index]);
    }

    [Fact]
    public void Spawn_RejectsBadArguments()
    {
        var sim = Make();
        Assert.Throws<ArgumentOutOfRangeException>(() => sim.SpawnInfantry(0, 0, new CellCoord(1, 1), 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => sim.SpawnInfantry(0, 9, new CellCoord(1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => sim.SpawnInfantry(0, 0, new CellCoord(99, 1)));
    }

    [Fact]
    public void Snapshot_ListsVehiclesThenInfantry()
    {
        var sim = Make();
        var units = sim.Snapshot().Units;
        Assert.Equal(30, units.Count);
        Assert.All(units.Take(20), u => Assert.Equal(UnitKind.Vehicle, u.Kind));
        Assert.All(units.Skip(20), u => Assert.Equal(UnitKind.Infantry, u.Kind));
    }

    [Fact]
    public void Infantry_CapacityIs2048()
    {
        Assert.Equal(2048, new InfantryStore().Capacity);
    }
}
