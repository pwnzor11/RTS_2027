using Rts.Sim.Core;
using Rts.Sim.World;

namespace Rts.Sim.Tests.World;

public class SlotArrayTests
{
    [Fact]
    public void Allocate_ReusesLowestFreeIndex()
    {
        var s = new SlotArray(128);
        var ids = Enumerable.Range(0, 5).Select(_ => s.Allocate()).ToArray();
        Assert.Equal(new ushort[] { 1, 2, 3, 4, 5 }, ids.Select(i => i.Index).ToArray());
        s.Free(ids[3]); s.Free(ids[1]);
        Assert.Equal(2, s.Allocate().Index);
        Assert.Equal(4, s.Allocate().Index);
        Assert.Equal(6, s.Allocate().Index);
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, s.AliveIndexes().ToArray());
    }

    [Fact]
    public void StaleId_IsNotAliveAfterReuse()
    {
        var s = new SlotArray(8);
        EntityId a = s.Allocate();
        Assert.True(s.IsAlive(a));
        s.Free(a);
        Assert.False(s.IsAlive(a));
        EntityId b = s.Allocate();
        Assert.Equal(a.Index, b.Index);
        Assert.NotEqual(a.Generation, b.Generation);
        Assert.False(s.IsAlive(a));
        Assert.True(s.IsAlive(b));
        Assert.False(s.Free(a));
        Assert.Equal(1, s.Count);
    }

    [Fact]
    public void Full_Throws_AndNoneIsNeverAlive()
    {
        var s = new SlotArray(3);
        s.Allocate(); s.Allocate();
        Assert.Throws<InvalidOperationException>(() => s.Allocate());
        Assert.False(s.IsAlive(EntityId.None));
    }
}
