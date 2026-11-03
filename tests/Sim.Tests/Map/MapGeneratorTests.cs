using Rts.Data;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.Map;

public class MapGeneratorTests
{
    [Fact]
    public void SameSeedSameMapDifferentSeedDifferentMap()
    {
        ulong a = MapHasher.Compute(MapGenerator.Generate(48, 7));
        Assert.Equal(a, MapHasher.Compute(MapGenerator.Generate(48, 7)));
        Assert.NotEqual(a, MapHasher.Compute(MapGenerator.Generate(48, 8)));
    }

    [Theory]
    [InlineData(32, 1)]
    [InlineData(64, 1)]
    [InlineData(47, 3)]
    [InlineData(96, 9)]
    public void MapIsRotationallySymmetricAndConnected(int size, ulong seed)
    {
        MapData m = MapGenerator.Generate(size, seed);
        int n = size * size;
        for (int i = 0; i < n; i++)
        {
            int j = n - 1 - i;
            Assert.Equal(m.Land[i], m.Land[j]);
            Assert.Equal(m.HeightLevel[i], m.HeightLevel[j]);
            Assert.Equal(m.Overlay[i], m.Overlay[j]);
            Assert.Equal(m.Density[i], m.Density[j]);
        }
        Assert.Equal(2, m.StartPositions.Count);
        Assert.Equal(m.StartPositions[0].X, size - 1 - m.StartPositions[1].X);
        Assert.Equal(m.StartPositions[0].Y, size - 1 - m.StartPositions[1].Y);
        Assert.True(MapGenerator.IsConnected(m, Locomotion.Tracked));
        foreach (var s in m.StartPositions)
        {
            Assert.Equal(LandType.Clear, m.Land[m.IndexOf(s)]);
            Assert.Equal(OverlayKind.None, m.Overlay[m.IndexOf(s)]);
        }
    }

    [Fact]
    public void MapHasExpectedFeatures()
    {
        MapData m = MapGenerator.Generate(64, 1);
        bool Has(LandType l) => m.Land.Contains(l);
        Assert.True(Has(LandType.Ash) && Has(LandType.Cliff) && Has(LandType.Water));
        Assert.Contains(OverlayKind.Ashfall, m.Overlay);
        Assert.True(m.Overlay.Count(o => o == OverlayKind.Pitchblende) >= 16); // 2x2 pits: 2 start + 2 contested
        Assert.True(m.Overlay.Select((o, i) => o == OverlayKind.Pitchblende ? m.Density[i] : 8).All(d => d is >= 8 and <= 12));
        Assert.True(m.Overlay.Select((o, i) => o == OverlayKind.Ashfall ? m.Density[i] : 3).All(d => d is >= 3 and <= 12));
    }

    [Fact]
    public void RejectsBadSize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MapGenerator.Generate(8, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => MapGenerator.Generate(200, 1));
    }
}
